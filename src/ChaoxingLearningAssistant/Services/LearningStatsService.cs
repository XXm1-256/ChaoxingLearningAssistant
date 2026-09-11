using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public sealed class LearningStatsService
{
    private readonly object _sync = new();
    private readonly string _path;
    private readonly FileLogger _logger;

    public LearningStatsService(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    public IReadOnlyList<LearningStatRecord> GetAll()
    {
        lock (_sync)
            return JsonFile.LoadOrDefault(_path, new List<LearningStatRecord>())
                .OrderByDescending(x => x.StartedAt)
                .ToArray();
    }

    public IReadOnlyList<LearningStatRecord> GetToday()
    {
        var today = DateTime.Today;
        return GetAll()
            .Where(x => x.StartedAt.Date == today)
            .OrderByDescending(x => x.StartedAt)
            .ToArray();
    }

    public void Append(LearningStatRecord record)
    {
        lock (_sync)
        {
            try
            {
                var list = JsonFile.LoadOrDefault(_path, new List<LearningStatRecord>());
                list.Add(record);
                JsonFile.Save(_path, list);
                _logger.Info("STATS-APPEND", $"已记录学习统计：{record.VideoTitle}");
            }
            catch (Exception ex)
            {
                _logger.Error("STATS-001", "学习统计保存失败。", ex);
            }
        }
    }
}
