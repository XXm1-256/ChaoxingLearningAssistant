using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public sealed class CourseCacheService
{
    private readonly string _path;
    private readonly FileLogger _logger;

    public CourseCacheService(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    public IReadOnlyList<CourseItem> Load(TimeSpan? maxAge = null)
    {
        var snapshot = JsonFile.LoadOrDefault(_path, new CourseCacheSnapshot());
        var ageLimit = maxAge ?? TimeSpan.FromDays(30);
        if (snapshot.SavedAt == DateTime.MinValue || DateTime.Now - snapshot.SavedAt > ageLimit)
            return Array.Empty<CourseItem>();

        return (snapshot.Courses ?? new List<CourseCacheEntry>())
            .Where(x => x is not null && IsWebUrl(x.Url) && !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(x => new CourseItem
            {
                Title = x.Title.Trim(),
                Url = x.Url.Trim(),
                VideoCount = Math.Max(0, x.VideoCount),
                CompletedCount = Math.Clamp(x.CompletedCount, 0, Math.Max(0, x.VideoCount)),
                LastChapter = x.LastChapter?.Trim() ?? string.Empty
            })
            .ToArray();
    }

    public void Save(IEnumerable<CourseItem> courses)
    {
        try
        {
            var entries = courses
                .Where(x => x is not null && IsWebUrl(x.Url) && !string.IsNullOrWhiteSpace(x.Title))
                .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(x => new CourseCacheEntry
                {
                    Title = x.Title.Trim(),
                    Url = x.Url.Trim(),
                    VideoCount = Math.Max(0, x.VideoCount),
                    CompletedCount = Math.Clamp(x.CompletedCount, 0, Math.Max(0, x.VideoCount)),
                    LastChapter = x.LastChapter?.Trim() ?? string.Empty
                })
                .ToList();

            if (entries.Count == 0)
                return;

            JsonFile.Save(_path, new CourseCacheSnapshot { SavedAt = DateTime.Now, Courses = entries });
        }
        catch (Exception ex)
        {
            _logger.Warn("COURSE-CACHE", $"课程缓存保存失败：{ex.Message}");
        }
    }

    private static bool IsWebUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    public sealed class CourseCacheSnapshot
    {
        public DateTime SavedAt { get; set; }
        public List<CourseCacheEntry>? Courses { get; set; } = new();
    }

    public sealed class CourseCacheEntry
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int VideoCount { get; set; }
        public int CompletedCount { get; set; }
        public string LastChapter { get; set; } = string.Empty;
    }
}
