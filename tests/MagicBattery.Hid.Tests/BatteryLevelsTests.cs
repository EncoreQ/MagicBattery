using FluentAssertions;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class BatteryLevelsTests
{
    [Theory]
    [InlineData(100, BatteryLevel.Full)]
    [InlineData(76, BatteryLevel.Full)]
    [InlineData(75, BatteryLevel.High)]
    [InlineData(51, BatteryLevel.High)]
    [InlineData(50, BatteryLevel.Medium)]
    [InlineData(26, BatteryLevel.Medium)]
    [InlineData(25, BatteryLevel.Low)]
    [InlineData(11, BatteryLevel.Low)]
    [InlineData(10, BatteryLevel.Critical)]
    [InlineData(0, BatteryLevel.Critical)]
    public void FromPercentage_maps_boundaries(int pct, BatteryLevel expected)
    {
        BatteryLevels.FromPercentage(pct).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, BatteryLevel.Critical)]
    [InlineData(1, BatteryLevel.Low)]
    [InlineData(2, BatteryLevel.Medium)]
    [InlineData(3, BatteryLevel.High)]
    [InlineData(4, BatteryLevel.Full)]
    public void FromSwitchRaw_maps_0_to_4(int raw, BatteryLevel expected)
    {
        BatteryLevels.FromSwitchRaw(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, BatteryLevel.Critical)]
    [InlineData(7, BatteryLevel.Full)]
    public void FromSwitchRaw_clamps_out_of_range(int raw, BatteryLevel expected)
    {
        BatteryLevels.FromSwitchRaw(raw).Should().Be(expected);
    }

    [Fact]
    public void Levels_ascend_by_severity()
    {
        ((int)BatteryLevel.Critical).Should().BeLessThan((int)BatteryLevel.Low);
        ((int)BatteryLevel.High).Should().BeLessThan((int)BatteryLevel.Full);
    }
}
