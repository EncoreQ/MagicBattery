using MagicBattery.Hid.Internal;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace MagicBattery.Hid.Ble;

/// <summary>
/// <see cref="IBleBatteryGatt"/> 的 WinRT 实现,封装标准 Battery Level
/// characteristic(0x2A19)的读取与 notify(真机适配层)。
///
/// ⚠️ 待真机手测:本类不在 Phase 1 跑真机。CCCD 订阅是否被 Apple 设备接受、
/// 通知频率等需拿到设备后验证(protocol-spec.md §4.1 / §8 U6)。
/// </summary>
public sealed class WinRtBleGatt : IBleBatteryGatt
{
    private readonly GattCharacteristic _batteryLevel;
    private readonly SimpleSubject<byte> _notifications = new();

    public WinRtBleGatt(GattCharacteristic batteryLevelCharacteristic)
    {
        _batteryLevel = batteryLevelCharacteristic
            ?? throw new ArgumentNullException(nameof(batteryLevelCharacteristic));
        _batteryLevel.ValueChanged += OnValueChanged;
    }

    public IObservable<byte> LevelNotifications => _notifications;

    public async Task<byte?> ReadBatteryLevelAsync(CancellationToken ct)
    {
        GattReadResult result = await _batteryLevel
            .ReadValueAsync(BluetoothCacheMode.Uncached)
            .AsTask(ct)
            .ConfigureAwait(false);

        return result.Status == GattCommunicationStatus.Success
            ? ReadFirstByte(result.Value)
            : null;
    }

    /// <summary>订阅 GATT notify(写 CCCD)。真机手测时调用。</summary>
    public async Task<bool> EnableNotificationsAsync(CancellationToken ct)
    {
        GattCommunicationStatus status = await _batteryLevel
            .WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify)
            .AsTask(ct)
            .ConfigureAwait(false);

        return status == GattCommunicationStatus.Success;
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        byte? level = ReadFirstByte(args.CharacteristicValue);
        if (level is not null)
        {
            _notifications.OnNext(level.Value);
        }
    }

    private static byte? ReadFirstByte(IBuffer? buffer)
    {
        if (buffer is null || buffer.Length < 1)
        {
            return null;
        }

        using var reader = DataReader.FromBuffer(buffer);
        return reader.ReadByte();
    }

    public void Dispose() => _batteryLevel.ValueChanged -= OnValueChanged;
}
