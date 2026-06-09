namespace MagicBattery.Hid;

/// <summary>
/// 单个 Magic 设备的电量读取器。USB 与蓝牙共用同一实现,区别仅在底层 HID 源的 VID。
/// 多设备由 <see cref="MagicBatteryReaderFactory.CreateAll"/> 枚举产出,每台一个 reader。
/// </summary>
public interface IBatteryReader : IDisposable
{
    /// <summary>此 reader 代表的连接路径。</summary>
    DeviceConnection Connection { get; }

    /// <summary>设备类别(触控板 / 键盘 / 鼠标)。</summary>
    DeviceKind Kind { get; }

    /// <summary>设备稳定标识(序列号或路径),用于多设备区分与跨连接连续。</summary>
    string DeviceKey { get; }

    /// <summary>主动读取一次电量,返回三态结果。</summary>
    Task<BatteryReadResult> ReadAsync(CancellationToken ct);

    /// <summary>
    /// 电量变化推送流。只在产生 <see cref="BatteryReadOutcome.Updated"/> 时推送。
    /// BLE 实现可借此把设备的 GATT notify 直接转出来。
    /// </summary>
    IObservable<BatteryStatus> Changes { get; }
}
