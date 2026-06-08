using FluentAssertions;
using MagicBattery.Tray.Core;
using MagicBattery.Tray.Tests.Fakes;

namespace MagicBattery.Tray.Tests;

public class AutostartServiceTests
{
    private static AutostartService Make(FakeAutostartStore store) =>
        new(store, exePath: () => @"C:\Tools\Magic Battery\MagicBattery.exe");

    [Fact]
    public void Enable_writes_quoted_exe_path_under_value_name()
    {
        var store = new FakeAutostartStore();
        AutostartService service = Make(store);

        service.Enable();

        service.IsEnabled.Should().BeTrue();
        store.Get(AutostartService.ValueName).Should().Be("\"C:\\Tools\\Magic Battery\\MagicBattery.exe\"");
    }

    [Fact]
    public void Disable_removes_value()
    {
        var store = new FakeAutostartStore();
        AutostartService service = Make(store);
        service.Enable();

        service.Disable();

        service.IsEnabled.Should().BeFalse();
        store.Contains(AutostartService.ValueName).Should().BeFalse();
    }

    [Fact]
    public void Toggle_flips_and_returns_new_state()
    {
        var store = new FakeAutostartStore();
        AutostartService service = Make(store);

        service.Toggle().Should().BeTrue();
        service.IsEnabled.Should().BeTrue();
        service.Toggle().Should().BeFalse();
        service.IsEnabled.Should().BeFalse();
    }
}
