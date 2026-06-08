using System.IO;
using FluentAssertions;
using MagicBattery.Hid.Tests.Fakes;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class MagicBatteryReaderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    private static MagicBatteryReader ReaderReturning(string fixture, DeviceConnection conn,
        out FakeHidInputReportSource fake)
    {
        fake = FakeHidInputReportSource.Returning(
            FixtureLoader.LoadBytes("report-0x90", fixture), conn);
        return new MagicBatteryReader(fake, () => Now);
    }

    [Fact]
    public async Task First_read_Updated_then_same_value_Unchanged()
    {
        using MagicBatteryReader reader =
            ReaderReturning("mt2_bt_2pct", DeviceConnection.Bluetooth, out _);

        BatteryReadResult first = await reader.ReadAsync(CancellationToken.None);
        BatteryReadResult second = await reader.ReadAsync(CancellationToken.None);

        first.Outcome.Should().Be(BatteryReadOutcome.Updated);
        first.Status!.Percentage.Should().Be(2);
        first.Status.Connection.Should().Be(DeviceConnection.Bluetooth);
        second.Outcome.Should().Be(BatteryReadOutcome.Unchanged);
        second.Status.Should().BeNull();
    }

    [Fact]
    public async Task Charging_flip_at_same_percentage_is_Updated()
    {
        // 满电下拔电:电量不变(100),仅充电标志 0x03→0x00,应判为 Updated 以便托盘刷新充电指示
        var fake = FakeHidInputReportSource.ReturningSequence(DeviceConnection.Bluetooth,
            new byte[] { 0x90, 0x03, 0x64 },   // 充电中
            new byte[] { 0x90, 0x00, 0x64 });  // 拔电
        using var reader = new MagicBatteryReader(fake, () => Now);

        BatteryReadResult first = await reader.ReadAsync(CancellationToken.None);
        BatteryReadResult second = await reader.ReadAsync(CancellationToken.None);

        first.Outcome.Should().Be(BatteryReadOutcome.Updated);
        first.Status!.IsCharging.Should().BeTrue();
        second.Outcome.Should().Be(BatteryReadOutcome.Updated);
        second.Status!.Percentage.Should().Be(100);
        second.Status.IsCharging.Should().BeFalse();
    }

    [Fact]
    public async Task Io_failure_returns_Unavailable()
    {
        var fake = FakeHidInputReportSource.Throwing(new IOException("设备未就绪"));
        using var reader = new MagicBatteryReader(fake, () => Now);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public async Task Garbage_report_returns_Unavailable()
    {
        using MagicBatteryReader reader =
            ReaderReturning("mt2_garbage_oob", DeviceConnection.Bluetooth, out _);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public async Task Updated_pushes_one_value_to_Changes()
    {
        using MagicBatteryReader reader =
            ReaderReturning("mt2_usb_charging_3pct", DeviceConnection.Usb, out _);
        var collector = new CollectingObserver<BatteryStatus>();
        using IDisposable sub = reader.Changes.Subscribe(collector);

        await reader.ReadAsync(CancellationToken.None);

        collector.Values.Should().ContainSingle()
            .Which.IsCharging.Should().BeTrue();
    }

    [Fact]
    public void Dispose_disposes_underlying_source()
    {
        MagicBatteryReader reader =
            ReaderReturning("mt2_bt_2pct", DeviceConnection.Bluetooth, out FakeHidInputReportSource fake);

        reader.Dispose();

        fake.Disposed.Should().BeTrue();
    }
}
