using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TooltipFormatterTests
{
    private static readonly DateTimeOffset At1432 =
        new(2026, 6, 8, 14, 32, 0, TimeSpan.Zero);

    private static DeviceBattery Magic(DeviceKind kind, int pct, bool charging, DeviceConnection conn) =>
        new(kind.ToString(), kind, BatteryLevels.FromPercentage(pct), pct, charging, conn, At1432,
            BatteryAvailability.Live);

    private static DeviceBattery Pad(BatteryLevel level, bool charging) =>
        new("pad", DeviceKind.Gamepad, level, null, charging, DeviceConnection.Bluetooth, At1432,
            BatteryAvailability.Live);

    private static DeviceBattery Dead(DeviceKind kind) =>
        new(kind.ToString(), kind, BatteryLevel.Critical, null, false, DeviceConnection.Disconnected,
            At1432, BatteryAvailability.Disconnected);

    [Fact]
    public void Device_precise_keyboard_charging()
    {
        TooltipFormatter.Device(Magic(DeviceKind.Keyboard, 8, true, DeviceConnection.Bluetooth))
            .Should().Be("键盘 8% · 充电中 · 蓝牙");
    }

    [Fact]
    public void Device_precise_trackpad_usb()
    {
        TooltipFormatter.Device(Magic(DeviceKind.Trackpad, 87, false, DeviceConnection.Usb))
            .Should().Be("触控板 87% · 未充电 · USB");
    }

    [Fact]
    public void Device_coarse_gamepad_shows_level_name()
    {
        TooltipFormatter.Device(Pad(BatteryLevel.High, charging: false))
            .Should().Be("手柄 高 · 未充电 · 蓝牙");
    }

    [Fact]
    public void Device_disconnected_shows_name_and_unconnected()
    {
        TooltipFormatter.Device(Dead(DeviceKind.Mouse)).Should().Be("鼠标 · 未连接");
    }

    [Fact]
    public void Tooltip_lists_precise_and_coarse_devices()
    {
        var devices = new[]
        {
            Magic(DeviceKind.Trackpad, 87, false, DeviceConnection.Bluetooth),
            Pad(BatteryLevel.High, false),
        };

        TooltipFormatter.Tooltip(devices).Should().Be(
            "触控板 87% · 未充电 · 蓝牙\n手柄 高 · 未充电 · 蓝牙\n更新于 14:32");
    }

    [Fact]
    public void Tooltip_empty_shows_placeholder()
    {
        TooltipFormatter.Tooltip(Array.Empty<DeviceBattery>()).Should().Be("Magic 设备 · 未连接");
    }
}
