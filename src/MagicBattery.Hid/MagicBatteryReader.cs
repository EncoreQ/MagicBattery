using MagicBattery.Hid.Internal;

namespace MagicBattery.Hid;

/// <summary>
/// 统一的电量读取器:组合一个 <see cref="IHidInputReportSource"/> 与
/// <see cref="BatteryReport0x90"/> 解析,产出三态结果。USB 与蓝牙共用此实现,
/// 区别仅在源的 <see cref="IHidInputReportSource.Connection"/>。
///
/// 三态(docs/protocol-spec.md §3.2 / §9):
///   - IO 抛异常 / 解析返回 null → Unavailable
///   - 电量与上次相同            → Unchanged
///   - 否则                      → Updated,并向 Changes 推送
/// </summary>
public sealed class MagicBatteryReader : IBatteryReader
{
    private readonly IHidInputReportSource _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SimpleSubject<BatteryStatus> _changes = new();

    private int? _lastPercentage;

    public MagicBatteryReader(IHidInputReportSource source, Func<DateTimeOffset>? clock = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public DeviceConnection Connection => _source.Connection;

    public IObservable<BatteryStatus> Changes => _changes;

    public Task<BatteryReadResult> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] report;
        try
        {
            report = _source.GetInputReport(BatteryReport0x90.ReportId, BatteryReport0x90.ReportLength);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(BatteryReadResult.Unavailable);
        }

        BatteryStatus? status = BatteryReport0x90.Parse(report, _source.Connection, _clock());
        if (status is null)
        {
            return Task.FromResult(BatteryReadResult.Unavailable);
        }

        if (_lastPercentage == status.Percentage)
        {
            return Task.FromResult(BatteryReadResult.Unchanged);
        }

        _lastPercentage = status.Percentage;
        _changes.OnNext(status);
        return Task.FromResult(BatteryReadResult.Updated(status));
    }

    public void Dispose() => _source.Dispose();
}
