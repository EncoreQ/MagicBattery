using System.Globalization;

namespace MagicBattery.Tray.Core;

/// <summary>解析后的实际界面语言。</summary>
public enum AppLanguage
{
    Chinese,
    English,
}

/// <summary>用户的语言设置(持久化)。<see cref="System"/> = 跟随系统。</summary>
public enum LanguagePreference
{
    System,
    Chinese,
    English,
}

/// <summary>把语言设置 + 系统区域解析成实际语言(纯函数,可单测)。</summary>
public static class LanguageResolver
{
    /// <summary>
    /// 跟随系统时:`zh-*` → 中文、`en-*` → 英文、其它 → **中文**(回退)。
    /// 显式中/英则直接采用。
    /// </summary>
    public static AppLanguage Resolve(LanguagePreference preference, CultureInfo systemUiCulture) => preference switch
    {
        LanguagePreference.Chinese => AppLanguage.Chinese,
        LanguagePreference.English => AppLanguage.English,
        _ => systemUiCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "zh" => AppLanguage.Chinese,
            "en" => AppLanguage.English,
            _ => AppLanguage.Chinese, // 非中英 → 回退中文
        },
    };
}
