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
        // 目录可能只暴露章节入口，具体视频与完成状态要进入章节后才创建。
        // 已明确完成的章节直接排除；其余未知章节按课程顺序进入后核验。
        if (chapter.TaskType == TaskType.Unknown)
            return !chapter.CompletionKnown || !chapter.IsCompleted;
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
