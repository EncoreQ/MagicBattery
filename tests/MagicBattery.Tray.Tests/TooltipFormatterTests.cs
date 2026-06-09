using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TooltipFormatterTests
{
    private static readonly DateTimeOffset At1432 =
        new(2026, 6, 8, 14, 32, 0, TimeSpan.Zero);

    private static DeviceBattery Dev(DeviceKind kind, int? pct, bool charging,
        DeviceConnection conn, BatteryAvailability avail = BatteryAvailability.Live) =>
        new(kind.ToString(), kind, pct, charging, conn, At1432, avail);

    [Fact]
    public void Device_keyboard_charging()
    {
        TooltipFormatter.Device(Dev(DeviceKind.Keyboard, 8, true, DeviceConnection.Bluetooth))
            .Should().Be("键盘 8% · 充电中 · 蓝牙");
    }

    [Fact]
    public void Device_trackpad_usb_not_charging()
    {
        TooltipFormatter.Device(Dev(DeviceKind.Trackpad, 87, false, DeviceConnection.Usb))
            .Should().Be("触控板 87% · 未充电 · USB");
    }

    [Fact]
    public void Device_disconnected_shows_name_and_unconnected()
    {
        TooltipFormatter.Device(Dev(DeviceKind.Mouse, null, false, DeviceConnection.Disconnected,
            BatteryAvailability.Disconnected)).Should().Be("鼠标 · 未连接");
    }

    [Fact]
    public void Tooltip_lists_live_devices_with_update_time()
    {
        var devices = new[]
        {
            Dev(DeviceKind.Trackpad, 87, false, DeviceConnection.Bluetooth),
            Dev(DeviceKind.Keyboard, 8, true, DeviceConnection.Bluetooth),
        };

        TooltipFormatter.Tooltip(devices).Should().Be(
            "触控板 87% · 未充电 · 蓝牙\n键盘 8% · 充电中 · 蓝牙\n更新 14:32");
    }

    [Fact]
    public void Tooltip_empty_shows_placeholder()
    {
        TooltipFormatter.Tooltip(Array.Empty<DeviceBattery>()).Should().Be("Magic 设备 · 未连接");
    }
}
