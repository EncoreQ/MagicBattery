using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 多设备电量轮询编排器(由 Phase 2 单设备 BatteryMonitor 泛化而来)。
/// 每轮枚举并打开**全部**在线 Magic 电量设备(<see cref="MagicBatteryReaderFactory.CreateAll"/>),
/// 各读一次后即 dispose,按 DeviceKey 维护快照字典,抛出整组 <see cref="SnapshotChanged"/>。
///
/// 「每轮开-读-弃」让设备增减与 USB↔蓝牙切换天然生效(枚举不到即移除,免去持久 reader 的重建编排)。
/// 不碰 WPF 类型,<see cref="PollOnceAsync"/> 可被单测确定性调用;reader 来源经 <c>Func</c> 注入。
/// </summary>
public sealed class BatteryCoordinator : IDisposable
{
    private readonly Func<IReadOnlyList<IBatteryReader>> _openAll;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1); // 单飞:定时器与手动刷新不重叠抢设备控制管道
    private readonly Dictionary<string, DeviceBattery> _devices = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public BatteryCoordinator(
        Func<IReadOnlyList<IBatteryReader>> openAll,
        TimeSpan interval,
        Func<DateTimeOffset>? clock = null)
    {
        _openAll = openAll ?? throw new ArgumentNullException(nameof(openAll));
        _interval = interval;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>最近一轮的设备快照(按类别排序)。</summary>
    public IReadOnlyList<DeviceBattery> Devices { get; private set; } = Array.Empty<DeviceBattery>();

    /// <summary>每轮轮询后触发,带整组设备快照。订阅方负责切回 UI 线程。</summary>
    public event Action<IReadOnlyList<DeviceBattery>>? SnapshotChanged;

    /// <summary>启动后台轮询:先立即读一次,再每 <c>interval</c> 读一次。</summary>
    public async Task StartAsync(CancellationToken externalCt = default)
    {
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        CancellationToken ct = _loopCts.Token;

        await SafePollAsync(ct).ConfigureAwait(false); // 立即首轮
        _loopTask = RunLoopAsync(ct);
    }

    /// <summary>请求一次立即轮询(供右键菜单「立即刷新」与 WM_DEVICECHANGE 调用)。</summary>
    public void RequestRefresh()
    {
        CancellationToken ct = _loopCts?.Token ?? CancellationToken.None;
        _ = Task.Run(() => SafePollAsync(ct), ct);
    }

    /// <summary>枚举所有设备各读一次并更新快照。单测直接调它(确定性)。</summary>
    public async Task PollOnceAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PollCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PollCoreAsync(CancellationToken ct)
    {
        IReadOnlyList<IBatteryReader> readers = _openAll();
        DateTimeOffset now = _clock();
        var seen = new HashSet<string>();

        foreach (IBatteryReader reader in readers)
        {
            try
            {
                seen.Add(reader.DeviceKey);
                BatteryReadResult result = await TryReadAsync(reader, ct).ConfigureAwait(false);
                _devices[reader.DeviceKey] = BuildState(reader, result, now);
            }
            finally
            {
                reader.Dispose();
            }
        }

        // 本轮未出现的设备 = 已拔出/离线,移除
        foreach (string key in _devices.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _devices.Remove(key);
        }

        Publish();
    }

    private DeviceBattery BuildState(IBatteryReader reader, BatteryReadResult result, DateTimeOffset now)
    {
        _devices.TryGetValue(reader.DeviceKey, out DeviceBattery? prev);

        return result.Outcome switch
        {
            BatteryReadOutcome.Updated when result.Status is { } s =>
                new DeviceBattery(reader.DeviceKey, reader.Kind, s.Percentage, s.IsCharging,
                    s.Connection, now, BatteryAvailability.Live),

            // 新 reader 每轮重建,理论上不会回 Unchanged;稳妥起见保留上次值仍标在线
            BatteryReadOutcome.Unchanged when prev is not null =>
                prev with { LastUpdate = now, Availability = BatteryAvailability.Live },

            // Unavailable / 异常:保留上次已知电量(若有),标记离线
            _ => prev is not null
                ? prev with { Availability = BatteryAvailability.Disconnected }
                : new DeviceBattery(reader.DeviceKey, reader.Kind, null, false,
                    reader.Connection, now, BatteryAvailability.Disconnected),
        };
    }

    private static async Task<BatteryReadResult> TryReadAsync(IBatteryReader reader, CancellationToken ct)
    {
        try
        {
            return await reader.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return BatteryReadResult.Unavailable;
        }
    }

    private void Publish()
    {
        Devices = _devices.Values.OrderBy(d => d.Kind).ToArray();
        SnapshotChanged?.Invoke(Devices);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await SafePollAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    private async Task SafePollAsync(CancellationToken ct)
    {
        try
        {
            await PollOnceAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 停止中,忽略
        }
        catch
        {
            // 单次轮询异常不能让循环停摆
        }
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 关停期间异常忽略
        }

        _loopCts?.Dispose();
        _gate.Dispose();
    }
}
