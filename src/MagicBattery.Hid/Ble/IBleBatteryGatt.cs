namespace MagicBattery.Hid.Ble;

/// <summary>
/// 对标准 GATT Battery Service(0x180F / Battery Level 0x2A19)的最小抽象。
/// 值直接是 0-100 百分比,无需换算(protocol-spec.md §4)。把 WinRT 调用挡在接口后,
/// 让 <see cref="BleBatteryReader"/> 能脱离真机/蓝牙单测。
/// </summary>
public interface IBleBatteryGatt : IDisposable
{
    /// <summary>主动读一次 Battery Level。</summary>
    /// <returns>0-100 的电量;读不到返回 <c>null</c>。</returns>
    Task<byte?> ReadBatteryLevelAsync(CancellationToken ct);

    /// <summary>设备主动推送的 Battery Level 通知流(GATT notify)。</summary>
    IObservable<byte> LevelNotifications { get; }
}
