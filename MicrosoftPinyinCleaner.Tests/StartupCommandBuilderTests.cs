using MicrosoftPinyinCleaner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MicrosoftPinyinCleaner.Tests;

[TestClass]
public sealed class StartupCommandBuilderTests
{
    [TestMethod]
    public void Build_QuotesExecutablePathContainingSpacesAndChineseCharacters()
    {
        const string executablePath = @"D:\工具 软件\微软拼音清理工具\MicrosoftPinyinCleaner.exe";

        var command = StartupCommandBuilder.Build(executablePath);

        Assert.AreEqual(
            "\"D:\\工具 软件\\微软拼音清理工具\\MicrosoftPinyinCleaner.exe\" --silent-clean",
            command);
    }
}
