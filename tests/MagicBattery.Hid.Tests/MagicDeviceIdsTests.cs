using FluentAssertions;
using Xunit;

namespace MagicBattery.Hid.Tests;

public class MagicDeviceIdsTests
{
    [Theory]
    [InlineData(MagicDeviceIds.MagicTrackpad2, DeviceKind.Trackpad)]
    [InlineData(MagicDeviceIds.MagicTrackpad2Usbc, DeviceKind.Trackpad)]
    [InlineData(MagicDeviceIds.MagicKeyboard2021, DeviceKind.Keyboard)]
    [InlineData(MagicDeviceIds.MagicKeyboard2015, DeviceKind.Keyboard)]
    [InlineData(MagicDeviceIds.MagicKeyboard2024, DeviceKind.Keyboard)]
    [InlineData(MagicDeviceIds.MagicMouse2, DeviceKind.Mouse)]
    public void KindFor_classifies_pid(int pid, DeviceKind expected)
    {
        MagicDeviceIds.KindFor(pid).Should().Be(expected);
    }

    [Theory]
    [InlineData(MagicDeviceIds.MagicTrackpad2)]
    [InlineData(MagicDeviceIds.MagicKeyboard2021)]   // MK 已纳入(旁证同构)
    [InlineData(MagicDeviceIds.MagicKeyboard2024)]
    public void IsMagicBatteryDevice_accepts_supported_pids(int pid)
    {
        MagicDeviceIds.IsMagicBatteryDevice(MagicDeviceIds.VendorBt, pid).Should().BeTrue();
        MagicDeviceIds.IsMagicBatteryDevice(MagicDeviceIds.VendorUsb, pid).Should().BeTrue();
    }

    [Theory]
    [InlineData(MagicDeviceIds.MagicMouse2)]         // MM2 Windows 未验证,暂不纳入
    [InlineData(MagicDeviceIds.MagicMouse2Usbc)]
    public void IsMagicBatteryDevice_rejects_mouse_pids(int pid)
    {
        MagicDeviceIds.IsMagicBatteryDevice(MagicDeviceIds.VendorBt, pid).Should().BeFalse();
    }

    [Fact]
    public void IsMagicBatteryDevice_rejects_foreign_vendor()
    {
        MagicDeviceIds.IsMagicBatteryDevice(0x046D, MagicDeviceIds.MagicTrackpad2).Should().BeFalse();
    }
}
