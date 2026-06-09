using System.Globalization;
using FluentAssertions;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests;

public class LanguageResolverTests
{
    [Theory]
    [InlineData("zh-CN", AppLanguage.Chinese)]
    [InlineData("zh-TW", AppLanguage.Chinese)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("en-GB", AppLanguage.English)]
    [InlineData("fr-FR", AppLanguage.Chinese)] // 非中英 → 回退中文
    [InlineData("ja-JP", AppLanguage.Chinese)]
    public void System_follows_culture_with_chinese_fallback(string culture, AppLanguage expected)
    {
        LanguageResolver.Resolve(LanguagePreference.System, new CultureInfo(culture))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(LanguagePreference.Chinese, AppLanguage.Chinese)]
    [InlineData(LanguagePreference.English, AppLanguage.English)]
    public void Explicit_preference_overrides_system(LanguagePreference pref, AppLanguage expected)
    {
        // 即便系统是日文,显式设置也直接生效
        LanguageResolver.Resolve(pref, new CultureInfo("ja-JP")).Should().Be(expected);
    }
}
