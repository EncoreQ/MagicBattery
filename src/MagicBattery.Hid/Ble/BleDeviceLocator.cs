using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace MagicBattery.Hid.Ble;

/// <summary>
/// 在已配对的 BLE 设备里找出暴露 Battery Service(0x180F)的 Magic 设备(真机适配层)。
/// 配对/连接交给 Windows 蓝牙设置,本项目不做(见 CLAUDE.md「明确不做」)。
///
/// ⚠️ 待真机手测:设备识别先按名称粗筛,真机上应改用更稳的标识(地址段/服务特征),
/// 见 protocol-spec.md §5.1 / §8 U6。
/// </summary>
public static class BleDeviceLocator
{
    private static readonly string[] NameHints = { "Trackpad", "Magic" };

    /// <summary>尝试定位并打开一个 Magic 设备的 Battery Level characteristic。</summary>
    /// <returns>成功返回可用的 GATT 封装;找不到返回 <c>null</c>。</returns>
    public static async Task<IBleBatteryGatt?> TryFindAsync(CancellationToken ct)
    {
        string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(pairingState: true);
        DeviceInformationCollection devices = await DeviceInformation
            .FindAllAsync(selector)
            .AsTask(ct)
            .ConfigureAwait(false);

        foreach (DeviceInformation info in devices)
        {
            ct.ThrowIfCancellationRequested();

            if (!LooksLikeMagicDevice(info.Name))
            {
                continue;
            }

            using BluetoothLEDevice? device = await BluetoothLEDevice
                .FromIdAsync(info.Id).AsTask(ct).ConfigureAwait(false);
            if (device is null)
            {
                continue;
            }

            GattDeviceServicesResult services = await device
                .GetGattServicesForUuidAsync(GattServiceUuids.Battery)
                .AsTask(ct).ConfigureAwait(false);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            {
                continue;
            }

            GattCharacteristicsResult characteristics = await services.Services[0]
                .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel)
                .AsTask(ct).ConfigureAwait(false);
            if (characteristics.Status == GattCommunicationStatus.Success &&
                characteristics.Characteristics.Count > 0)
            {
                return new WinRtBleGatt(characteristics.Characteristics[0]);
            }
        }

        return null;
    }

    private static bool LooksLikeMagicDevice(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (string hint in NameHints)
        {
            if (name.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
