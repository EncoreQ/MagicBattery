namespace MagicBattery.Hid;

/// <summary>Magic 设备类别(按 PID 判定),用于多设备区分与 UI 命名。</summary>
public enum DeviceKind
{
    /// <summary>无法归类(理论上不出现,枚举已按 PID 过滤)。</summary>
    Unknown,

    /// <summary>Magic Trackpad 2。</summary>
    Trackpad,

    /// <summary>Magic Keyboard 系列。</summary>
    Keyboard,

    /// <summary>Magic Mouse 2(暂未纳入电量读取,保留分类)。</summary>
    Mouse,

    /// <summary>游戏手柄(Nintendo Switch Pro)。</summary>
    Gamepad,
}
