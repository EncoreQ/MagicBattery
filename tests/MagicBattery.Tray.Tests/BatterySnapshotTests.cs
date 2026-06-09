using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class BatterySnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private static DeviceBattery Magic(DeviceKind kind, int pct) =>
        new(kind.ToString(), kind, BatteryLevels.FromPercentage(pct), pct, false,
            DeviceConnection.Bluetooth, Now, BatteryAvailability.Live);

    private static DeviceBattery Pad(BatteryLevel level) =>
        new("pad", DeviceKind.Gamepad, level, null, false, DeviceConnection.Bluetooth, Now,
            BatteryAvailability.Live);

    private static DeviceBattery Dead(DeviceKind kind) =>
        new(kind.ToString(), kind, BatteryLevel.Critical, null, false, DeviceConnection.Disconnected,
            Now, BatteryAvailability.Disconnected);

    [Fact]
    public void Primary_picks_lowest_level()
    {
        var devices = new[] { Magic(DeviceKind.Trackpad, 87), Magic(DeviceKind.Keyboard, 8), Magic(DeviceKind.Mouse, 50) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Keyboard);
    }

    [Fact]
    public void Primary_compares_coarse_and_precise_by_level()
    {
        // 手柄 Low(档 1)比 触控板 87%(Full 档)低 → 手柄为主
        var devices = new[] { Magic(DeviceKind.Trackpad, 87), Pad(BatteryLevel.Low) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Gamepad);
    }

    [Fact]
    public void Primary_same_level_prefers_lower_percentage()
    {
        // 触控板 30%(Medium)vs 手柄 Medium:同档,触控板有更低的精确值 → 触控板
        var devices = new[] { Magic(DeviceKind.Trackpad, 30), Pad(BatteryLevel.Medium) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Trackpad);
    }

    [Fact]
    public void Primary_ignores_disconnected()
    {
        var devices = new[] { Dead(DeviceKind.Keyboard), Magic(DeviceKind.Trackpad, 40) };

        BatterySnapshot.Primary(devices).Kind.Should().Be(DeviceKind.Trackpad);
    }

    [Fact]
    public void Primary_of_empty_is_none()
    {
        BatterySnapshot.Primary(Array.Empty<DeviceBattery>()).Should().Be(DeviceBattery.None);
    }
}
