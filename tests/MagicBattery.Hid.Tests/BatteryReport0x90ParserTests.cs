using FluentAssertions;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class BatteryReport0x90ParserTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_real_bluetooth_fixture_2pct_not_charging()
    {
        // 真机录制:蓝牙拔线 2% → 90 00 02
        byte[] report = FixtureLoader.LoadBytes("report-0x90", "mt2_bt_2pct");

        BatteryStatus? status = BatteryReport0x90.Parse(report, DeviceConnection.Bluetooth, Now);

        status.Should().NotBeNull();
        status!.Percentage.Should().Be(2);
        status.IsCharging.Should().BeFalse();      // byte[1]=0x00
        status.Connection.Should().Be(DeviceConnection.Bluetooth);
        status.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Parse_real_usb_fixture_3pct_charging()
    {
        // 真机录制:USB 充电 3% → 90 03 03
        byte[] report = FixtureLoader.LoadBytes("report-0x90", "mt2_usb_charging_3pct");

        BatteryStatus? status = BatteryReport0x90.Parse(report, DeviceConnection.Usb, Now);

        status!.Percentage.Should().Be(3);
        status.IsCharging.Should().BeTrue();        // byte[1]=0x03
        status.Connection.Should().Be(DeviceConnection.Usb);
    }

    [Fact]
    public void Parse_real_bluetooth_full_charging_fixture_100pct()
    {
        // 真机录制:充电中经蓝牙接口读到满电 → 90 03 64
        // 关键点:byte[2]=0x64=100 高值直读;byte[1]=0x03 充电标志即便走蓝牙也成立
        byte[] report = FixtureLoader.LoadBytes("report-0x90", "mt2_bt_full_charging_100pct");

        BatteryStatus? status = BatteryReport0x90.Parse(report, DeviceConnection.Bluetooth, Now);

        status!.Percentage.Should().Be(100);
        status.IsCharging.Should().BeTrue();        // byte[1]=0x03,与蓝牙连接并存
        status.Connection.Should().Be(DeviceConnection.Bluetooth);
    }

    [Fact]
    public void Parse_real_bluetooth_full_unplugged_fixture_100pct_not_charging()
    {
        // 真机录制:从满电拔线后读到 → 90 00 64
        // 与 90 03 64 仅差 byte[1]:拔线后 0x03→0x00,坐实 byte[1] 即充电标志
        byte[] report = FixtureLoader.LoadBytes("report-0x90", "mt2_bt_full_unplugged_100pct");

        BatteryStatus? status = BatteryReport0x90.Parse(report, DeviceConnection.Bluetooth, Now);

        status!.Percentage.Should().Be(100);
        status.IsCharging.Should().BeFalse();       // byte[1]=0x00,拔线
        status.Connection.Should().Be(DeviceConnection.Bluetooth);
    }

    [Fact]
    public void Parse_out_of_range_battery_returns_null()
    {
        // 90 00 C8:byte[2]=200 > 100,怪值
        byte[] report = FixtureLoader.LoadBytes("report-0x90", "mt2_garbage_oob");

        BatteryReport0x90.Parse(report, DeviceConnection.Bluetooth, Now).Should().BeNull();
    }

    [Fact]
    public void Parse_wrong_report_id_returns_null()
    {
        byte[] report = { 0x91, 0x00, 0x02 };

        BatteryReport0x90.Parse(report, DeviceConnection.Usb, Now).Should().BeNull();
    }

    [Fact]
    public void Parse_too_short_returns_null()
    {
        byte[] report = { 0x90, 0x00 };

        BatteryReport0x90.Parse(report, DeviceConnection.Usb, Now).Should().BeNull();
    }
}
