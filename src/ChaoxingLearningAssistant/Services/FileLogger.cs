using System.Text;

namespace ChaoxingLearningAssistant.Services;

public sealed class FileLogger
{
    private readonly object _sync = new();
    private readonly string _directory;

    public event EventHandler<LogEntry>? EntryWritten;

    public FileLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public void Debug(string code, string message) => Write("DEBUG", code, message, null);
    public void Info(string code, string message) => Write("INFO", code, message, null);
    public void Warn(string code, string message) => Write("WARN", code, message, null);
    public void Error(string code, string message, Exception? ex = null) => Write("ERROR", code, message, ex);

    public string GetLatestLogFile()
        => Directory.EnumerateFiles(_directory, "*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? string.Empty;

    public IReadOnlyList<string> GetRecentLogFiles(int count = 3)
        => Directory.EnumerateFiles(_directory, "*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(Math.Max(1, count))
            .ToArray();

    public void CleanupOldLogs(int retentionDays)
    {
        retentionDays = Math.Clamp(retentionDays, 1, 3650);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        foreach (var file in Directory.EnumerateFiles(_directory, "*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // 清理失败不能影响主程序启动。
            }
        }
    }

    private void Write(string level, string code, string message, Exception? ex)
    {
        var now = DateTime.Now;
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(level).Append("] ")
            .Append(code).Append(" - ")
            .Append(message);

        if (ex is not null)
            line.AppendLine().Append(ex);

        var text = line.ToString();
        var path = Path.Combine(_directory, $"{now:yyyy-MM-dd}.log");

        lock (_sync)
        {
            File.AppendAllText(path, text + Environment.NewLine, Encoding.UTF8);
        }

        try
        {
            EntryWritten?.Invoke(this, new LogEntry(now, level, code, message));
        }
        catch
        {
            // UI/log observers must never make the operation being logged fail.
        }
    }
}

public sealed record LogEntry(DateTime Time, string Level, string Code, string Message);
