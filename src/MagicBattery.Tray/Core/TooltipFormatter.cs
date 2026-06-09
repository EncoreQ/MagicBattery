using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 设备电量的文案格式化(纯函数,可单测)。
/// <see cref="Device"/> 给单设备一行(菜单逐设备项);<see cref="Tooltip"/> 给托盘 hover 多行汇总。
/// </summary>
public static class TooltipFormatter
{
    /// <summary>单设备一行:<c>键盘 87% · 充电中 · 蓝牙</c>;离线则 <c>键盘 · 未连接</c>。</summary>
    public static string Device(DeviceBattery d)
    {
        string name = DeviceKindNames.Of(d.Kind);
        if (d.Availability == BatteryAvailability.Disconnected || d.Percentage is null)
        {
            return $"{name} · 未连接";
        }

        string conn = ConnectionText(d.Connection);
        string charge = d.IsCharging ? "充电中" : "未充电";
        return $"{name} {d.Percentage}% · {charge} · {conn}";
    }

    /// <summary>托盘 hover:每在线设备一行 + 末行更新时间;无在线设备时占位。</summary>
    public static string Tooltip(IReadOnlyList<DeviceBattery> devices)
    {
        List<DeviceBattery> live = devices
            .Where(d => d.Availability == BatteryAvailability.Live && d.Percentage is not null)
            .ToList();

        if (live.Count == 0)
        {
            return "Magic 设备 · 未连接";
        }

        string lines = string.Join("\n", live.Select(Device));
        DateTimeOffset last = live.Max(d => d.LastUpdate);
        return $"{lines}\n更新 {last:HH:mm}";
    }

    private static string ConnectionText(DeviceConnection connection) => connection switch
    {
        DeviceConnection.Usb => "USB",
        DeviceConnection.Bluetooth => "蓝牙",
        _ => "未知",
    };
}
