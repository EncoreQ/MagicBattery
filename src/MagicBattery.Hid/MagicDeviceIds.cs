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

    // ---- Magic Trackpad 2 ----
    public const int MagicTrackpad2 = 0x0265;       // Lightning
    public const int MagicTrackpad2Usbc = 0x0324;   // USB-C

    // ---- Magic Keyboard 系列(spec §1;§U7 实测 029C 同样暴露 Input 0x90 len 3,与 MT2 同构)----
    public const int MagicKeyboard2015 = 0x0267;
    public const int MagicKeyboardNumpad2015 = 0x026C;
    public const int MagicKeyboard2021 = 0x029C;
    public const int MagicKeyboardFingerprint2021 = 0x029A;
    public const int MagicKeyboardNumpad2021 = 0x029F;
    public const int MagicKeyboard2024 = 0x0320;
    public const int MagicKeyboardFingerprint2024 = 0x0321;
    public const int MagicKeyboardNumpad2024 = 0x0322;

    // ---- Magic Mouse 2(Windows 未验证,暂不纳入电量读取;仅供 KindFor 分类)----
    public const int MagicMouse2 = 0x0269;
    public const int MagicMouse2Usbc = 0x0323;

    private static readonly IReadOnlyList<int> TrackpadPids = new[]
    {
        MagicTrackpad2, MagicTrackpad2Usbc,
    };

    private static readonly IReadOnlyList<int> KeyboardPids = new[]
    {
        MagicKeyboard2015, MagicKeyboardNumpad2015,
        MagicKeyboard2021, MagicKeyboardFingerprint2021, MagicKeyboardNumpad2021,
        MagicKeyboard2024, MagicKeyboardFingerprint2024, MagicKeyboardNumpad2024,
    };

    private static readonly IReadOnlyList<int> MousePids = new[]
    {
        MagicMouse2, MagicMouse2Usbc,
    };

    /// <summary>当前支持电量读取的 PID(触控板 + 键盘;MM2 未验证,不在内)。</summary>
    public static readonly IReadOnlyList<int> BatteryPids =
        TrackpadPids.Concat(KeyboardPids).ToArray();

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

    /// <summary>由 PID 判定设备类别。</summary>
    public static DeviceKind KindFor(int pid)
    {
        if (TrackpadPids.Contains(pid)) return DeviceKind.Trackpad;
        if (KeyboardPids.Contains(pid)) return DeviceKind.Keyboard;
        if (MousePids.Contains(pid)) return DeviceKind.Mouse;
        return DeviceKind.Unknown;
    }
}
