namespace MagicBattery.Hid;

/// <summary>
/// 一次读取的三态结果。区分这三态是为了避免把「无新值」「读取失败」
/// 错当成 0%,从而误触 Phase 3 的低电量告警(见 protocol-spec.md §3.2 / §9)。
/// </summary>
public enum BatteryReadOutcome
{
    /// <summary>读到一个与上次不同的有效新值。</summary>
    Updated,

    /// <summary>读取成功但值未变化(例如满电时驱动跳过刷新)。</summary>
    Unchanged,

    /// <summary>本次读取失败 / 设备未就绪 / 报文怪值,无有效数据。</summary>
    Unavailable,
}

/// <summary>
/// 读取结果。<see cref="Status"/> 仅在 <see cref="BatteryReadOutcome.Updated"/> 时非空。
/// </summary>
public sealed record BatteryReadResult(BatteryReadOutcome Outcome, BatteryStatus? Status)
{
    public static BatteryReadResult Updated(BatteryStatus status) =>
        new(BatteryReadOutcome.Updated, status);

    public static readonly BatteryReadResult Unchanged = new(BatteryReadOutcome.Unchanged, null);

    public static readonly BatteryReadResult Unavailable = new(BatteryReadOutcome.Unavailable, null);
}
