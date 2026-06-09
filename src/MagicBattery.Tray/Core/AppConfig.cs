namespace MagicBattery.Tray.Core;

/// <summary>
/// 用户配置(持久化到 %APPDATA%\MagicBattery\config.json)。
/// 开机自启不在此 —— 它存注册表 Run 键(见 <see cref="AutostartService"/>)。
/// </summary>
public sealed record AppConfig
{
    /// <summary>轮询间隔(分钟)。默认 15,对齐 Magic Utilities。</summary>
    public int PollIntervalMinutes { get; init; } = 15;

    /// <summary>低电量告警总开关。</summary>
    public bool LowBatteryAlertsEnabled { get; init; } = true;

    /// <summary>告警阈值(百分比)。默认 20 / 10 / 5。</summary>
    public IReadOnlyList<int> AlertThresholds { get; init; } = new[] { 20, 10, 5 };

    /// <summary>界面语言设置。默认跟随系统。</summary>
    public LanguagePreference Language { get; init; } = LanguagePreference.System;

    public static AppConfig Default => new();
}
