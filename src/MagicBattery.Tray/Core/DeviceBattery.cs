using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>读取层当前是否拿得到该设备。</summary>
public enum BatteryAvailability
{
    /// <summary>正常读到数据。</summary>
    Live,

    /// <summary>设备不在线 / 读取失败,无可用数据。</summary>
    Disconnected,
}

/// <summary>
/// 单个设备的电量快照(不可变)。<see cref="BatteryCoordinator"/> 每轮为每台在线设备产出一个,
/// 汇成快照列表推给 UI;UI 据此刷新菜单逐设备项,并取最低电量者渲染主图标 / tooltip。
/// </summary>
public sealed record DeviceBattery(
    string DeviceKey,
    DeviceKind Kind,
    int? Percentage,
    bool IsCharging,
    DeviceConnection Connection,
    DateTimeOffset LastUpdate,
    BatteryAvailability Availability)
{
    /// <summary>无任何设备时的占位(图标置灰)。</summary>
    public static readonly DeviceBattery None = new(
        DeviceKey: "",
        Kind: DeviceKind.Unknown,
        Percentage: null,
        IsCharging: false,
        Connection: DeviceConnection.Disconnected,
        LastUpdate: default,
        Availability: BatteryAvailability.Disconnected);
}
