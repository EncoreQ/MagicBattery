namespace MagicBattery.Tray.Core;

/// <summary>对一组设备快照的纯查询:挑出用于主图标的「代表设备」。</summary>
public static class BatterySnapshot
{
    /// <summary>
    /// 主图标代表 = 在线且有电量的设备里**电量最低**的那个(最需要充电)。
    /// 没有任何在线设备时返回 <see cref="DeviceBattery.None"/>(置灰)。
    /// </summary>
    public static DeviceBattery Primary(IReadOnlyList<DeviceBattery> devices)
    {
        DeviceBattery? lowest = devices
            .Where(d => d.Availability == BatteryAvailability.Live && d.Percentage is not null)
            .OrderBy(d => d.Percentage!.Value)
            .ThenBy(d => d.Kind)
            .FirstOrDefault();

        return lowest ?? DeviceBattery.None;
    }
}
