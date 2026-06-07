namespace MagicBattery.Hid;

/// <summary>
/// 电量读取器。USB 与 BLE 各有一个独立实现,上层按当前连接选择
/// (见 <see cref="MagicBatteryReaderFactory"/>)。
/// </summary>
public interface IBatteryReader : IDisposable
{
    /// <summary>此 reader 代表的连接路径。</summary>
    DeviceConnection Connection { get; }

    /// <summary>主动读取一次电量,返回三态结果。</summary>
    Task<BatteryReadResult> ReadAsync(CancellationToken ct);

    /// <summary>
    /// 电量变化推送流。只在产生 <see cref="BatteryReadOutcome.Updated"/> 时推送。
    /// BLE 实现可借此把设备的 GATT notify 直接转出来。
    /// </summary>
    IObservable<BatteryStatus> Changes { get; }
}
