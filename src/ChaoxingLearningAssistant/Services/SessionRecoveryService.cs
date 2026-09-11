using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public sealed class SessionRecoveryService
{
    private readonly string _path;
    private readonly FileLogger _logger;

    public SessionRecoveryService(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    public SessionSnapshot Load()
        => JsonFile.LoadOrDefault(_path, new SessionSnapshot());

    public void Save(SessionSnapshot snapshot)
    {
        try
        {
            JsonFile.Save(_path, snapshot);
        }
        catch (Exception ex)
        {
            _logger.Error("SESSION-001", "会话恢复信息保存失败。", ex);
        }
    }

    public void MarkCleanExit()
    {
        var snapshot = Load();
        snapshot.WasRunning = false;
        snapshot.SavedAt = DateTime.Now;
        Save(snapshot);
    }

    public void Clear()
    {
        try
        {
            // Remove fallback first: deleting only the primary resurrects the old session.
            if (File.Exists(_path + ".bak"))
                File.Delete(_path + ".bak");
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch (Exception ex)
        {
            _logger.Warn("SESSION-CLEAR", $"会话文件清理失败：{ex.Message}");
        }
    }
}
