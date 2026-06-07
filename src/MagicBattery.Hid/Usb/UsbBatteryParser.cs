namespace MagicBattery.Hid.Usb;

/// <summary>
/// 把 USB feature report 原始字节解析成 <see cref="BatteryStatus"/> 的纯函数。
/// 无状态、无 IO,是本阶段单测的主要目标(见 protocol-spec.md §2/§3)。
/// </summary>
public static class UsbBatteryParser
{
    /// <summary>
    /// 解析一份 feature report。
    /// </summary>
    /// <returns>
    /// 解析成功返回 <see cref="BatteryStatus"/>;报文无效或为怪值时返回 <c>null</c>:
    /// 长度不符、ReportId 不匹配、raw 越出 [LogicalMin, LogicalMax] 区间
    /// (覆盖 §3.2 的睡眠/唤醒怪值,例如返回 0 或越界值)。
    /// 上层据此把 null 转成 <see cref="BatteryReadOutcome.Unavailable"/>。
    /// </returns>
    public static BatteryStatus? Parse(
        ReadOnlySpan<byte> report,
        UsbBatteryReportLayout layout,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(layout);

        // 1) 长度校验
        if (report.Length < layout.ReportLength ||
            layout.BatteryByteOffset < 0 ||
            layout.BatteryByteOffset >= report.Length)
        {
            return null;
        }

        // 2) Report ID 校验(报文首字节应等于约定的 ReportId)
        if (report[0] != layout.ReportId)
        {
            return null;
        }

        // 3) 取原始电量值
        int raw = report[layout.BatteryByteOffset];

        // 4) Logical 区间校验:越界即视为怪值(睡眠/唤醒初期会返回 0 或越界值)
        if (layout.LogicalMax <= layout.LogicalMin ||
            raw < layout.LogicalMin ||
            raw > layout.LogicalMax)
        {
            return null;
        }

        // 5) 线性换算到 0-100 并 clamp(§3.1)
        int span = layout.LogicalMax - layout.LogicalMin;
        int percentage = (int)Math.Round((raw - layout.LogicalMin) * 100.0 / span,
            MidpointRounding.AwayFromZero);
        percentage = Math.Clamp(percentage, 0, 100);

        // USB 直连即在充电(§3.2 / §5)
        return new BatteryStatus(percentage, IsCharging: true, DeviceConnection.Usb, now);
    }
}
