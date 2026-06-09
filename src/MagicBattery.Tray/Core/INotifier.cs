namespace MagicBattery.Tray.Core;

/// <summary>
/// 弹通知的薄抽象。生产实现包住 <c>TaskbarIcon.ShowNotification</c>(见 App),
/// 让告警链路(<see cref="LowBatteryAlerter"/> 的决策 → 弹窗)在单测里可用假实现验证。
/// </summary>
public interface INotifier
{
    void Notify(string title, string message);
}
