namespace MagicBattery.Hid;

/// <summary>
/// 把 Magic 设备的 HID Input report <c>0x90</c>(3 字节)解析成 <see cref="BatteryStatus"/>。
/// 布局经真机校准确定(docs/protocol-spec.md「实测更正」),USB 与蓝牙通用:
/// <code>
///   byte[0] = 0x90 (report id)
///   byte[1] = 充电标志(0x00 未充电 / 非 0 充电)
///   byte[2] = 电量百分比,直读 0-100
/// </code>
/// 无状态纯函数,是本层单测的主要目标。
/// </summary>
public static class BatteryReport0x90
{
    public const byte ReportId = 0x90;
    public const int ReportLength = 3;
    public const int ChargeFlagOffset = 1;
    public const int BatteryOffset = 2;

    /// <summary>
    /// 解析一份 report 0x90。
    /// </summary>
    /// <param name="connection">由设备 VID 判定的连接类型,写入结果。</param>
    /// <returns>
    /// 成功返回 <see cref="BatteryStatus"/>;无效返回 <c>null</c>:
    /// 长度不足、首字节非 0x90、或 byte[2] &gt; 100(睡眠/异常怪值)。
    /// </returns>
    public static BatteryStatus? Parse(
        ReadOnlySpan<byte> report,
        DeviceConnection connection,
        DateTimeOffset now)
    {
        if (report.Length < ReportLength || report[0] != ReportId)
        {
            return null;
        }

        int percentage = report[BatteryOffset];
        if (percentage > 100)
        {
            return null; // 0-100 才是有效电量,越界视为怪值
        }

        bool charging = report[ChargeFlagOffset] != 0;
        return new BatteryStatus(percentage, charging, connection, now);
    }
}
