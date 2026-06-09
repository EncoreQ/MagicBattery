using System.IO;
using System.Runtime.Versioning;
using HidSharp;
using HidSharp.Reports;

namespace MagicBattery.Hid;

/// <summary>
/// 用 HidSharp 枚举本机 HID 设备,挑出带 report 0x90 的 Magic 设备接口并打开成
/// <see cref="IHidInputReportSource"/>。
///
/// 一台设备会暴露多个 HID collection,电量在含 Input report 0x90 的那个;
/// 这里按 report descriptor 精确筛选,不硬编码 col/mi 路径。
/// 多设备(触控板 + 键盘…)各自打开一个源。
/// </summary>
[SupportedOSPlatform("windows")]
public static class MagicHidDeviceEnumerator
{
    /// <summary>
    /// 打开第一个可用的 Magic 电量设备接口(USB 优先)。保留给单设备调用方。
    /// </summary>
    public static IHidInputReportSource? TryOpenFirst() => OpenAll().FirstOrDefault();

    /// <summary>
    /// 打开**所有**可用的 Magic 电量设备接口,每台一个源。打不开的接口跳过。
    /// 同一台设备只会以一种连接(USB 或蓝牙)出现一个含 report 0x90 的接口。
    /// </summary>
    public static IReadOnlyList<IHidInputReportSource> OpenAll()
    {
        var sources = new List<IHidInputReportSource>();

        IEnumerable<HidDevice> candidates = DeviceList.Local.GetHidDevices()
            .Where(d => MagicDeviceIds.IsMagicBatteryDevice(d.VendorID, d.ProductID))
            .Where(HasInputReport0x90)
            .OrderBy(d => d.VendorID == MagicDeviceIds.VendorUsb ? 0 : 1); // USB 优先

        foreach (HidDevice device in candidates)
        {
            DeviceConnection connection = MagicDeviceIds.ConnectionFor(device.VendorID);
            DeviceKind kind = MagicDeviceIds.KindFor(device.ProductID);
            string key = DeviceKeyFor(device);
            try
            {
                sources.Add(new Win32HidInputReportSource(device.DevicePath, connection, kind, key));
            }
            catch (IOException)
            {
                // 这个接口打不开,试下一个
            }
        }

        return sources;
    }

    // 稳定标识:优先序列号(USB↔蓝牙间不变),回退设备路径
    private static string DeviceKeyFor(HidDevice device)
    {
        try
        {
            string? serial = device.GetSerialNumber();
            if (!string.IsNullOrWhiteSpace(serial))
            {
                return serial;
            }
        }
        catch
        {
            // 某些设备取不到序列号,回退路径
        }

        return device.DevicePath;
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
