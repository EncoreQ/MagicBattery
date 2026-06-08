using FluentAssertions;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class BatteryTierTests
{
    // CLAUDE.md:>75 / 50 / 25 / 10 / <10 五档,逐个边界验证
    [Theory]
    [InlineData(100, BatteryTier.Full)]
    [InlineData(76, BatteryTier.Full)]
    [InlineData(75, BatteryTier.High)]
    [InlineData(51, BatteryTier.High)]
    [InlineData(50, BatteryTier.Medium)]
    [InlineData(26, BatteryTier.Medium)]
    [InlineData(25, BatteryTier.Low)]
    [InlineData(11, BatteryTier.Low)]
    [InlineData(10, BatteryTier.Critical)]
    [InlineData(9, BatteryTier.Critical)]
    [InlineData(0, BatteryTier.Critical)]
    public void FromPercentage_maps_to_expected_tier(int percentage, BatteryTier expected)
    {
        BatteryTierMap.FromPercentage(percentage).Should().Be(expected);
    }

    [Fact]
    public void Each_tier_has_a_distinct_color()
    {
        var colors = new[]
        {
            BatteryTier.Critical, BatteryTier.Low, BatteryTier.Medium, BatteryTier.High, BatteryTier.Full,
        }.Select(BatteryTierMap.ColorFor).ToList();

        colors.Distinct().Should().HaveCount(5);
    }
}
