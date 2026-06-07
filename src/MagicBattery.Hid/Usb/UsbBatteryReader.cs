using MagicBattery.Hid.Internal;

namespace MagicBattery.Hid.Usb;

/// <summary>
/// USB 路径的 <see cref="IBatteryReader"/>:组合一个 <see cref="IUsbHidConnection"/>(IO)
/// 与 <see cref="UsbBatteryParser"/>(解析),产出三态结果。
///
/// 三态判定(protocol-spec.md §3.2 / §9):
///   - IO 抛异常 / 解析返回 null  → Unavailable
///   - 解析出的百分比与上次相同    → Unchanged(覆盖「满电跳过刷新」语义)
///   - 否则                        → Updated,并向 Changes 推送
/// </summary>
public sealed class UsbBatteryReader : IBatteryReader
{
    private readonly IUsbHidConnection _connection;
    private readonly UsbBatteryReportLayout _layout;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SimpleSubject<BatteryStatus> _changes = new();

    private int? _lastPercentage;

    public UsbBatteryReader(
        IUsbHidConnection connection,
        UsbBatteryReportLayout layout,
        Func<DateTimeOffset>? clock = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public DeviceConnection Connection => DeviceConnection.Usb;

    public IObservable<BatteryStatus> Changes => _changes;

    public Task<BatteryReadResult> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] report;
        try
        {
            report = _connection.GetFeatureReport(_layout.ReportId, _layout.ReportLength);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 设备未就绪 / 读取失败:保留上次值,本次 Unavailable
            return Task.FromResult(BatteryReadResult.Unavailable);
        }

        BatteryStatus? status = UsbBatteryParser.Parse(report, _layout, _clock());
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

    public void Dispose() => _connection.Dispose();
}
