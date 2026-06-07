using System.IO;
using FluentAssertions;
using MagicBattery.Hid.Tests.Fakes;
using MagicBattery.Hid.Usb;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class UsbBatteryReaderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly UsbBatteryReportLayout Mt2 =
        UsbBatteryReportLayout.MagicTrackpad2Synthetic;

    private static UsbBatteryReader ReaderReturning(string fixture, out FakeUsbHidConnection fake)
    {
        fake = FakeUsbHidConnection.Returning(FixtureLoader.LoadBytes("usb", fixture));
        return new UsbBatteryReader(fake, Mt2, () => Now);
    }

    [Fact]
    public async Task First_read_returns_Updated_then_same_value_is_Unchanged()
    {
        using UsbBatteryReader reader = ReaderReturning("mt2_50pct", out _);

        BatteryReadResult first = await reader.ReadAsync(CancellationToken.None);
        BatteryReadResult second = await reader.ReadAsync(CancellationToken.None);

        first.Outcome.Should().Be(BatteryReadOutcome.Updated);
        first.Status!.Percentage.Should().Be(50);
        second.Outcome.Should().Be(BatteryReadOutcome.Unchanged);
        second.Status.Should().BeNull();
    }

    [Fact]
    public async Task Io_failure_returns_Unavailable()
    {
        var fake = FakeUsbHidConnection.Throwing(new IOException("设备未就绪"));
        using var reader = new UsbBatteryReader(fake, Mt2, () => Now);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public async Task Garbage_report_returns_Unavailable()
    {
        using UsbBatteryReader reader = ReaderReturning("mt2_oob", out _);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public async Task Updated_pushes_one_value_to_Changes()
    {
        using UsbBatteryReader reader = ReaderReturning("mt2_50pct", out _);
        var collector = new CollectingObserver<BatteryStatus>();
        using IDisposable sub = reader.Changes.Subscribe(collector);

        await reader.ReadAsync(CancellationToken.None);

        collector.Values.Should().ContainSingle().Which.Percentage.Should().Be(50);
    }

    [Fact]
    public void Dispose_disposes_underlying_connection()
    {
        UsbBatteryReader reader = ReaderReturning("mt2_50pct", out FakeUsbHidConnection fake);

        reader.Dispose();

        fake.Disposed.Should().BeTrue();
    }
}
