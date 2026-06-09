using System.IO;
using FluentAssertions;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class DebugLoggerTests : IDisposable
{
    private static readonly Func<DateTimeOffset> FixedClock =
        () => new DateTimeOffset(2026, 6, 10, 12, 30, 45, 123, TimeSpan.FromHours(8));

    private readonly string _dir;
    private readonly string _path;

    public DebugLoggerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MagicBatteryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "debug.log");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    [Fact]
    public void Disabled_instance_is_noop_and_does_not_throw()
    {
        DebugLogger.Disabled.Enabled.Should().BeFalse();
        DebugLogger.Disabled.Write("ignored");
        DebugLogger.Disabled.Write("ctx", new InvalidOperationException("boom"));
    }

    [Fact]
    public void Write_appends_timestamped_lines()
    {
        var log = new DebugLogger(_path, FixedClock);

        log.Write("第一条");
        log.Write("第二条");

        string[] lines = File.ReadAllLines(_path);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("2026-06-10 12:30:45.123 第一条");
        lines[1].Should().EndWith("第二条");
    }

    [Fact]
    public void New_file_starts_with_utf8_bom()
    {
        var log = new DebugLogger(_path, FixedClock);

        log.Write("中文");
        log.Write("第二条"); // 追加不重复写 BOM

        byte[] bytes = File.ReadAllBytes(_path);
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        // 第二行若混入 BOM 会以 U+FEFF 字符出现
        string[] lines = File.ReadAllLines(_path);
        lines.Should().HaveCount(2);
        lines[1].Should().NotContain(((char)0xFEFF).ToString()).And.EndWith("第二条");
    }

    [Fact]
    public void Exception_overload_includes_type_and_message()
    {
        var log = new DebugLogger(_path, FixedClock);

        log.Write("保存配置失败", new IOException("磁盘满"));

        string text = File.ReadAllText(_path);
        text.Should().Contain("保存配置失败");
        text.Should().Contain(nameof(IOException));
        text.Should().Contain("磁盘满");
    }

    [Fact]
    public void Creates_missing_directory()
    {
        string nested = Path.Combine(_dir, "sub", "debug.log");

        new DebugLogger(nested, FixedClock).Write("hi");

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Oversize_file_is_truncated_on_construction()
    {
        File.WriteAllBytes(_path, new byte[2 * 1024 * 1024]); // 2MB 旧日志

        var log = new DebugLogger(_path, FixedClock);
        log.Write("新一轮");

        new FileInfo(_path).Length.Should().BeLessThan(1024); // 旧内容已清,只剩新行
        File.ReadAllText(_path).Should().Contain("新一轮");
    }

    [Fact]
    public void Invalid_path_degrades_to_disabled_without_throwing()
    {
        // 目录位置被同名文件占住 → CreateDirectory 抛 IOException → 降级为禁用
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");

        var log = new DebugLogger(Path.Combine(blocker, "debug.log"), FixedClock);

        log.Enabled.Should().BeFalse();
        log.Write("ignored"); // no-op,不抛
    }
}
