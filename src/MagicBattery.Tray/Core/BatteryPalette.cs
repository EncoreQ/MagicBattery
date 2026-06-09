using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>与 WPF 无关的 RGB 颜色(Core 保持可单测、无需 STA)。渲染层再转成 WPF Color。</summary>
public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>电量档位 → 托盘图标颜色的纯映射。</summary>
public static class BatteryPalette
{
    /// <summary>未连接 / 无数据时的置灰底色。</summary>
    public static readonly RgbColor Disconnected = new(0x9E, 0x9E, 0x9E);

    /// <summary>档位 → 颜色。</summary>
    public static RgbColor ColorFor(BatteryLevel level) => level switch
    {
        BatteryLevel.Full => new RgbColor(0x4C, 0xAF, 0x50),     // 绿
        BatteryLevel.High => new RgbColor(0x8B, 0xC3, 0x4A),     // 黄绿
        BatteryLevel.Medium => new RgbColor(0xFF, 0xC1, 0x07),   // 琥珀
        BatteryLevel.Low => new RgbColor(0xFF, 0x98, 0x00),      // 橙
        BatteryLevel.Critical => new RgbColor(0xF4, 0x43, 0x36), // 红
        _ => Disconnected,
    };
}
