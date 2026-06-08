using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>读取层当前是否拿得到设备。</summary>
public enum BatteryAvailability
{
    /// <summary>正常读到数据。</summary>
    Live,

    /// <summary>设备不在线 / 连续读取失败,无可用数据。</summary>
    Disconnected,
}

/// <summary>
/// 供 UI 绑定的不可变快照。<see cref="BatteryMonitor"/> 每次轮询产出一个,
/// 经 Dispatcher 推给托盘刷新图标 / tooltip。
/// </summary>
public sealed record BatteryViewState(
    int? Percentage,
    bool IsCharging,
    DeviceConnection Connection,
    DateTimeOffset LastUpdate,
    BatteryAvailability Availability)
{
    /// <summary>启动时尚未读到任何数据的初始态。</summary>
    public static readonly BatteryViewState Initial = new(
        Percentage: null,
        IsCharging: false,
        Connection: DeviceConnection.Disconnected,
        LastUpdate: default,
        Availability: BatteryAvailability.Disconnected);
}
