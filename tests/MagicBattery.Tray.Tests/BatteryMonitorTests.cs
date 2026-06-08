using System.IO;
using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;
using MagicBattery.Tray.Tests.Fakes;

namespace MagicBattery.Tray.Tests;

public class BatteryMonitorTests
{
    private DateTimeOffset _now = new(2026, 6, 8, 14, 0, 0, TimeSpan.Zero);

    private BatteryMonitor Monitor(Func<IBatteryReader?> factory) =>
        new(factory, TimeSpan.FromMinutes(15), () => _now);

    // 工厂:依次返回给定 readers,用尽后返回 null
    private static Func<IBatteryReader?> Sequence(params IBatteryReader?[] readers)
    {
        var queue = new Queue<IBatteryReader?>(readers);
        return () => queue.Count > 0 ? queue.Dequeue() : null;
    }

    private static BatteryReadResult Updated(int pct, bool charging, DeviceConnection conn) =>
        BatteryReadResult.Updated(new BatteryStatus(pct, charging, conn, default));

    [Fact]
    public async Task Updated_sets_live_state_with_value()
    {
        var reader = new FakeBatteryReader(DeviceConnection.Bluetooth,
            Updated(50, false, DeviceConnection.Bluetooth));
        using BatteryMonitor monitor = Monitor(Sequence(reader));

        await monitor.PollOnceAsync(CancellationToken.None);

        monitor.State.Percentage.Should().Be(50);
        monitor.State.Connection.Should().Be(DeviceConnection.Bluetooth);
        monitor.State.Availability.Should().Be(BatteryAvailability.Live);
        monitor.State.LastUpdate.Should().Be(_now);
    }

    [Fact]
    public async Task Unchanged_keeps_value_and_refreshes_time()
    {
        var reader = new FakeBatteryReader(DeviceConnection.Usb,
            Updated(80, true, DeviceConnection.Usb),
            BatteryReadResult.Unchanged);
        using BatteryMonitor monitor = Monitor(Sequence(reader));

        await monitor.PollOnceAsync(CancellationToken.None);
        _now = _now.AddMinutes(15);
        await monitor.PollOnceAsync(CancellationToken.None);

        monitor.State.Percentage.Should().Be(80);     // 保留
        monitor.State.IsCharging.Should().BeTrue();    // 保留
        monitor.State.Availability.Should().Be(BatteryAvailability.Live);
        monitor.State.LastUpdate.Should().Be(_now);    // 刷新
    }

    [Fact]
    public async Task Unavailable_recreates_reader_and_reads_new_connection()
    {
        // 模拟 USB 拔出:首个 reader 失效,重建后选到蓝牙
        var stale = new FakeBatteryReader(DeviceConnection.Usb, BatteryReadResult.Unavailable);
        var fresh = new FakeBatteryReader(DeviceConnection.Bluetooth,
            Updated(42, false, DeviceConnection.Bluetooth));
        using BatteryMonitor monitor = Monitor(Sequence(stale, fresh));

        await monitor.PollOnceAsync(CancellationToken.None);

        stale.Disposed.Should().BeTrue();
        monitor.State.Percentage.Should().Be(42);
        monitor.State.Connection.Should().Be(DeviceConnection.Bluetooth);
        monitor.State.Availability.Should().Be(BatteryAvailability.Live);
    }

    [Fact]
    public async Task Read_exception_is_treated_as_unavailable_then_recovers()
    {
        var throwing = new FakeBatteryReader(DeviceConnection.Usb, new IOException("设备未就绪"));
        var fresh = new FakeBatteryReader(DeviceConnection.Bluetooth,
            Updated(33, false, DeviceConnection.Bluetooth));
        using BatteryMonitor monitor = Monitor(Sequence(throwing, fresh));

        await monitor.PollOnceAsync(CancellationToken.None);

        monitor.State.Percentage.Should().Be(33);
        monitor.State.Availability.Should().Be(BatteryAvailability.Live);
    }

    [Fact]
    public async Task No_device_sets_disconnected()
    {
        using BatteryMonitor monitor = Monitor(Sequence()); // 工厂始终 null

        await monitor.PollOnceAsync(CancellationToken.None);

        monitor.State.Availability.Should().Be(BatteryAvailability.Disconnected);
    }

    [Fact]
    public async Task Persistent_unavailable_sets_disconnected()
    {
        var a = new FakeBatteryReader(DeviceConnection.Usb, BatteryReadResult.Unavailable);
        var b = new FakeBatteryReader(DeviceConnection.Bluetooth, BatteryReadResult.Unavailable);
        using BatteryMonitor monitor = Monitor(Sequence(a, b));

        await monitor.PollOnceAsync(CancellationToken.None);

        monitor.State.Availability.Should().Be(BatteryAvailability.Disconnected);
    }

    [Fact]
    public async Task StateChanged_fires_with_new_state()
    {
        var reader = new FakeBatteryReader(DeviceConnection.Usb,
            Updated(60, false, DeviceConnection.Usb));
        using BatteryMonitor monitor = Monitor(Sequence(reader));
        BatteryViewState? observed = null;
        monitor.StateChanged += s => observed = s;

        await monitor.PollOnceAsync(CancellationToken.None);

        observed.Should().NotBeNull();
        observed!.Percentage.Should().Be(60);
    }

    [Fact]
    public async Task StartAsync_performs_immediate_first_poll()
    {
        var reader = new FakeBatteryReader(DeviceConnection.Usb,
            Updated(77, false, DeviceConnection.Usb));
        using BatteryMonitor monitor = Monitor(Sequence(reader));

        await monitor.StartAsync();

        monitor.State.Percentage.Should().Be(77);
    }
}
