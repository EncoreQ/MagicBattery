using FluentAssertions;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class DeviceKindNamesTests
{
    [Theory]
    [InlineData(DeviceKind.Trackpad, "触控板")]
    [InlineData(DeviceKind.Keyboard, "键盘")]
    [InlineData(DeviceKind.Mouse, "鼠标")]
    [InlineData(DeviceKind.Unknown, "设备")]
    public void Of_maps_kind_to_chinese_name(DeviceKind kind, string expected)
    {
        DeviceKindNames.Of(kind).Should().Be(expected);
    }
}
