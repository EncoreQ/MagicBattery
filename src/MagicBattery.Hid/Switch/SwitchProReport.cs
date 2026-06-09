namespace MagicBattery.Hid;

/// <summary>
/// 解析 Switch Pro Controller 的标准完整输入报文 <c>0x30</c> 中的电量字节。
/// 协议来源:Linux <c>drivers/hid/hid-nintendo.c</c>(<c>joycon_parse_battery_status</c>)
/// + dekuNukem 逆向 + 真机校准(见 docs/switch-pro-spec.md)。
/// <code>
///   byte[0] = 0x30 (report id)
///   byte[1] = timer
///   byte[2] = bat_con:bit0=外部供电, bit4=充电中, bits5-7(&gt;&gt;5)=电量档 0..4
/// </code>
/// 手柄只给 5 粗档,无精确百分比,故结果 <see cref="BatteryStatus.Percentage"/> 为 null。
/// </summary>
public static class SwitchProReport
{
    public const byte StandardReportId = 0x30;
    public const int BatConOffset = 2;
    public const byte ChargingBit = 0x10;

    /// <summary>
    /// 解析一帧 0x30 报文。无效(长度不足 / 首字节非 0x30)返回 <c>null</c>。
    /// </summary>
    public static BatteryStatus? Parse(
        ReadOnlySpan<byte> report,
        DeviceConnection connection,
        DateTimeOffset now)
    {
        if (report.Length <= BatConOffset || report[0] != StandardReportId)
        {
            return null;
        }

        byte batCon = report[BatConOffset];
        bool charging = (batCon & ChargingBit) != 0;
        BatteryLevel level = BatteryLevels.FromSwitchRaw(batCon >> 5); // bits 5-7 = 0..4

        return new BatteryStatus(level, Percentage: null, charging, connection, now);
    }
}
