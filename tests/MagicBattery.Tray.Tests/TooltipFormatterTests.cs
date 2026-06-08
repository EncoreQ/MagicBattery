using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class TooltipFormatterTests
{
    private static readonly DateTimeOffset At1432 =
        new(2026, 6, 8, 14, 32, 0, TimeSpan.Zero);

    [Fact]
    public void Live_bluetooth_charging()
    {
        var state = new BatteryViewState(8, true, DeviceConnection.Bluetooth, At1432, BatteryAvailability.Live);

        TooltipFormatter.Format(state).Should().Be("8% · 充电中 · 蓝牙 · 14:32");
    }

    [Fact]
    public void Live_usb_not_charging()
    {
        var state = new BatteryViewState(87, false, DeviceConnection.Usb, At1432, BatteryAvailability.Live);

        TooltipFormatter.Format(state).Should().Be("87% · 未充电 · USB · 14:32");
    }

    [Fact]
    public void Disconnected_with_history_shows_last_time()
    {
        var state = new BatteryViewState(50, false, DeviceConnection.Bluetooth, At1432, BatteryAvailability.Disconnected);

        TooltipFormatter.Format(state).Should().Be("未连接 · 最后 14:32");
    }

    [Fact]
    public void Disconnected_without_history_shows_placeholder()
    {
        TooltipFormatter.Format(BatteryViewState.Initial).Should().Be("Magic Trackpad 2 · 未连接");
    }
}
