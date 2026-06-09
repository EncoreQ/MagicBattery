using MagicBattery.Hid.Internal;

namespace MagicBattery.Hid;

/// <summary>
/// Switch Pro 手柄的电量读取器。组合一个 <see cref="ISwitchReportSource"/> 与
/// <see cref="SwitchProReport"/> 解析,产出三态结果。与 Magic 的 reader 实现同一 <see cref="IBatteryReader"/>
/// 契约,因此 <c>BatteryCoordinator</c> 把手柄和 Magic 设备一视同仁。
/// </summary>
public sealed class SwitchProBatteryReader : IBatteryReader
{
    private readonly ISwitchReportSource _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SimpleSubject<BatteryStatus> _changes = new();

    private (BatteryLevel Level, bool IsCharging)? _last;

    public SwitchProBatteryReader(ISwitchReportSource source, Func<DateTimeOffset>? clock = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public DeviceConnection Connection => _source.Connection;

    public DeviceKind Kind => _source.Kind;

    public string DeviceKey => _source.DeviceKey;

    public IObservable<BatteryStatus> Changes => _changes;

    public Task<BatteryReadResult> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] report;
        try
        {
            report = _source.ReadStandardReport();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(BatteryReadResult.Unavailable);
        }

        BatteryStatus? status = SwitchProReport.Parse(report, _source.Connection, _clock());
        if (status is null)
        {
            return Task.FromResult(BatteryReadResult.Unavailable);
        }

        var signature = (status.Level, status.IsCharging); // 手柄只有粗档,以 (档位, 充电) 去重
        if (_last == signature)
        {
            return Task.FromResult(BatteryReadResult.Unchanged);
        }

        _last = signature;
        _changes.OnNext(status);
        return Task.FromResult(BatteryReadResult.Updated(status));
    }

    public void Dispose() => _source.Dispose();
}
