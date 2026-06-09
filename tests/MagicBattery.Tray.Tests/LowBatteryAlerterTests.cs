using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class LowBatteryAlerterTests
{
    private static readonly int[] Thresholds = { 20, 10, 5 };

    private static LowBatteryAlerter Make(bool enabled = true) =>
        new(Thresholds, () => enabled);

    private static AlertDecision? Eval(LowBatteryAlerter a, int pct, bool charging = false) =>
        a.Evaluate("dev", DeviceKind.Keyboard, pct, charging);

    [Fact]
    public void Fires_once_per_threshold_crossing()
    {
        LowBatteryAlerter a = Make();

        Eval(a, 18)!.Threshold.Should().Be(20);   // 跌穿 20
        Eval(a, 17).Should().BeNull();             // 仍在 20 档,不重复
        Eval(a, 9)!.Threshold.Should().Be(10);     // 跌穿 10
        Eval(a, 6).Should().BeNull();
        Eval(a, 4)!.Threshold.Should().Be(5);      // 跌穿 5
    }

    [Fact]
    public void Dropping_through_multiple_thresholds_fires_most_severe()
    {
        LowBatteryAlerter a = Make();

        AlertDecision? d = Eval(a, 4); // 一次从充足跌到 4:跨 20/10/5

        d!.Threshold.Should().Be(5);
        d.Percentage.Should().Be(4);
        Eval(a, 4).Should().BeNull(); // 不重复
    }

    [Fact]
    public void Charging_suppresses_and_rearms()
    {
        LowBatteryAlerter a = Make();
        Eval(a, 8)!.Threshold.Should().Be(10);

        Eval(a, 8, charging: true).Should().BeNull(); // 充电不弹并重置

        Eval(a, 8)!.Threshold.Should().Be(10);         // 拔电后重新武装,再弹
    }

    [Fact]
    public void Recovery_above_threshold_rearms()
    {
        LowBatteryAlerter a = Make();
        Eval(a, 8)!.Threshold.Should().Be(10);

        Eval(a, 55).Should().BeNull();   // 回升到充足

        Eval(a, 8)!.Threshold.Should().Be(10); // 再次跌落重新触发
    }

    [Fact]
    public void Disabled_never_fires()
    {
        LowBatteryAlerter a = Make(enabled: false);

        Eval(a, 3).Should().BeNull();
        Eval(a, 1).Should().BeNull();
    }

    [Fact]
    public void Devices_are_independent()
    {
        LowBatteryAlerter a = Make();

        a.Evaluate("a", DeviceKind.Keyboard, 8, false).Should().NotBeNull();
        a.Evaluate("b", DeviceKind.Trackpad, 8, false).Should().NotBeNull(); // 另一台独立触发
    }

    [Fact]
    public void Above_all_thresholds_never_fires()
    {
        LowBatteryAlerter a = Make();

        Eval(a, 100).Should().BeNull();
        Eval(a, 21).Should().BeNull();
    }
}
