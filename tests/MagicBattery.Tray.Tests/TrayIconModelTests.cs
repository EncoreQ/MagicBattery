using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TrayIconModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private static DeviceBattery Live(int pct, bool charging, DeviceConnection conn) =>
        new("k", DeviceKind.Trackpad, pct, charging, conn, Now, BatteryAvailability.Live);

    [Fact]
    public void Live_value_shows_number_and_tier_color_no_dim()
    {
        TrayIconModel model = TrayIconModel.FromState(Live(87, false, DeviceConnection.Usb));

        model.Text.Should().Be("87");
        model.Dimmed.Should().BeFalse();
        model.ShowBolt.Should().BeFalse();
        model.Background.Should().Be(BatteryTierMap.ColorFor(BatteryTier.Full));
    }

    [Fact]
    public void Charging_sets_bolt()
    {
        TrayIconModel model = TrayIconModel.FromState(Live(8, true, DeviceConnection.Bluetooth));

        model.ShowBolt.Should().BeTrue();
        model.Background.Should().Be(BatteryTierMap.ColorFor(BatteryTier.Critical));
    }

    [Fact]
    public void Full_battery_shows_100()
    {
        TrayIconModel.FromState(Live(100, false, DeviceConnection.Bluetooth)).Text.Should().Be("100");
    }

    [Fact]
    public void None_is_dimmed_question_mark()
    {
        TrayIconModel model = TrayIconModel.FromState(DeviceBattery.None);

        model.Text.Should().Be("?");
        model.Dimmed.Should().BeTrue();
        model.ShowBolt.Should().BeFalse();
        model.Background.Should().Be(BatteryTierMap.Disconnected);
    }
}
