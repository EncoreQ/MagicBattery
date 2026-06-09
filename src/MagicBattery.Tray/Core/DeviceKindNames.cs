using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>设备类别 → 中文显示名(UI 文案)。</summary>
public static class DeviceKindNames
{
    public static string Of(DeviceKind kind) => kind switch
    {
        DeviceKind.Trackpad => "触控板",
        DeviceKind.Keyboard => "键盘",
        DeviceKind.Mouse => "鼠标",
        _ => "设备",
    };
}
