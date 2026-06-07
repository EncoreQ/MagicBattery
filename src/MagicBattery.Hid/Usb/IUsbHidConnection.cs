namespace MagicBattery.Hid.Usb;

/// <summary>
/// 对单个已打开的 USB HID 设备的最小抽象。把真实 HID 调用挡在接口后,
/// 让 <see cref="UsbBatteryReader"/> 能用 fake 脱离真机单测。
/// </summary>
public interface IUsbHidConnection : IDisposable
{
    /// <summary>
    /// 发起一次 HID Get Feature Report。
    /// </summary>
    /// <param name="reportId">目标 feature report 的 Report ID。</param>
    /// <param name="reportLength">期望读取的报文长度(含 ReportId 字节)。</param>
    /// <returns>原始报文字节(首字节为 ReportId)。</returns>
    /// <exception cref="System.IO.IOException">设备未就绪 / 读取失败时抛出,由上层转 Unavailable。</exception>
    byte[] GetFeatureReport(byte reportId, int reportLength);
}
