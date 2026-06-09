using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TrayIconModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private static DeviceBattery Magic(int pct, bool charging, DeviceConnection conn) =>
        new("k", DeviceKind.Trackpad, BatteryLevels.FromPercentage(pct), pct, charging, conn, Now,
            BatteryAvailability.Live);

    private static DeviceBattery Pad(BatteryLevel level, bool charging) =>
        new("p", DeviceKind.Gamepad, level, null, charging, DeviceConnection.Bluetooth, Now,
            BatteryAvailability.Live);

    [Fact]
    public void Precise_device_shows_number_and_tier_color()
    {
        TrayIconModel model = TrayIconModel.FromState(Magic(87, false, DeviceConnection.Usb));

        model.Kind.Should().Be(TrayIconKind.Number);
        model.Text.Should().Be("87");
        model.Dimmed.Should().BeFalse();
        model.ShowBolt.Should().BeFalse();
        model.Background.Should().Be(BatteryPalette.ColorFor(BatteryLevel.Full));
    }

    [Fact]
    public void Charging_sets_bolt()
    {
        TrayIconModel.FromState(Magic(8, true, DeviceConnection.Bluetooth)).ShowBolt.Should().BeTrue();
    }

    [Fact]
    public void Full_battery_shows_100()
    {
        TrayIconModel.FromState(Magic(100, false, DeviceConnection.Bluetooth)).Text.Should().Be("100");
    }

    [Fact]
    public void Coarse_device_uses_bars_equal_to_level()
    {
        TrayIconModel model = TrayIconModel.FromState(Pad(BatteryLevel.High, charging: false));

        model.Kind.Should().Be(TrayIconKind.Bars);
        model.Bars.Should().Be(3);                       // High = 序号 3
        model.Text.Should().BeEmpty();
        model.Background.Should().Be(BatteryPalette.ColorFor(BatteryLevel.High));
    }

    [Fact]
    public void Coarse_full_is_four_bars()
    {
        TrayIconModel.FromState(Pad(BatteryLevel.Full, false)).Bars.Should().Be(4);
    }

    [Fact]
    public void None_is_dimmed_question_mark()
    {
        TrayIconModel model = TrayIconModel.FromState(DeviceBattery.None);

        model.Kind.Should().Be(TrayIconKind.Number);
        model.Text.Should().Be("?");
        model.Dimmed.Should().BeTrue();
        model.Background.Should().Be(BatteryPalette.Disconnected);
    }
}
