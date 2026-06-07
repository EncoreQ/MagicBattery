using MagicBattery.Hid.Ble;
using MagicBattery.Hid.Usb;

namespace MagicBattery.Hid;

/// <summary>
/// 按当前连接选择电量读取器:USB 优先,其次 BLE(protocol-spec.md §5.1)。
/// 同一设备 USB 与 BLE 同时可用时取 USB(数据更直接,且在充电)。
///
/// 注意:USB 拔出后切换到 BLE 的「去抖编排」属于 Phase 2,本工厂只做一次性选路。
/// </summary>
public static class MagicBatteryReaderFactory
{
    /// <summary>
    /// 探测并创建当前可用的读取器。
    /// </summary>
    /// <returns>USB 或 BLE 读取器;两路都不可用时返回 <c>null</c>(对应 Disconnected)。</returns>
    public static async Task<IBatteryReader?> CreateAsync(CancellationToken ct)
    {
        // 1) USB 优先
        IUsbHidConnection? usb = UsbHidDeviceEnumerator.TryOpenFirst();
        if (usb is not null)
        {
            return new UsbBatteryReader(usb, UsbBatteryReportLayout.MagicTrackpad2Synthetic);
        }

        // 2) 回退 BLE
        IBleBatteryGatt? ble = await BleDeviceLocator.TryFindAsync(ct).ConfigureAwait(false);
        if (ble is not null)
        {
            return new BleBatteryReader(ble);
        }

        // 3) 都没有
        return null;
    }
}
