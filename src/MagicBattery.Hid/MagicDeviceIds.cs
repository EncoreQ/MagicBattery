namespace MagicBattery.Hid;

/// <summary>
/// Apple Magic 系列设备的 VID/PID 常量与识别。数值来自 Linux 内核 drivers/hid/hid-ids.h
/// 并经真机校准确认(见 docs/protocol-spec.md「实测更正」)。
/// 关键:USB 与蓝牙是同一台设备的两种连接,**仅 VID 不同**(USB=0x05AC、BT=0x004C),
/// 电量都在 HID Input report 0x90。识别一律按 VID/PID,**不靠设备名**(名称会本地化)。
/// </summary>
public static class MagicDeviceIds
{
    /// <summary>USB 直连时的 VID。</summary>
    public const int VendorUsb = 0x05AC;

    /// <summary>蓝牙(BT Classic HID)时的 VID。</summary>
    public const int VendorBt = 0x004C;

    // ---- Magic Trackpad 2(MVP 目标)----
    public const int MagicTrackpad2 = 0x0265;       // Lightning
    public const int MagicTrackpad2Usbc = 0x0324;   // USB-C

    // ---- Phase 3 预留(report 0x90 模型同构,实测 MK 也是 Input id 0x90 len 3)----
    public const int MagicMouse2 = 0x0269;
    public const int MagicMouse2Usbc = 0x0323;

    /// <summary>当前支持电量读取的 PID(MVP 仅 MT2;MM2/MK 留到 Phase 3)。</summary>
    public static readonly IReadOnlyList<int> BatteryPids = new[]
    {
        MagicTrackpad2,
        MagicTrackpad2Usbc,
    };

    /// <summary>给定 (VID, PID) 是否为当前支持电量读取的 Magic 设备(USB 或蓝牙)。</summary>
    public static bool IsMagicBatteryDevice(int vid, int pid) =>
        (vid == VendorUsb || vid == VendorBt) && BatteryPids.Contains(pid);

    /// <summary>由 VID 推断连接类型。</summary>
    public static DeviceConnection ConnectionFor(int vid) => vid switch
    {
        VendorUsb => DeviceConnection.Usb,
        VendorBt => DeviceConnection.Bluetooth,
        _ => DeviceConnection.Disconnected,
    };
}
