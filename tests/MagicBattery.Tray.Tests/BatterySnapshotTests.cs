using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class BatterySnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private static DeviceBattery Live(DeviceKind kind, int pct) =>
        new(kind.ToString(), kind, pct, false, DeviceConnection.Bluetooth, Now, BatteryAvailability.Live);

    private static DeviceBattery Dead(DeviceKind kind) =>
        new(kind.ToString(), kind, null, false, DeviceConnection.Disconnected, Now, BatteryAvailability.Disconnected);

    [Fact]
    public void Primary_picks_lowest_live_percentage()
    {
        var devices = new[] { Live(DeviceKind.Trackpad, 87), Live(DeviceKind.Keyboard, 8), Live(DeviceKind.Mouse, 50) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Keyboard);
    }

    [Fact]
    public void Primary_ignores_disconnected_devices()
    {
        var devices = new[] { Dead(DeviceKind.Keyboard), Live(DeviceKind.Trackpad, 40) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Trackpad);
    }

    [Fact]
    public void Primary_of_empty_is_none()
    {
        BatterySnapshot.Primary(Array.Empty<DeviceBattery>()).Should().Be(DeviceBattery.None);
    }

    [Fact]
    public void Primary_all_disconnected_is_none()
    {
        BatterySnapshot.Primary(new[] { Dead(DeviceKind.Keyboard), Dead(DeviceKind.Trackpad) })
            .Should().Be(DeviceBattery.None);
    }
}
