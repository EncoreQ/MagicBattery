using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class LocalizerTests
{
    private static readonly DateTimeOffset At1432 = new(2026, 6, 8, 14, 32, 0, TimeSpan.Zero);

    private static readonly Localizer Zh = Localizer.For(AppLanguage.Chinese);
    private static readonly Localizer En = Localizer.For(AppLanguage.English);

    private static DeviceBattery Magic(DeviceKind kind, int pct, bool charging, DeviceConnection conn) =>
        new(kind.ToString(), kind, BatteryLevels.FromPercentage(pct), pct, charging, conn, At1432,
            BatteryAvailability.Live);

    private static DeviceBattery Pad(BatteryLevel level, bool charging) =>
        new("pad", DeviceKind.Gamepad, level, null, charging, DeviceConnection.Bluetooth, At1432,
            BatteryAvailability.Live);

    private static DeviceBattery Dead(DeviceKind kind) =>
        new(kind.ToString(), kind, BatteryLevel.Critical, null, false, DeviceConnection.Disconnected,
            At1432, BatteryAvailability.Disconnected);

    [Theory]
    [InlineData(DeviceKind.Trackpad, "触控板", "Trackpad")]
    [InlineData(DeviceKind.Keyboard, "键盘", "Keyboard")]
    [InlineData(DeviceKind.Gamepad, "手柄", "Gamepad")]
    public void DeviceName_localized(DeviceKind kind, string zh, string en)
    {
        Zh.DeviceName(kind).Should().Be(zh);
        En.DeviceName(kind).Should().Be(en);
    }

    [Theory]
    [InlineData(BatteryLevel.Full, "满", "Full")]
    [InlineData(BatteryLevel.High, "高", "High")]
    [InlineData(BatteryLevel.Critical, "危", "Critical")]
    public void LevelName_localized(BatteryLevel level, string zh, string en)
    {
        Zh.LevelName(level).Should().Be(zh);
        En.LevelName(level).Should().Be(en);
    }

    [Fact]
    public void Device_precise_chinese()
    {
        Zh.Device(Magic(DeviceKind.Trackpad, 87, false, DeviceConnection.Usb))
            .Should().Be("触控板 87% · 未充电 · USB");
    }

    [Fact]
    public void Device_precise_english()
    {
        En.Device(Magic(DeviceKind.Keyboard, 8, true, DeviceConnection.Bluetooth))
            .Should().Be("Keyboard 8% · charging · Bluetooth");
    }

    [Fact]
    public void Device_coarse_gamepad_both_languages()
    {
        Zh.Device(Pad(BatteryLevel.High, false)).Should().Be("手柄 高 · 未充电 · 蓝牙");
        En.Device(Pad(BatteryLevel.High, false)).Should().Be("Gamepad High · on battery · Bluetooth");
    }

    [Fact]
    public void Device_disconnected_both_languages()
    {
        Zh.Device(Dead(DeviceKind.Mouse)).Should().Be("鼠标 · 未连接");
        En.Device(Dead(DeviceKind.Mouse)).Should().Be("Mouse · disconnected");
    }

    [Fact]
    public void Tooltip_lists_devices_compactly_with_time()
    {
        // tooltip 受 Shell 128 字符上限,只放精简「名称 电量」(完整信息在菜单)
        var devices = new[]
        {
            Magic(DeviceKind.Trackpad, 87, false, DeviceConnection.Bluetooth),
            Pad(BatteryLevel.High, false),
        };

        Zh.Tooltip(devices).Should().Be("触控板 87%\n手柄 高\n更新于 14:32");
        En.Tooltip(devices).Should().Be("Trackpad 87%\nGamepad High\nUpdated 14:32");
    }

    [Fact]
    public void Tooltip_stays_well_under_shell_limit_with_four_devices()
    {
        var devices = new[]
        {
            Magic(DeviceKind.Trackpad, 100, true, DeviceConnection.Bluetooth),
            Magic(DeviceKind.Keyboard, 100, true, DeviceConnection.Bluetooth),
            Magic(DeviceKind.Mouse, 100, true, DeviceConnection.Bluetooth),
            Pad(BatteryLevel.Critical, true),
        };

        En.Tooltip(devices).Length.Should().BeLessThan(120); // Shell szTip 上限 128
    }

    [Fact]
    public void Tooltip_empty_placeholder()
    {
        Zh.Tooltip(Array.Empty<DeviceBattery>()).Should().Be("Magic 设备 · 未连接");
        En.Tooltip(Array.Empty<DeviceBattery>()).Should().Be("MagicBattery · no device");
    }

    [Fact]
    public void AlertBody_precise_and_coarse()
    {
        Zh.AlertBody(DeviceKind.Keyboard, BatteryLevel.Critical, 8).Should().Be("键盘电量 8%,请及时充电");
        En.AlertBody(DeviceKind.Keyboard, BatteryLevel.Critical, 8).Should().Be("Keyboard battery 8% — please charge");

        Zh.AlertBody(DeviceKind.Gamepad, BatteryLevel.Low, null).Should().Be("手柄电量 低档,请及时充电");
        En.AlertBody(DeviceKind.Gamepad, BatteryLevel.Low, null).Should().Be("Gamepad battery Low — please charge");
    }

    [Fact]
    public void Menu_labels_localized()
    {
        Zh.MenuRefresh.Should().Be("立即刷新");
        En.MenuRefresh.Should().Be("Refresh now");
        Zh.MenuLanguage.Should().Be("语言");
        En.MenuLanguage.Should().Be("Language");
    }
}
