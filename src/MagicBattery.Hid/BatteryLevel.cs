namespace MagicBattery.Hid;

/// <summary>
/// 5 档电量等级 —— 全设备的公共货币。序号按严重度升序(Critical 最低、Full 最高),
/// 便于「取最低电量设备」直接比 ordinal。
///
/// Magic 设备由精确百分比映射而来(见 <see cref="BatteryLevels.FromPercentage"/>);
/// Switch 手柄原生只给这 5 档(byte[2]&gt;&gt;5 = 0..4),与本枚举 1:1。
/// </summary>
public enum BatteryLevel
{
    /// <summary>≤10% / 手柄档 0。红。</summary>
    Critical = 0,

    /// <summary>&gt;10 且 ≤25 / 手柄档 1。橙。</summary>
    Low = 1,

    /// <summary>&gt;25 且 ≤50 / 手柄档 2。琥珀。</summary>
    Medium = 2,

    /// <summary>&gt;50 且 ≤75 / 手柄档 3。黄绿。</summary>
    High = 3,

    /// <summary>&gt;75 / 手柄档 4。绿。</summary>
    Full = 4,
}

/// <summary>电量等级的纯映射。</summary>
public static class BatteryLevels
{
    /// <summary>精确百分比 → 档位(Magic;边界 &gt;75 / &gt;50 / &gt;25 / &gt;10）。</summary>
    public static BatteryLevel FromPercentage(int percentage) => percentage switch
    {
        > 75 => BatteryLevel.Full,
        > 50 => BatteryLevel.High,
        > 25 => BatteryLevel.Medium,
        > 10 => BatteryLevel.Low,
        _ => BatteryLevel.Critical,
    };

    /// <summary>Switch 原生档位 0..4 → <see cref="BatteryLevel"/>(同序,clamp 越界)。</summary>
    public static BatteryLevel FromSwitchRaw(int raw) =>
        (BatteryLevel)Math.Clamp(raw, (int)BatteryLevel.Critical, (int)BatteryLevel.Full);
}
