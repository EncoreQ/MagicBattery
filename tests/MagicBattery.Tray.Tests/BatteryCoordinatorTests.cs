using System.IO;
using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;
using MagicBattery.Tray.Tests.Fakes;

namespace MagicBattery.Tray.Tests;

public class BatteryCoordinatorTests
{
    private DateTimeOffset _now = new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private BatteryCoordinator Coordinator(Func<IReadOnlyList<IBatteryReader>> openAll) =>
        new(openAll, TimeSpan.FromMinutes(15), () => _now);

    private static BatteryReadResult Updated(int pct, bool charging, DeviceConnection conn) =>
        BatteryReadResult.Updated(new BatteryStatus(pct, charging, conn, default));

    private static FakeBatteryReader Reader(DeviceKind kind, string key, DeviceConnection conn,
        params object[] script) =>
        new(conn, script) { Kind = kind, DeviceKey = key };

    [Fact]
    public async Task Two_devices_produce_two_snapshots()
    {
        var trackpad = Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Bluetooth,
            Updated(87, false, DeviceConnection.Bluetooth));
        var keyboard = Reader(DeviceKind.Keyboard, "kb", DeviceConnection.Bluetooth,
            Updated(8, true, DeviceConnection.Bluetooth));
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[] { trackpad, keyboard });

        await coord.PollOnceAsync(CancellationToken.None);

        coord.Devices.Should().HaveCount(2);
        coord.Devices.Should().Contain(d => d.Kind == DeviceKind.Trackpad && d.Percentage == 87);
        coord.Devices.Should().Contain(d => d.Kind == DeviceKind.Keyboard && d.Percentage == 8);
    }

    [Fact]
    public async Task Primary_is_lowest_battery_device()
    {
        var trackpad = Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Bluetooth,
            Updated(87, false, DeviceConnection.Bluetooth));
        var keyboard = Reader(DeviceKind.Keyboard, "kb", DeviceConnection.Bluetooth,
            Updated(8, true, DeviceConnection.Bluetooth));
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[] { trackpad, keyboard });

        await coord.PollOnceAsync(CancellationToken.None);

        DeviceBattery primary = BatterySnapshot.Primary(coord.Devices);
        primary.Kind.Should().Be(DeviceKind.Keyboard);
        primary.Percentage.Should().Be(8);
    }

    [Fact]
    public async Task Device_disappearing_is_removed_next_poll()
    {
        var readers = new List<IBatteryReader>
        {
            Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Bluetooth, Updated(50, false, DeviceConnection.Bluetooth)),
            Reader(DeviceKind.Keyboard, "kb", DeviceConnection.Bluetooth, Updated(60, false, DeviceConnection.Bluetooth)),
        };
        using BatteryCoordinator coord = Coordinator(() => readers.ToList());

        await coord.PollOnceAsync(CancellationToken.None);
        coord.Devices.Should().HaveCount(2);

        // 键盘拔出:下一轮只剩触控板
        readers.RemoveAll(r => r.DeviceKey == "kb");
        // 触控板需要新的可读 reader(上一轮的已被 dispose 且脚本用尽仍可 Peek,但已 Disposed)
        readers.Clear();
        readers.Add(Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Bluetooth, Updated(50, false, DeviceConnection.Bluetooth)));

        await coord.PollOnceAsync(CancellationToken.None);

        coord.Devices.Should().ContainSingle().Which.Kind.Should().Be(DeviceKind.Trackpad);
    }

    [Fact]
    public async Task Unavailable_device_marked_disconnected_without_affecting_others()
    {
        var good = Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Bluetooth,
            Updated(50, false, DeviceConnection.Bluetooth));
        var bad = Reader(DeviceKind.Keyboard, "kb", DeviceConnection.Bluetooth,
            new IOException("设备未就绪"));
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[] { good, bad });

        await coord.PollOnceAsync(CancellationToken.None);

        coord.Devices.Single(d => d.Kind == DeviceKind.Trackpad).Availability
            .Should().Be(BatteryAvailability.Live);
        coord.Devices.Single(d => d.Kind == DeviceKind.Keyboard).Availability
            .Should().Be(BatteryAvailability.Disconnected);
    }

    [Fact]
    public async Task No_devices_yields_empty_snapshot()
    {
        using BatteryCoordinator coord = Coordinator(() => Array.Empty<IBatteryReader>());

        await coord.PollOnceAsync(CancellationToken.None);

        coord.Devices.Should().BeEmpty();
        BatterySnapshot.Primary(coord.Devices).Should().Be(DeviceBattery.None);
    }

    [Fact]
    public async Task SnapshotChanged_fires_with_devices()
    {
        var reader = Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Usb,
            Updated(60, false, DeviceConnection.Usb));
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[] { reader });
        IReadOnlyList<DeviceBattery>? observed = null;
        coord.SnapshotChanged += s => observed = s;

        await coord.PollOnceAsync(CancellationToken.None);

        observed.Should().NotBeNull();
        observed!.Should().ContainSingle().Which.Percentage.Should().Be(60);
    }

    [Fact]
    public async Task Each_reader_is_disposed_after_poll()
    {
        var reader = Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Usb,
            Updated(60, false, DeviceConnection.Usb));
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[] { reader });

        await coord.PollOnceAsync(CancellationToken.None);

        reader.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_performs_immediate_first_poll()
    {
        using BatteryCoordinator coord = Coordinator(() => new IBatteryReader[]
        {
            Reader(DeviceKind.Trackpad, "tp", DeviceConnection.Usb, Updated(77, false, DeviceConnection.Usb)),
        });

        await coord.StartAsync();

        coord.Devices.Should().ContainSingle().Which.Percentage.Should().Be(77);
    }
}
