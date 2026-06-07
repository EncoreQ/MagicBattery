using FluentAssertions;
using MagicBattery.Hid.Ble;
using MagicBattery.Hid.Tests.Fakes;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class BleBatteryReaderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Read_returns_level_as_percentage_not_charging_bluetooth()
    {
        var gatt = new FakeBleBatteryGatt { Level = 77 };
        using var reader = new BleBatteryReader(gatt, () => Now);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Updated);
        result.Status!.Percentage.Should().Be(77);
        result.Status.IsCharging.Should().BeFalse();
        result.Status.Connection.Should().Be(DeviceConnection.Bluetooth);
    }

    [Fact]
    public async Task Same_level_twice_is_Unchanged()
    {
        var gatt = new FakeBleBatteryGatt { Level = 77 };
        using var reader = new BleBatteryReader(gatt, () => Now);

        await reader.ReadAsync(CancellationToken.None);
        BatteryReadResult second = await reader.ReadAsync(CancellationToken.None);

        second.Outcome.Should().Be(BatteryReadOutcome.Unchanged);
    }

    [Fact]
    public async Task Unreadable_level_returns_Unavailable()
    {
        var gatt = new FakeBleBatteryGatt { Level = null };
        using var reader = new BleBatteryReader(gatt, () => Now);

        BatteryReadResult result = await reader.ReadAsync(CancellationToken.None);

        result.Outcome.Should().Be(BatteryReadOutcome.Unavailable);
    }

    [Fact]
    public async Task Notify_pushes_to_Changes_and_dedupes_later_read()
    {
        var gatt = new FakeBleBatteryGatt { Level = 60 };
        using var reader = new BleBatteryReader(gatt, () => Now);
        var collector = new CollectingObserver<BatteryStatus>();
        using IDisposable _ = reader.Changes.Subscribe(collector);

        gatt.PushNotification(60);                                 // 设备主动推送
        BatteryReadResult read = await reader.ReadAsync(CancellationToken.None);

        collector.Values.Should().ContainSingle().Which.Percentage.Should().Be(60);
        read.Outcome.Should().Be(BatteryReadOutcome.Unchanged);    // notify 已更新过,主动读不再重复
    }
}
