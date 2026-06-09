using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 设备电量的文案格式化(纯函数,可单测)。
/// 精确设备(Magic)显示百分比;粗档设备(手柄)显示档位名。
/// <see cref="Device"/> 给单设备一行(菜单逐设备项);<see cref="Tooltip"/> 给托盘 hover 多行汇总。
/// </summary>
public static class TooltipFormatter
{
    /// <summary>单设备一行:<c>触控板 87% · 未充电 · 蓝牙</c> 或 <c>手柄 高 · 未充电 · 蓝牙</c>;离线则 <c>… · 未连接</c>。</summary>
    public static string Device(DeviceBattery d)
    {
        string name = DeviceKindNames.Of(d.Kind);
        if (d.Availability == BatteryAvailability.Disconnected)
        {
            return $"{name} · 未连接";
        }

        string conn = ConnectionText(d.Connection);
        string charge = d.IsCharging ? "充电中" : "未充电";
        string amount = d.Percentage is int pct ? $"{pct}%" : BatteryLevelNames.Of(d.Level);
        return $"{name} {amount} · {charge} · {conn}";
    }

    /// <summary>托盘 hover:每在线设备一行 + 末行更新时间;无在线设备时占位。</summary>
    public static string Tooltip(IReadOnlyList<DeviceBattery> devices)
    {
        List<DeviceBattery> live = devices
            .Where(d => d.Availability == BatteryAvailability.Live)
            .ToList();

        if (live.Count == 0)
        {
            return "Magic 设备 · 未连接";
        }

        string lines = string.Join("\n", live.Select(Device));
        DateTimeOffset last = live.Max(d => d.LastUpdate);
        return $"{lines}\n更新于 {last:HH:mm}";
    }

    private static string ConnectionText(DeviceConnection connection) => connection switch
    {
        DeviceConnection.Usb => "USB",
        DeviceConnection.Bluetooth => "蓝牙",
        _ => "未知",
    };
}
