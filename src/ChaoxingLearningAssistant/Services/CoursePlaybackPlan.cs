using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public static class CoursePlaybackPlan
{
    public static IReadOnlyList<ChapterItem> BuildPendingChapters(
        IEnumerable<ChapterItem> chapters,
        int startIndex,
        ISet<string> verifiedChapters)
    {
        var firstIndex = Math.Max(0, startIndex);
        return chapters
            .Where(x => x.Index >= firstIndex && x.IsNavigationCandidate && HasPendingVideo(x))
            .Where(x => !verifiedChapters.Contains(Identity(x)))
            .OrderBy(x => x.Index)
            .ToArray();
    }

    public static bool HasPendingVideo(ChapterItem chapter)
    {
        if (chapter.VideoTasks.Count > 0)
            return chapter.VideoTasks.Any(x => !x.CompletionKnown || !x.IsCompleted);
        return !chapter.CompletionKnown || !chapter.IsCompleted;
    }

    public static string Identity(ChapterItem chapter)
    {
        if (!string.IsNullOrWhiteSpace(chapter.ChapterId))
            return $"id:{chapter.ChapterId}";
        if (!string.IsNullOrWhiteSpace(chapter.Url))
            return $"url:{chapter.Url}";
        return $"title:{chapter.Index}:{chapter.Title}";
    }
}
