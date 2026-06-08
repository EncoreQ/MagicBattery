using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TrayIconModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Live_value_shows_number_and_tier_color_no_dim()
    {
        var state = new BatteryViewState(87, false, DeviceConnection.Usb, Now, BatteryAvailability.Live);

        TrayIconModel model = TrayIconModel.FromState(state);

        model.Text.Should().Be("87");
        model.Dimmed.Should().BeFalse();
        model.ShowBolt.Should().BeFalse();
        model.Background.Should().Be(BatteryTierMap.ColorFor(BatteryTier.Full));
    }

    [Fact]
    public void Charging_sets_bolt()
    {
        var state = new BatteryViewState(8, true, DeviceConnection.Bluetooth, Now, BatteryAvailability.Live);

        TrayIconModel model = TrayIconModel.FromState(state);

        model.ShowBolt.Should().BeTrue();
        model.Background.Should().Be(BatteryTierMap.ColorFor(BatteryTier.Critical));
    }

    [Fact]
    public void Full_battery_shows_100()
    {
        var state = new BatteryViewState(100, false, DeviceConnection.Bluetooth, Now, BatteryAvailability.Live);

        TrayIconModel.FromState(state).Text.Should().Be("100");
    }

    [Fact]
    public void Disconnected_is_dimmed_question_mark()
    {
        TrayIconModel model = TrayIconModel.FromState(BatteryViewState.Initial);

        model.Text.Should().Be("?");
        model.Dimmed.Should().BeTrue();
        model.ShowBolt.Should().BeFalse();
        model.Background.Should().Be(BatteryTierMap.Disconnected);
    }
}
