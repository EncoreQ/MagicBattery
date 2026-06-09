using System.IO;
using FluentAssertions;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MagicBatteryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        AppConfig config = new ConfigService(_path).Load();

        config.PollIntervalMinutes.Should().Be(15);
        config.LowBatteryAlertsEnabled.Should().BeTrue();
        config.AlertThresholds.Should().Equal(20, 10, 5);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var service = new ConfigService(_path);
        var saved = new AppConfig
        {
            PollIntervalMinutes = 30,
            LowBatteryAlertsEnabled = false,
            AlertThresholds = new[] { 15, 5 },
        };

        service.Save(saved);
        AppConfig loaded = new ConfigService(_path).Load();

        loaded.PollIntervalMinutes.Should().Be(30);
        loaded.LowBatteryAlertsEnabled.Should().BeFalse();
        loaded.AlertThresholds.Should().Equal(15, 5);
    }

    [Fact]
    public void Corrupt_json_returns_defaults_without_throwing()
    {
        File.WriteAllText(_path, "{ this is not valid json ");

        AppConfig config = new ConfigService(_path).Load();

        config.PollIntervalMinutes.Should().Be(15);
        config.LowBatteryAlertsEnabled.Should().BeTrue();
        config.AlertThresholds.Should().Equal(20, 10, 5);
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        string nested = Path.Combine(_dir, "sub", "config.json");

        new ConfigService(nested).Save(AppConfig.Default);

        File.Exists(nested).Should().BeTrue();
    }
}
