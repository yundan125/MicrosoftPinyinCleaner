using System.Text;
using System.IO;

namespace MicrosoftPinyinCleaner.Services;

public sealed class AppLogger
{
    private readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MicrosoftPinyinCleaner",
        "logs",
        "latest.log");

    public async Task WriteLatestAsync(string eventName, string result)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath)!;
            Directory.CreateDirectory(directory);
            var content = new StringBuilder()
                .AppendLine($"时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
                .AppendLine($"事件：{Sanitize(eventName)}")
                .AppendLine($"结果：{Sanitize(result)}")
                .ToString();
            await File.WriteAllTextAsync(_logPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Logging must never interrupt normal or silent cleanup.
        }
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
