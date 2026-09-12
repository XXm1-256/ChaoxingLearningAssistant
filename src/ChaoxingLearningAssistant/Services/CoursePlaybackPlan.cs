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
        // 目录可能只暴露“章节未完成”，具体视频要进入章节后才创建。
        // 只放行平台明确未完成的未知章节供进入后核验；状态也未知时仍不盲跳。
        if (chapter.TaskType == TaskType.Unknown)
            return chapter.CompletionKnown && !chapter.IsCompleted;
        return chapter.TaskType == TaskType.Video &&
               (!chapter.CompletionKnown || !chapter.IsCompleted);
    }

    public static bool AllKnownVideosCompleted(IEnumerable<VideoTaskItem> tasks)
    {
        var videos = tasks.ToArray();
        return videos.Length > 0 && videos.All(x => x.CompletionKnown && x.IsCompleted);
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
