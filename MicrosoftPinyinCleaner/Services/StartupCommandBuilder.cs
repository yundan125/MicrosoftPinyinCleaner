namespace MicrosoftPinyinCleaner.Services;

public static class StartupCommandBuilder
{
    public static string Build(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("程序路径不能为空。", nameof(executablePath));
        }

        if (executablePath.Contains('"'))
        {
            throw new ArgumentException("程序路径不能包含双引号。", nameof(executablePath));
        }

        return $"{QuoteWindowsArgument(executablePath)} --silent-clean";
    }

    public static string QuoteWindowsArgument(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 2);
        result.Append('"');
        var consecutiveBackslashes = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                consecutiveBackslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', consecutiveBackslashes * 2 + 1);
                result.Append('"');
                consecutiveBackslashes = 0;
                continue;
            }

            result.Append('\\', consecutiveBackslashes);
            consecutiveBackslashes = 0;
            result.Append(character);
        }

        result.Append('\\', consecutiveBackslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
