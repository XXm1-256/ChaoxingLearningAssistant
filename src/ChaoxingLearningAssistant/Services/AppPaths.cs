namespace ChaoxingLearningAssistant.Services;

public static class AppPaths
{
    public const string AppName = "ChaoxingLearningAssistant";

    public static bool IsPortable
        => File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));

    public static string BaseDataDirectory
        => IsPortable
            ? Path.Combine(AppContext.BaseDirectory, "Data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName);

    public static string SettingsFile => Path.Combine(BaseDataDirectory, "settings.json");
    public static string StatsFile => Path.Combine(BaseDataDirectory, "learning-stats.json");
    public static string SessionFile => Path.Combine(BaseDataDirectory, "session.json");
    public static string CacheFile => Path.Combine(BaseDataDirectory, "course-cache.json");
    public static string LogDirectory => Path.Combine(BaseDataDirectory, "Logs");
    public static string DiagnosticDirectory => Path.Combine(BaseDataDirectory, "Diagnostics");
    public static string WebViewUserDataDirectory => Path.Combine(BaseDataDirectory, "WebView2");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(DiagnosticDirectory);
        Directory.CreateDirectory(WebViewUserDataDirectory);
    }
}
