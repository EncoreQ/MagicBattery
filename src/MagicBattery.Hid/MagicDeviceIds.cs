namespace MagicBattery.Hid;

/// <summary>
/// Apple Magic 系列设备的 VID/PID 常量。数值来自 Linux 内核 drivers/hid/hid-ids.h,
/// 见 protocol-spec.md §1。MVP 只用 Magic Trackpad 2;其余为 Phase 3 预留。
/// </summary>
public static class MagicDeviceIds
{
    /// <summary>USB 直连时的厂商 ID。</summary>
    public const int UsbVendorIdApple = 0x05AC;

    /// <summary>蓝牙(HID over BT)上报时的厂商 ID。</summary>
    public const int BtVendorIdApple = 0x004C;

    // ---- Magic Trackpad 2(MVP 目标)----
    public const int MagicTrackpad2 = 0x0265;       // Lightning
    public const int MagicTrackpad2Usbc = 0x0324;   // USB-C

    // ---- Magic Mouse 2(Phase 3)----
    public const int MagicMouse2 = 0x0269;          // Lightning
    public const int MagicMouse2Usbc = 0x0323;      // USB-C

    /// <summary>
    /// 当前在 USB 下支持电量读取的 (VID, PID) 集合。
    /// 注意:Magic Mouse 1 / Trackpad 1 不在内核 USB 电量支持范围内,故不含。
    /// </summary>
    public static readonly IReadOnlyList<(int Vid, int Pid)> UsbBatterySupported = new[]
    {
        (UsbVendorIdApple, MagicTrackpad2),
        (UsbVendorIdApple, MagicTrackpad2Usbc),
        // Phase 3 放开 MM2:
        // (UsbVendorIdApple, MagicMouse2),
        // (UsbVendorIdApple, MagicMouse2Usbc),
    };

    /// <summary>判断给定 (VID, PID) 是否为当前 USB 电量支持的 Magic 设备。</summary>
    public static bool IsUsbBatterySupported(int vid, int pid)
    {
        foreach (var (v, p) in UsbBatterySupported)
        {
            if (v == vid && p == pid)
            {
                return true;
            }
        }

        return false;
    }
}
