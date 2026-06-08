namespace MagicBattery.Tray.Core;

/// <summary>
/// 电量档位。对应 CLAUDE.md「按电量分 5 档」:&gt;75 / &gt;50 / &gt;25 / &gt;10 / ≤10。
/// </summary>
public enum BatteryTier
{
    /// <summary>≤10%,红。</summary>
    Critical,

    /// <summary>&gt;10 且 ≤25,橙。</summary>
    Low,

    /// <summary>&gt;25 且 ≤50,琥珀。</summary>
    Medium,

    /// <summary>&gt;50 且 ≤75,黄绿。</summary>
    High,

    /// <summary>&gt;75,绿。</summary>
    Full,
}

/// <summary>与 WPF 无关的 RGB 颜色(Core 保持可单测、无需 STA)。渲染层再转成 WPF Color。</summary>
public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>电量百分比 → 档位 / 颜色的纯映射。</summary>
public static class BatteryTierMap
{
    /// <summary>断言:百分比落入哪一档(边界见 <see cref="BatteryTier"/>)。</summary>
    public static BatteryTier FromPercentage(int percentage) => percentage switch
    {
        > 75 => BatteryTier.Full,
        > 50 => BatteryTier.High,
        > 25 => BatteryTier.Medium,
        > 10 => BatteryTier.Low,
        _ => BatteryTier.Critical,
    };

    /// <summary>未连接 / 无数据时的置灰底色。</summary>
    public static readonly RgbColor Disconnected = new(0x9E, 0x9E, 0x9E);

    /// <summary>档位 → 托盘图标底色。</summary>
    public static RgbColor ColorFor(BatteryTier tier) => tier switch
    {
        BatteryTier.Full => new RgbColor(0x4C, 0xAF, 0x50),     // 绿
        BatteryTier.High => new RgbColor(0x8B, 0xC3, 0x4A),     // 黄绿
        BatteryTier.Medium => new RgbColor(0xFF, 0xC1, 0x07),   // 琥珀
        BatteryTier.Low => new RgbColor(0xFF, 0x98, 0x00),      // 橙
        BatteryTier.Critical => new RgbColor(0xF4, 0x43, 0x36), // 红
        _ => Disconnected,
    };
}
