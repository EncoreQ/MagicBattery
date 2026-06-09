using FluentAssertions;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class SwitchProReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_real_fixture_high_not_charging()
    {
        // 真机:byte[2]=0x60 → 高档、未充电
        byte[] report = FixtureLoader.LoadBytes("switch-pro", "pro_bt_high");

        BatteryStatus? status = SwitchProReport.Parse(report, DeviceConnection.Bluetooth, Now);

        status!.Level.Should().Be(BatteryLevel.High);
        status.Percentage.Should().BeNull();        // 手柄无精确百分比
        status.IsCharging.Should().BeFalse();
        status.Connection.Should().Be(DeviceConnection.Bluetooth);
    }

    [Theory]
    [InlineData(0x00, BatteryLevel.Critical, false)] // 档 0
    [InlineData(0x20, BatteryLevel.Low, false)]      // 档 1 (0x20>>5=1)
    [InlineData(0x40, BatteryLevel.Medium, false)]   // 档 2
    [InlineData(0x60, BatteryLevel.High, false)]     // 档 3
    [InlineData(0x80, BatteryLevel.Full, false)]     // 档 4
    [InlineData(0x90, BatteryLevel.Full, true)]      // 档 4 + 充电(bit4)
    [InlineData(0x50, BatteryLevel.Medium, true)]    // 档 2 + 充电
    public void Parse_decodes_level_and_charging(byte batCon, BatteryLevel level, bool charging)
    {
        byte[] report = { 0x30, 0x00, batCon };

        BatteryStatus? status = SwitchProReport.Parse(report, DeviceConnection.Bluetooth, Now);

        status!.Level.Should().Be(level);
        status.IsCharging.Should().Be(charging);
    }

    [Fact]
    public void Parse_wrong_report_id_returns_null()
    {
        byte[] report = { 0x3F, 0x00, 0x60 }; // 基础模式报文,无电量
        SwitchProReport.Parse(report, DeviceConnection.Bluetooth, Now).Should().BeNull();
    }

    [Fact]
    public void Parse_too_short_returns_null()
    {
        byte[] report = { 0x30, 0x00 };
        SwitchProReport.Parse(report, DeviceConnection.Bluetooth, Now).Should().BeNull();
    }
}
