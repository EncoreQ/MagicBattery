using MagicBattery.Hid.Internal;

namespace MagicBattery.Hid.Ble;

/// <summary>
/// BLE 路径的 <see cref="IBatteryReader"/>:组合一个 <see cref="IBleBatteryGatt"/>。
/// BLE 值直接 0-100,无换算;<see cref="BatteryStatus.IsCharging"/> 默认 false(§4 / §5)。
/// 订阅底层 GATT notify,变化时转推到 <see cref="Changes"/>。
/// </summary>
public sealed class BleBatteryReader : IBatteryReader
{
    private readonly IBleBatteryGatt _gatt;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SimpleSubject<BatteryStatus> _changes = new();
    private readonly IDisposable _notifySubscription;

    private int? _lastPercentage;

    public BleBatteryReader(IBleBatteryGatt gatt, Func<DateTimeOffset>? clock = null)
    {
        _gatt = gatt ?? throw new ArgumentNullException(nameof(gatt));
        _clock = clock ?? (() => DateTimeOffset.Now);
        _notifySubscription = _gatt.LevelNotifications.Subscribe(
            new NotifyObserver(this));
    }

    public DeviceConnection Connection => DeviceConnection.Bluetooth;

    public IObservable<BatteryStatus> Changes => _changes;

    public async Task<BatteryReadResult> ReadAsync(CancellationToken ct)
    {
        byte? level = await _gatt.ReadBatteryLevelAsync(ct).ConfigureAwait(false);
        if (level is null)
        {
            return BatteryReadResult.Unavailable;
        }

        return Apply(level.Value);
    }

    /// <summary>把一个原始 level 应用为三态结果(读取与 notify 共用)。</summary>
    private BatteryReadResult Apply(byte level)
    {
        int percentage = Math.Clamp((int)level, 0, 100);
        if (_lastPercentage == percentage)
        {
            return BatteryReadResult.Unchanged;
        }

        _lastPercentage = percentage;
        var status = new BatteryStatus(percentage, IsCharging: false, DeviceConnection.Bluetooth, _clock());
        _changes.OnNext(status);
        return BatteryReadResult.Updated(status);
    }

    public void Dispose()
    {
        _notifySubscription.Dispose();
        _gatt.Dispose();
    }

    /// <summary>把 GATT notify 推送的 level 走与主动读取相同的 Apply 逻辑。</summary>
    private sealed class NotifyObserver : IObserver<byte>
    {
        private readonly BleBatteryReader _owner;

        public NotifyObserver(BleBatteryReader owner) => _owner = owner;

        public void OnNext(byte value) => _owner.Apply(value);

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}
