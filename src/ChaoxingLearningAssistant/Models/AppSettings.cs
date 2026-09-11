namespace ChaoxingLearningAssistant.Models;

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public bool EnableInAppNotifications { get; set; } = true;
    public bool EnableWindowsNotifications { get; set; } = true;
    public bool EnableSound { get; set; } = true;
    public bool EnableDeveloperMode { get; set; } = false;
    public bool AutoPlayNextVideo { get; set; } = true;
    public bool PreservePlaybackRate { get; set; } = true;
    public int LogRetentionDays { get; set; } = 30;
    public int PageTimeoutSeconds { get; set; } = 20;
    public int MaxPageTimeoutSeconds { get; set; } = 60;
    public bool FirstRunCompleted { get; set; } = false;
    public string LastCourseUrl { get; set; } = string.Empty;
    public string LastCourseTitle { get; set; } = string.Empty;

    public void Normalize()
    {
        if (!Enum.IsDefined(Theme)) Theme = ThemeMode.Light;
        LogRetentionDays = Math.Clamp(LogRetentionDays, 1, 3650);
        PageTimeoutSeconds = Math.Clamp(PageTimeoutSeconds, 5, 60);
        MaxPageTimeoutSeconds = Math.Clamp(MaxPageTimeoutSeconds, PageTimeoutSeconds, 120);
        LastCourseTitle = (LastCourseTitle ?? string.Empty).Trim();
        LastCourseUrl = NormalizeWebUrl(LastCourseUrl);
    }

    private static string NormalizeWebUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https"
            ? uri.ToString()
            : string.Empty;
    }
}
