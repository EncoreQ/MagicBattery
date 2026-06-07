using System.Globalization;

namespace MagicBattery.Hid.Tests;

/// <summary>
/// 加载 tests/fixtures 下的 .hex 录制数据(构建时拷到输出的 fixtures/ 目录)。
/// </summary>
internal static class FixtureLoader
{
    /// <summary>读取并解析一份 hex fixture,例如 LoadBytes("usb", "mt2_50pct")。</summary>
    public static byte[] LoadBytes(string category, string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", category, name + ".hex");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到 fixture: {path}");
        }

        string text = File.ReadAllText(path);
        string[] tokens = text.Split(
            new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var bytes = new byte[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            bytes[i] = byte.Parse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
