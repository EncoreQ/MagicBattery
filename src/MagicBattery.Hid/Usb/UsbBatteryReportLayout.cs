namespace MagicBattery.Hid.Usb;

/// <summary>
/// USB feature report 中电量字段的布局。这是 protocol-spec.md §8 那批「待真机实测」
/// 项的载体——report ID / 字节偏移 / Logical Min-Max 全部做成可注入参数,**不写死**,
/// 真机数据到手后只替换常量,解析代码与单测不动。
/// </summary>
/// <param name="ReportId">电量 feature report 的 Report ID。</param>
/// <param name="ReportLength">该 feature report 的总字节数(含 ReportId 字节)。</param>
/// <param name="BatteryByteOffset">电量原始值所在字节偏移(从 0 起,0 即 ReportId 字节)。</param>
/// <param name="LogicalMin">HID Battery Strength usage 的 Logical Minimum。</param>
/// <param name="LogicalMax">HID Battery Strength usage 的 Logical Maximum。</param>
public sealed record UsbBatteryReportLayout(
    byte ReportId,
    int ReportLength,
    int BatteryByteOffset,
    int LogicalMin,
    int LogicalMax)
{
    /// <summary>
    /// Magic Trackpad 2 的占位布局。
    ///
    /// ⚠️ SYNTHETIC — 这些数值是基于 protocol-spec.md §2/§8 的**假设**构造的占位值,
    /// 并非真机实测:
    ///   - ReportId/Offset 为占位,真机须按 §8 U1/U2 dump 描述符确认;
    ///   - 这里先假设 Logical 0..100(即 raw 直接是百分比,§8 U3 待确认);
    ///   - ReportLength 取一个合理上限占位。
    /// 拿到设备后:用真实 dump 替换本常量 + 替换 tests/fixtures/usb 下的合成数据。
    /// </summary>
    public static readonly UsbBatteryReportLayout MagicTrackpad2Synthetic = new(
        ReportId: 0x90,
        ReportLength: 3,
        BatteryByteOffset: 1,
        LogicalMin: 0,
        LogicalMax: 100);
}
