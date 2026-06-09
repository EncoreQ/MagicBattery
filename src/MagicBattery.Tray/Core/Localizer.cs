using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 全部面向用户的字符串与格式化,按 <see cref="AppLanguage"/> 提供中/英两套。
/// 取代并吸收原 TooltipFormatter / DeviceKindNames / BatteryLevelNames。纯字符串,可单测。
/// </summary>
public sealed class Localizer
{
    public AppLanguage Language { get; }

    private Localizer(AppLanguage language) => Language = language;

    public static readonly Localizer Chinese = new(AppLanguage.Chinese);
    public static readonly Localizer English = new(AppLanguage.English);

    public static Localizer For(AppLanguage language) =>
        language == AppLanguage.English ? English : Chinese;

    private bool En => Language == AppLanguage.English;

    // ---- 菜单 / 通知标签 ----
    public string MenuRefresh => En ? "Refresh now" : "立即刷新";
    public string MenuAlerts => En ? "Low-battery alerts" : "低电量告警";
    public string MenuAutostart => En ? "Run at startup" : "开机自启";
    public string MenuLanguage => En ? "Language" : "语言";
    public string MenuFollowSystem => En ? "Follow system" : "跟随系统";
    public string MenuExit => En ? "Exit" : "退出";
    public string MenuNoDevice => En ? "No device detected" : "未检测到设备";
    public string DefaultTooltip => En ? "MagicBattery" : "Magic 设备";
    public string AlertTitle => En ? "Low battery" : "电量不足";

    // ---- 映射 ----
    public string DeviceName(DeviceKind kind) => kind switch
    {
        DeviceKind.Trackpad => En ? "Trackpad" : "触控板",
        DeviceKind.Keyboard => En ? "Keyboard" : "键盘",
        DeviceKind.Mouse => En ? "Mouse" : "鼠标",
        DeviceKind.Gamepad => En ? "Gamepad" : "手柄",
        _ => En ? "Device" : "设备",
    };

    public string LevelName(BatteryLevel level) => level switch
    {
        BatteryLevel.Full => En ? "Full" : "满",
        BatteryLevel.High => En ? "High" : "高",
        BatteryLevel.Medium => En ? "Medium" : "中",
        BatteryLevel.Low => En ? "Low" : "低",
        BatteryLevel.Critical => En ? "Critical" : "危",
        _ => "—",
    };

    private string ConnectionText(DeviceConnection connection) => connection switch
    {
        DeviceConnection.Usb => "USB",
        DeviceConnection.Bluetooth => En ? "Bluetooth" : "蓝牙",
        _ => En ? "Unknown" : "未知",
    };

    private string ChargeText(bool charging) =>
        charging ? (En ? "charging" : "充电中") : (En ? "on battery" : "未充电");

    // ---- 组合格式化 ----

    /// <summary>单设备一行。精确设备显示百分比,粗档设备显示档位名;离线显示「未连接」。</summary>
    public string Device(DeviceBattery d)
    {
        string name = DeviceName(d.Kind);
        if (d.Availability == BatteryAvailability.Disconnected)
        {
            return En ? $"{name} · disconnected" : $"{name} · 未连接";
        }

        string amount = d.Percentage is int pct ? $"{pct}%" : LevelName(d.Level);
        return $"{name} {amount} · {ChargeText(d.IsCharging)} · {ConnectionText(d.Connection)}";
    }

    /// <summary>
    /// 托盘 hover:每在线设备一行(精简「名称 电量」)+ 末行更新时间;无在线设备时占位。
    /// 注意:Windows 托盘 tooltip 上限 128 字符(Shell szTip),故此处只放速览信息;
    /// 充电状态 / 连接方式等完整信息在右键菜单(<see cref="Device"/>)。
    /// </summary>
    public string Tooltip(IReadOnlyList<DeviceBattery> devices)
    {
        List<DeviceBattery> live = devices
            .Where(d => d.Availability == BatteryAvailability.Live)
            .ToList();

        if (live.Count == 0)
        {
            return En ? "MagicBattery · no device" : "Magic 设备 · 未连接";
        }

        string lines = string.Join("\n", live.Select(TooltipLine));
        DateTimeOffset last = live.Max(d => d.LastUpdate);
        string updated = En ? $"Updated {last:HH:mm}" : $"更新于 {last:HH:mm}";
        return $"{lines}\n{updated}";
    }

    private string TooltipLine(DeviceBattery d)
    {
        string amount = d.Percentage is int pct ? $"{pct}%" : LevelName(d.Level);
        return $"{DeviceName(d.Kind)} {amount}";
    }

    /// <summary>低电量告警正文。</summary>
    public string AlertBody(DeviceKind kind, BatteryLevel level, int? percentage)
    {
        string name = DeviceName(kind);
        string amount = percentage is int p
            ? $"{p}%"
            : (En ? LevelName(level) : $"{LevelName(level)}档");
        return En ? $"{name} battery {amount} — please charge" : $"{name}电量 {amount},请及时充电";
    }
}
