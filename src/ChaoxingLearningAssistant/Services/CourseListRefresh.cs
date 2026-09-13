using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

/// <summary>Preserve selected object identity and observed progress across list scans.</summary>
public static class CourseListRefresh
{
    public static IReadOnlyList<CourseItem> Merge(IEnumerable<CourseItem> existing, IEnumerable<CourseItem> scanned)
    {
        var known = existing.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        return scanned.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var fresh = group.First();
            if (!known.TryGetValue(fresh.Url, out var retained)) return fresh;
            retained.Title = fresh.Title;
            if (fresh.TaskCount is not null)
            {
                retained.TaskCount = fresh.TaskCount;
                retained.CompletedTaskCount = fresh.CompletedTaskCount;
            }
            // Course-list scans do not carry chapter/player progress. Keep observed counts.
            return retained;
        }).ToArray();
    }
}
