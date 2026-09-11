using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public sealed class SettingsService
{
    private readonly string _path;
    private readonly FileLogger _logger;

    public AppSettings Current { get; private set; }

    public SettingsService(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
        Current = JsonFile.LoadOrDefault(_path, new AppSettings());
        Current.Normalize();
    }

    public void Save()
    {
        try
        {
            Current.Normalize();
            JsonFile.Save(_path, Current);
            _logger.Info("SETTINGS-SAVE", "程序设置已保存。");
        }
        catch (Exception ex)
        {
            _logger.Error("SETTINGS-SAVE-001", "程序设置保存失败。", ex);
        }
    }

    public void Replace(AppSettings settings)
    {
        settings.Normalize();
        Current = settings;
        Save();
    }
}
