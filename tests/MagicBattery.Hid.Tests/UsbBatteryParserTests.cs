using FluentAssertions;
using MagicBattery.Hid.Usb;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class UsbBatteryParserTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly UsbBatteryReportLayout Mt2 =
        UsbBatteryReportLayout.MagicTrackpad2Synthetic;

    [Fact]
    public void Parse_50pct_fixture_returns_50_charging_usb()
    {
        byte[] report = FixtureLoader.LoadBytes("usb", "mt2_50pct");

        BatteryStatus? status = UsbBatteryParser.Parse(report, Mt2, Now);

        status.Should().NotBeNull();
        status!.Percentage.Should().Be(50);
        status.IsCharging.Should().BeTrue();
        status.Connection.Should().Be(DeviceConnection.Usb);
        status.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Parse_full_fixture_returns_100()
    {
        byte[] report = FixtureLoader.LoadBytes("usb", "mt2_full");

        UsbBatteryParser.Parse(report, Mt2, Now)!.Percentage.Should().Be(100);
    }

    [Fact]
    public void Parse_out_of_range_value_returns_null()
    {
        // 0xC8 = 200 > Logical Max 100,属怪值(睡眠/唤醒)
        byte[] report = FixtureLoader.LoadBytes("usb", "mt2_oob");

        UsbBatteryParser.Parse(report, Mt2, Now).Should().BeNull();
    }

    [Fact]
    public void Parse_wrong_report_id_returns_null()
    {
        // 首字节与 layout.ReportId(0x90)不符
        byte[] report = { 0x91, 0x32, 0x00 };

        UsbBatteryParser.Parse(report, Mt2, Now).Should().BeNull();
    }

    [Fact]
    public void Parse_too_short_returns_null()
    {
        byte[] report = { 0x90, 0x32 }; // 少于 layout.ReportLength(3)

        UsbBatteryParser.Parse(report, Mt2, Now).Should().BeNull();
    }

    [Theory]
    [InlineData(0xFF, 100)] // 满程
    [InlineData(0x80, 50)]  // 128/255 ≈ 50.2 → 50
    [InlineData(0x00, 0)]   // 0
    public void Parse_scales_by_logical_min_max(byte raw, int expected)
    {
        // 自定义 layout:Logical 0..255,验证线性换算而非假设 raw==percent
        var layout = new UsbBatteryReportLayout(
            ReportId: 0x01, ReportLength: 2, BatteryByteOffset: 1, LogicalMin: 0, LogicalMax: 255);
        byte[] report = { 0x01, raw };

        UsbBatteryParser.Parse(report, layout, Now)!.Percentage.Should().Be(expected);
    }
}
