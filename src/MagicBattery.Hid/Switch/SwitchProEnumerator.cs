using System.IO;
using System.Runtime.Versioning;
using HidSharp;

namespace MagicBattery.Hid;

/// <summary>
/// 枚举并打开本机的 Switch Pro 手柄(VID 0x057E + Pro PID),每台一个
/// <see cref="ISwitchReportSource"/>。本期只支持蓝牙连接。
/// </summary>
[SupportedOSPlatform("windows")]
public static class SwitchProEnumerator
{
    // 蓝牙 HID 设备路径里带 BT 服务 GUID;USB 路径里带 "usb"
    private const string BluetoothHidGuid = "00001124-0000-1000-8000-00805f9b34fb";

    public static IReadOnlyList<ISwitchReportSource> OpenAll()
    {
        var sources = new List<ISwitchReportSource>();

        IEnumerable<HidDevice> candidates = DeviceList.Local.GetHidDevices()
            .Where(d => SwitchDeviceIds.IsSwitchBatteryDevice(d.VendorID, d.ProductID));

        foreach (HidDevice device in candidates)
        {
            // 本期只读蓝牙手柄(USB 需握手,侵入性,未做)
            if (!IsBluetooth(device.DevicePath))
            {
                continue;
            }

            string key = DeviceKeyFor(device);
            try
            {
                sources.Add(new HidStreamSwitchSource(
                    device, DeviceConnection.Bluetooth, DeviceKind.Gamepad, key));
            }
            catch (IOException)
            {
                // 打不开(独占等),跳过
            }
        }

        return sources;
    }

    private static bool IsBluetooth(string devicePath) =>
        devicePath.Contains(BluetoothHidGuid, StringComparison.OrdinalIgnoreCase) ||
        devicePath.Contains("bth", StringComparison.OrdinalIgnoreCase);

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
            // 取不到序列号,回退路径
        }

        return device.DevicePath;
    }
}
