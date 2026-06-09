namespace MagicBattery.Tray.Core;

/// <summary>
/// 描述托盘图标「要画什么」的纯数据:显示文本、底色、是否叠加充电闪电、是否置灰。
/// 把「画什么」(可单测)与「怎么光栅化」(<c>TrayIconRenderer</c>,WPF/UI 线程)分开。
/// </summary>
public sealed record TrayIconModel(string Text, RgbColor Background, bool ShowBolt, bool Dimmed)
{
    public static TrayIconModel FromState(DeviceBattery state)
    {
        if (state.Availability == BatteryAvailability.Disconnected || state.Percentage is null)
        {
            return new TrayIconModel("?", BatteryTierMap.Disconnected, ShowBolt: false, Dimmed: true);
        }

        int pct = Math.Clamp(state.Percentage.Value, 0, 100);
        BatteryTier tier = BatteryTierMap.FromPercentage(pct);
        return new TrayIconModel(
            Text: pct.ToString(),
            Background: BatteryTierMap.ColorFor(tier),
            ShowBolt: state.IsCharging,
            Dimmed: false);
    }
}
