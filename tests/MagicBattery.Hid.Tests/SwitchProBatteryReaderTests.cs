using System.IO;
using FluentAssertions;
using MagicBattery.Hid.Tests.Fakes;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class SwitchProBatteryReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reads_level_and_identity()
    {
        var fake = FakeSwitchReportSource.Returning(new byte[] { 0x30, 0x00, 0x60 });
        using var reader = new SwitchProBatteryReader(fake, () => Now);

        reader.Kind.Should().Be(DeviceKind.Gamepad);
        reader.Connection.Should().Be(DeviceConnection.Bluetooth);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Updated);
        result.Status!.Level.Should().Be(BatteryLevel.High);
        result.Status.Percentage.Should().BeNull();
    }

    [Fact]
    public async Task Same_level_is_Unchanged()
    {
        var fake = FakeSwitchReportSource.Returning(new byte[] { 0x30, 0x00, 0x60 });
        using var reader = new SwitchProBatteryReader(fake, () => Now);

        (await reader.ReadAsync(CancellationToken.None)).Outcome.Should().Be(BatteryReadOutcome.Updated);
        (await reader.ReadAsync(CancellationToken.None)).Outcome.Should().Be(BatteryReadOutcome.Unchanged);
    }

    [Fact]
    public async Task Io_failure_returns_Unavailable()
    {
        var fake = FakeSwitchReportSource.Throwing(new IOException("基础模式无电量"));
        using var reader = new SwitchProBatteryReader(fake, () => Now);

        (await reader.ReadAsync(CancellationToken.None)).Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public void Dispose_propagates_to_source()
    {
        var fake = FakeSwitchReportSource.Returning(new byte[] { 0x30, 0x00, 0x60 });
        var reader = new SwitchProBatteryReader(fake, () => Now);

        reader.Dispose();

        fake.Disposed.Should().BeTrue();
    }
}
