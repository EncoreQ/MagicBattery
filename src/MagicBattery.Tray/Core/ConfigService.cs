using System.IO;
using System.Text.Json;

namespace MagicBattery.Tray.Core;

/// <summary>
/// 读写 <see cref="AppConfig"/> 到 JSON 文件(默认 %APPDATA%\MagicBattery\config.json)。
/// 用内置 <c>System.Text.Json</c>,不引依赖。路径可注入便于单测。
/// 文件缺失 / 损坏一律回默认值,不抛;保存走临时文件 + 替换,避免写一半损坏。
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public ConfigService(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    /// <summary>默认配置文件路径:%APPDATA%\MagicBattery\config.json。</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MagicBattery",
        "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return AppConfig.Default;
            }

            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? AppConfig.Default;
        }
        catch
        {
            // 损坏 / 读不了 → 回默认,不让坏文件阻断启动
            return AppConfig.Default;
        }
    }

    public void Save(AppConfig config)
    {
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
