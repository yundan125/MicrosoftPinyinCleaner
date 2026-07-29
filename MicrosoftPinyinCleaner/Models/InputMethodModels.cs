namespace MicrosoftPinyinCleaner.Models;

public enum MicrosoftPinyinState
{
    Installed,
    NotInstalled,
    DetectionFailed
}

public sealed record InputMethodLanguage(string LanguageTag, IReadOnlyList<string> InputMethodTips);

public sealed record InputMethodAnalysis(
    bool HasMicrosoftPinyin,
    int MicrosoftPinyinCount,
    int OtherChineseInputMethodCount,
    int RemainingInputMethodCount)
{
    public bool CanSafelyRemove => HasMicrosoftPinyin && OtherChineseInputMethodCount > 0 && RemainingInputMethodCount > 0;
}

public sealed record DetectionResult(
    MicrosoftPinyinState State,
    bool CanSafelyRemove,
    string Message)
{
    public bool Success => State != MicrosoftPinyinState.DetectionFailed;
}

public enum RemovalOutcome
{
    Removed,
    NotInstalled,
    BlockedNoAlternative,
    Failed
}

public sealed record RemovalResult(RemovalOutcome Outcome, string Message)
{
    public bool Success => Outcome != RemovalOutcome.Failed;
}

public sealed record StartupStatus(bool IsEnabled, bool HasStaleEntry, string Message);
