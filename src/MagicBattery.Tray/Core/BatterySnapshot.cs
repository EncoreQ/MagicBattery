namespace MagicBattery.Tray.Core;

/// <summary>对一组设备快照的纯查询:挑出用于主图标的「代表设备」。</summary>
public static class BatterySnapshot
{
    /// <summary>
    /// 主图标代表 = 在线设备里**电量最低**的那个(最需要充电)。先比 5 档 <c>Level</c>(精确与粗档设备
    /// 的公共可比量),同档再比精确百分比(都有时),仍平再按类别稳定排序。
    /// 没有任何在线设备时返回 <see cref="DeviceBattery.None"/>(置灰)。
    /// </summary>
    public static DeviceBattery Primary(IReadOnlyList<DeviceBattery> devices)
    {
        DeviceBattery? lowest = devices
            .Where(d => d.Availability == BatteryAvailability.Live)
            .OrderBy(d => d.Level)
            .ThenBy(d => d.Percentage ?? int.MaxValue)
            .ThenBy(d => d.Kind)
            .FirstOrDefault();

        return lowest ?? DeviceBattery.None;
    }
}
