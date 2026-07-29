using MicrosoftPinyinCleaner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MicrosoftPinyinCleaner.Tests;

[TestClass]
public sealed class InputMethodPolicyTests
{
    [TestMethod]
    public void FilterMicrosoftPinyin_RemovesOnlyExactTipCaseInsensitively()
    {
        var microsoftPinyinLowerCase = InputMethodPolicy.MicrosoftPinyinTip.ToLowerInvariant();
        var otherTip = "0804:{00000000-0000-0000-0000-000000000001}{00000000-0000-0000-0000-000000000002}";
        var similarButDifferentTip = InputMethodPolicy.MicrosoftPinyinTip + "-other";

        var filtered = InputMethodPolicy.FilterMicrosoftPinyin(
            new[] { otherTip, microsoftPinyinLowerCase, similarButDifferentTip });

        CollectionAssert.AreEqual(new[] { otherTip, similarButDifferentTip }, filtered.ToArray());
    }
}
