using MicrosoftPinyinCleaner.Models;

namespace MicrosoftPinyinCleaner.Services;

public static class InputMethodPolicy
{
    public const string MicrosoftPinyinTip =
        "0804:{81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E}{FA550B04-5AD7-411F-A5AC-CA038EC515D7}";

    public static bool IsMicrosoftPinyin(string? tip) =>
        string.Equals(tip?.Trim(), MicrosoftPinyinTip, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> FilterMicrosoftPinyin(IEnumerable<string> tips) =>
        tips.Where(tip => !IsMicrosoftPinyin(tip)).ToArray();

    public static InputMethodAnalysis Analyze(IEnumerable<InputMethodLanguage> languages)
    {
        var microsoftPinyinCount = 0;
        var otherChineseCount = 0;
        var remainingCount = 0;

        foreach (var language in languages)
        {
            var isChinese = IsChineseLanguageTag(language.LanguageTag);

            foreach (var tip in language.InputMethodTips)
            {
                if (IsMicrosoftPinyin(tip))
                {
                    microsoftPinyinCount++;
                    continue;
                }

                remainingCount++;
                if (isChinese)
                {
                    otherChineseCount++;
                }
            }
        }

        return new InputMethodAnalysis(
            microsoftPinyinCount > 0,
            microsoftPinyinCount,
            otherChineseCount,
            remainingCount);
    }

    private static bool IsChineseLanguageTag(string? languageTag) =>
        !string.IsNullOrWhiteSpace(languageTag) &&
        (languageTag.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
         languageTag.StartsWith("zh-", StringComparison.OrdinalIgnoreCase));
}
