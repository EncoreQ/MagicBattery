namespace MagicBattery.Hid;

/// <summary>
/// 当前电量读取所走的连接路径。
/// </summary>
public enum DeviceConnection
{
    /// <summary>USB 直连(Lightning / USB-C)。</summary>
    Usb,

    /// <summary>蓝牙 BLE 连接。</summary>
    Bluetooth,

    /// <summary>设备不可用 / 未连接。</summary>
    Disconnected,
}
