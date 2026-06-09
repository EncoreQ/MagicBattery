using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>电量档位 → 中文显示名(供粗档设备如手柄显示)。</summary>
public static class BatteryLevelNames
{
    public static string Of(BatteryLevel level) => level switch
    {
        BatteryLevel.Full => "满",
        BatteryLevel.High => "高",
        BatteryLevel.Medium => "中",
        BatteryLevel.Low => "低",
        BatteryLevel.Critical => "危",
        _ => "—",
    };
}
