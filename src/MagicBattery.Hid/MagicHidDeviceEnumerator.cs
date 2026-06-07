using System.IO;
using System.Runtime.Versioning;
using HidSharp;
using HidSharp.Reports;

namespace MagicBattery.Hid;

/// <summary>
/// 用 HidSharp 枚举本机 HID 设备,挑出带 report 0x90 的 Magic 设备接口并打开成
/// <see cref="IHidInputReportSource"/>。USB(VID 05AC)优先于蓝牙(004C)。
///
/// 一台设备会暴露多个 HID collection,电量在含 Input report 0x90 的那个;
/// 这里按 report descriptor 精确筛选,不硬编码 col/mi 路径。
/// </summary>
[SupportedOSPlatform("windows")]
public static class MagicHidDeviceEnumerator
{
    /// <summary>
    /// 打开第一个可用的 Magic 电量设备接口。
    /// </summary>
    /// <returns>成功返回已打开的源;没有匹配设备或都打不开返回 <c>null</c>。</returns>
    public static IHidInputReportSource? TryOpenFirst()
    {
        IEnumerable<HidDevice> candidates = DeviceList.Local.GetHidDevices()
            .Where(d => MagicDeviceIds.IsMagicBatteryDevice(d.VendorID, d.ProductID))
            .Where(HasInputReport0x90)
            .OrderBy(d => d.VendorID == MagicDeviceIds.VendorUsb ? 0 : 1); // USB 优先

        foreach (HidDevice device in candidates)
        {
            DeviceConnection connection = MagicDeviceIds.ConnectionFor(device.VendorID);
            try
            {
                return new Win32HidInputReportSource(device.DevicePath, connection);
            }
            catch (IOException)
            {
                // 这个接口打不开,试下一个
            }
        }

        return null;
    }

    private static bool HasInputReport0x90(HidDevice device)
    {
        try
        {
            ReportDescriptor rd = device.GetReportDescriptor();
            foreach (Report rep in rd.Reports)
            {
                if (rep.ReportType == ReportType.Input && rep.ReportID == BatteryReport0x90.ReportId)
                {
                    return true;
                }
            }
        }
        catch
        {
            // 某些 collection 拿不到 / 解析不了描述符,跳过
        }

        return false;
    }
}
