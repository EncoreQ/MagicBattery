using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests.Fakes;

/// <summary>记录弹出的通知,供告警链路断言。</summary>
internal sealed class FakeNotifier : INotifier
{
    public List<(string Title, string Message)> Notifications { get; } = new();

    public void Notify(string title, string message) => Notifications.Add((title, message));
}
