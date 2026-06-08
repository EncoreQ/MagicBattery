using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 把 <see cref="BatteryViewState"/> 格式化成托盘 tooltip 文案。
/// CLAUDE.md:百分比 + 连接方式 + 最后更新时间。纯函数,可单测。
/// </summary>
public static class TooltipFormatter
{
    public static string Format(BatteryViewState state)
    {
        if (state.Availability == BatteryAvailability.Disconnected || state.Percentage is null)
        {
            return state.LastUpdate == default
                ? "Magic Trackpad 2 · 未连接"
                : $"未连接 · 最后 {state.LastUpdate:HH:mm}";
        }

        string conn = ConnectionText(state.Connection);
        string charge = state.IsCharging ? "充电中" : "未充电";
        return $"{state.Percentage}% · {charge} · {conn} · {state.LastUpdate:HH:mm}";
    }

    private static string ConnectionText(DeviceConnection connection) => connection switch
    {
        DeviceConnection.Usb => "USB",
        DeviceConnection.Bluetooth => "蓝牙",
        _ => "未知",
    };
}
