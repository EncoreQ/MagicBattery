using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 电量轮询编排器。组合读取层工厂(<see cref="MagicBatteryReaderFactory.Create"/>)与三态结果,
/// 维护一个当前 <see cref="BatteryViewState"/> 并在变化时抛 <see cref="StateChanged"/>。
///
/// 设计成不碰 WPF 类型,<see cref="PollOnceAsync"/> 可被单测直接、确定性地调用;
/// reader 经 <c>Func</c> 注入,测试用假 reader 驱动,完全脱离真机。
///
/// 连接恢复策略(对应 CLAUDE.md 验收:USB 拔出切蓝牙 / 睡眠唤醒恢复):
///   读到 Unavailable 时 dispose 并重建 reader 再读一次 —— USB↔蓝牙切换会让旧设备路径失效,
///   重建即重新选路;仍失败则标记 Disconnected。WM_DEVICECHANGE 会经
///   <see cref="RequestRefresh"/> 触发立即轮询,让切换在数秒内生效而非等满 15 分钟。
/// </summary>
public sealed class BatteryMonitor : IDisposable
{
    private readonly Func<IBatteryReader?> _readerFactory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1); // 单飞:定时器与手动刷新不重叠抢设备控制管道

    private IBatteryReader? _reader;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public BatteryMonitor(
        Func<IBatteryReader?> readerFactory,
        TimeSpan interval,
        Func<DateTimeOffset>? clock = null)
    {
        _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
        _interval = interval;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>最近一次产出的快照。</summary>
    public BatteryViewState State { get; private set; } = BatteryViewState.Initial;

    /// <summary>每产出一个新快照触发(含 Disconnected)。订阅方负责切回 UI 线程。</summary>
    public event Action<BatteryViewState>? StateChanged;

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

    /// <summary>读取一次并更新状态。单测直接调它(确定性,不依赖定时器)。</summary>
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
        _reader ??= _readerFactory();
        if (_reader is null)
        {
            PublishDisconnected();
            return;
        }

        BatteryReadResult result = await TryReadAsync(ct).ConfigureAwait(false);

        if (result.Outcome == BatteryReadOutcome.Unavailable)
        {
            // 设备路径可能因 USB↔蓝牙切换 / 睡眠而失效,重建一次再读
            RecreateReader();
            if (_reader is null)
            {
                PublishDisconnected();
                return;
            }

            result = await TryReadAsync(ct).ConfigureAwait(false);
            if (result.Outcome == BatteryReadOutcome.Unavailable)
            {
                PublishDisconnected();
                return;
            }
        }

        ApplyResult(result);
    }

    private async Task<BatteryReadResult> TryReadAsync(CancellationToken ct)
    {
        try
        {
            return await _reader!.ReadAsync(ct).ConfigureAwait(false);
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

    private void ApplyResult(BatteryReadResult result)
    {
        DateTimeOffset now = _clock();

        if (result.Outcome == BatteryReadOutcome.Updated && result.Status is { } status)
        {
            Publish(new BatteryViewState(
                status.Percentage, status.IsCharging, status.Connection, now, BatteryAvailability.Live));
        }
        else
        {
            // Unchanged:沿用上次的值,仅刷新时间并确保标记为在线
            Publish(State with { LastUpdate = now, Availability = BatteryAvailability.Live });
        }
    }

    private void RecreateReader()
    {
        _reader?.Dispose();
        _reader = _readerFactory();
    }

    private void PublishDisconnected() =>
        Publish(State with { Availability = BatteryAvailability.Disconnected });

    private void Publish(BatteryViewState state)
    {
        State = state;
        StateChanged?.Invoke(state);
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
            // 关停期间的异常忽略
        }

        _reader?.Dispose();
        _loopCts?.Dispose();
        _gate.Dispose();
    }
}
