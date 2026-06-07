using HidSharp;

namespace MagicBattery.Hid.Usb;

/// <summary>
/// 用 HidSharp 枚举本机 HID 设备,挑出 USB 电量支持的 Magic 设备并打开(真机适配层)。
///
/// ⚠️ 待真机手测:同一设备会暴露多个 HID 接口(Device Management / Trackpad / ...),
/// 电量在其中某个接口的 feature report 上。Phase 1 先返回第一个匹配 (VID,PID) 的可打开接口;
/// 真机校准时若打开错接口读不到,需按描述符进一步筛选(protocol-spec.md §2.3 / §8 U1/U4)。
/// </summary>
public static class UsbHidDeviceEnumerator
{
    /// <summary>
    /// 尝试打开一个 USB 电量支持的 Magic 设备。
    /// </summary>
    /// <returns>成功返回已打开的连接;没有匹配设备或无法打开返回 <c>null</c>。</returns>
    public static IUsbHidConnection? TryOpenFirst()
    {
        foreach (HidDevice device in DeviceList.Local.GetHidDevices())
        {
            if (!MagicDeviceIds.IsUsbBatterySupported(device.VendorID, device.ProductID))
            {
                continue;
            }

            if (device.TryOpen(out HidStream stream))
            {
                return new HidSharpUsbConnection(stream);
            }
        }

        return null;
    }
}
