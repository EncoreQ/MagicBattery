using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class LowBatteryAlerterTests
{
    private static readonly int[] Thresholds = { 20, 10, 5 };

    private static LowBatteryAlerter Make(bool enabled = true) => new(Thresholds, () => enabled);

    // 精确设备(传 % + 派生档位)
    private static AlertDecision? Pct(LowBatteryAlerter a, int pct, bool charging = false) =>
        a.Evaluate("magic", DeviceKind.Keyboard, BatteryLevels.FromPercentage(pct), pct, charging);

    // 粗档设备(手柄,percentage = null)
    private static AlertDecision? Pad(LowBatteryAlerter a, BatteryLevel level, bool charging = false) =>
        a.Evaluate("pad", DeviceKind.Gamepad, level, null, charging);

    // ---- 精确设备(% 阈值) ----

    [Fact]
    public void Percent_fires_once_per_threshold_crossing()
    {
        LowBatteryAlerter a = Make();

        Pct(a, 18).Should().NotBeNull();   // 跌穿 20
        Pct(a, 17).Should().BeNull();      // 仍在 20 档
        Pct(a, 9).Should().NotBeNull();    // 跌穿 10
        Pct(a, 6).Should().BeNull();
        Pct(a, 4)!.Percentage.Should().Be(4); // 跌穿 5,文案带当前 %
    }

    [Fact]
    public void Percent_multi_threshold_drop_fires_once()
    {
        LowBatteryAlerter a = Make();

        Pct(a, 4)!.Percentage.Should().Be(4); // 一次跌到 4:跨 20/10/5
        Pct(a, 4).Should().BeNull();
    }

    [Fact]
    public void Percent_charging_suppresses_and_rearms()
    {
        LowBatteryAlerter a = Make();
        Pct(a, 8).Should().NotBeNull();

        Pct(a, 8, charging: true).Should().BeNull();

        Pct(a, 8).Should().NotBeNull();
    }

    [Fact]
    public void Percent_recovery_rearms()
    {
        LowBatteryAlerter a = Make();
        Pct(a, 8).Should().NotBeNull();
        Pct(a, 55).Should().BeNull();
        Pct(a, 8).Should().NotBeNull();
    }

    [Fact]
    public void Disabled_never_fires()
    {
        LowBatteryAlerter a = Make(enabled: false);
        Pct(a, 3).Should().BeNull();
        Pad(a, BatteryLevel.Critical).Should().BeNull();
    }

    // ---- 粗档设备(手柄,按档位) ----

    [Fact]
    public void Coarse_fires_entering_low_then_critical()
    {
        LowBatteryAlerter a = Make();

        Pad(a, BatteryLevel.High).Should().BeNull();        // 高档不弹
        Pad(a, BatteryLevel.Low)!.Level.Should().Be(BatteryLevel.Low);  // 进入低档
        Pad(a, BatteryLevel.Low).Should().BeNull();         // 不重复
        Pad(a, BatteryLevel.Critical)!.Level.Should().Be(BatteryLevel.Critical); // 进入危
    }

    [Fact]
    public void Coarse_drop_to_critical_fires_most_severe_once()
    {
        LowBatteryAlerter a = Make();

        Pad(a, BatteryLevel.Critical)!.Level.Should().Be(BatteryLevel.Critical); // 直接跌到危,跨 低+危
        Pad(a, BatteryLevel.Critical).Should().BeNull();
    }

    [Fact]
    public void Coarse_charging_and_recovery_rearm()
    {
        LowBatteryAlerter a = Make();
        Pad(a, BatteryLevel.Low).Should().NotBeNull();

        Pad(a, BatteryLevel.Low, charging: true).Should().BeNull(); // 充电重置
        Pad(a, BatteryLevel.Low).Should().NotBeNull();              // 再弹

        Pad(a, BatteryLevel.High).Should().BeNull();                // 回升
        Pad(a, BatteryLevel.Low).Should().NotBeNull();              // 再次跌落
    }

    [Fact]
    public void Devices_are_independent()
    {
        LowBatteryAlerter a = Make();

        Pct(a, 8).Should().NotBeNull();
        Pad(a, BatteryLevel.Low).Should().NotBeNull(); // 另一台设备独立触发
    }
}
