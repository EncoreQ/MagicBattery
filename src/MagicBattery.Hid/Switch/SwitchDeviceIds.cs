namespace MagicBattery.Hid;

/// <summary>
/// Nintendo 手柄的 VID/PID 识别。数值来自 Linux 内核 drivers/hid/hid-ids.h / hid-nintendo.c。
/// 本期只做 Pro Controller;Joy-Con / NES 同协议,留 PID 扩展位。
/// </summary>
public static class SwitchDeviceIds
{
    /// <summary>Nintendo VID。</summary>
    public const int VendorNintendo = 0x057E;

    /// <summary>Switch Pro Controller。</summary>
    public const int ProController = 0x2009;

    // 同协议、本期未启用(留扩展位)
    public const int JoyConLeft = 0x2006;
    public const int JoyConRight = 0x2007;
    public const int NesController = 0x2017;

    /// <summary>当前支持电量读取的手柄 PID(仅 Pro Controller)。</summary>
    public static readonly IReadOnlyList<int> BatteryPids = new[] { ProController };

    /// <summary>给定 (VID, PID) 是否为当前支持电量读取的 Nintendo 手柄。</summary>
    public static bool IsSwitchBatteryDevice(int vid, int pid) =>
        vid == VendorNintendo && BatteryPids.Contains(pid);
}
