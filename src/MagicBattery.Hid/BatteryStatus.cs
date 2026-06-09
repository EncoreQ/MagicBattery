namespace MagicBattery.Hid;

/// <summary>
/// 一次有效的电量读数。
/// </summary>
/// <param name="Level">5 档电量等级(全设备通用)。</param>
/// <param name="Percentage">精确百分比 [0,100];仅精确设备(Magic)有,手柄等粗档设备为 <c>null</c>。</param>
/// <param name="IsCharging">是否在充电。</param>
/// <param name="Connection">本次读数来自哪条连接路径。</param>
/// <param name="Timestamp">读数产生时间。</param>
public sealed record BatteryStatus(
    BatteryLevel Level,
    int? Percentage,
    bool IsCharging,
    DeviceConnection Connection,
    DateTimeOffset Timestamp);
