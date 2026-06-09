using System.IO;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 极简调试日志(opt-in):由启动参数 <c>--log</c> 或环境变量 <c>MAGICBATTERY_LOG=1</c>
/// 开启,append 到 %APPDATA%\MagicBattery\debug.log。禁用时所有调用为 no-op,
/// 常驻运行零开销。不引依赖,路径与时钟可注入便于单测。
///
/// 日志自身绝不能成为新的崩溃源:目录建不了、文件写不进,一律静默降级/吞掉。
/// </summary>
public sealed class DebugLogger
{
    private const long MaxBytes = 1024 * 1024; // 启用时旧文件超 1MB 直接清掉,避免无限膨胀

    private readonly object _gate = new();
    private readonly string? _path; // null = 禁用
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>全 no-op 的禁用实例(默认)。</summary>
    public static DebugLogger Disabled { get; } = new(null);

    public DebugLogger(string? path, Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.Now);
        if (path is null)
        {
            return;
        }

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var info = new FileInfo(path);
            if (info.Exists && info.Length > MaxBytes)
            {
                info.Delete();
            }

            _path = path;
        }
        catch
        {
            // 目录建不了 / 旧文件删不掉 → 当作禁用,不影响主流程
            _path = null;
        }
    }

    public bool Enabled => _path is not null;

    /// <summary>默认日志路径:%APPDATA%\MagicBattery\debug.log(与 config.json 同目录)。</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MagicBattery",
        "debug.log");

    public void Write(string message)
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                File.AppendAllText(_path,
                    $"{_clock():yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 写失败(磁盘满、文件被占)吞掉
        }
    }

    /// <summary>记一条异常,含完整 ToString(类型 + 消息 + 堆栈)。</summary>
    public void Write(string context, Exception ex) => Write($"{context}: {ex}");
}
