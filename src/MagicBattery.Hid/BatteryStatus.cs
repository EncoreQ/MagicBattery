namespace MagicBattery.Hid;

/// <summary>
/// 一次有效的电量读数。
/// </summary>
/// <param name="Percentage">电量百分比,已 clamp 到 [0,100]。</param>
/// <param name="IsCharging">是否在充电(USB 直连即视为充电;BLE 默认 false)。</param>
/// <param name="Connection">本次读数来自哪条连接路径。</param>
/// <param name="Timestamp">读数产生时间。</param>
public sealed record BatteryStatus(
    int Percentage,
    bool IsCharging,
    DeviceConnection Connection,
    DateTimeOffset Timestamp);
