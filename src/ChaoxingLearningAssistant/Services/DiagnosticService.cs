using System.IO.Compression;
using System.Text.Json;
using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public sealed class DiagnosticService
{
    private readonly FileLogger _logger;

    public DiagnosticService(FileLogger logger)
    {
        _logger = logger;
    }

    public string Export(DiagnosticSnapshot snapshot)
    {
        Directory.CreateDirectory(AppPaths.DiagnosticDirectory);
        var work = Path.Combine(AppPaths.DiagnosticDirectory, $"diag-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(work);

        var info = new
        {
            AppVersion = typeof(DiagnosticService).Assembly.GetName().Version?.ToString() ?? "unknown",
            Os = Environment.OSVersion.VersionString,
            Is64Bit = Environment.Is64BitOperatingSystem,
            Runtime = Environment.Version.ToString(),
            Portable = AppPaths.IsPortable,
            Snapshot = snapshot
        };

        var snapshotJson = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(
            Path.Combine(work, "snapshot.json"),
            SensitiveDataRedactor.Redact(snapshotJson));

        foreach (var log in _logger.GetRecentLogFiles(3))
        {
            try
            {
                var safeLog = SensitiveDataRedactor.Redact(File.ReadAllText(log));
                File.WriteAllText(Path.Combine(work, Path.GetFileName(log)), safeLog);
            }
            catch
            {
                // 单个日志复制失败不影响诊断包其余内容。
            }
        }

        var zip = work + ".zip";
        if (File.Exists(zip))
            File.Delete(zip);

        ZipFile.CreateFromDirectory(work, zip, CompressionLevel.Fastest, false);
        Directory.Delete(work, true);
        _logger.Info("DIAG-EXPORT", $"诊断包已导出：{zip}");
        return zip;
    }
}
