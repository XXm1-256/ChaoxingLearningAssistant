using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

public static class NextVideoPreviewResolver
{
    public const string IdentifyingText = "正在识别下一视频…";
    public const string NoNextText = "未发现待播放的下一视频";

    public static string Resolve(
        IEnumerable<ChapterItem> chapters,
        ChapterItem? currentChapter,
        VideoTaskItem? currentTask)
    {
        var orderedChapters = chapters.OrderBy(x => x.Index).ToArray();
        if (currentChapter is null && currentTask is not null)
            currentChapter = FindChapter(orderedChapters, currentTask);

        if (currentChapter is null)
            return IdentifyingText;

        var currentVideos = currentChapter.VideoTasks.OrderBy(x => x.Index).ToArray();
        var effectiveTask = currentTask ?? currentVideos.FirstOrDefault(x => x.IsPlaying);
        if (effectiveTask is not null)
        {
            var currentIndex = Array.FindIndex(currentVideos, x => SameTask(x, effectiveTask));
            if (currentIndex >= 0)
            {
                var nextInChapter = currentVideos
                    .Skip(currentIndex + 1)
                    .FirstOrDefault(IsPending);
                if (nextInChapter is not null)
                    return FormatTask(nextInChapter, currentVideos);
            }
        }

        var chapterPosition = Array.FindIndex(orderedChapters, x => SameChapter(x, currentChapter));
        if (chapterPosition < 0)
            return IdentifyingText;

        foreach (var chapter in orderedChapters.Skip(chapterPosition + 1))
        {
            if (chapter.TaskType == TaskType.Quiz || chapter.TaskType == TaskType.Homework ||
                chapter.TaskType == TaskType.Exam || chapter.TaskType == TaskType.SignIn ||
                chapter.TaskType == TaskType.Document || chapter.TaskType == TaskType.Other)
                continue;

            var videos = chapter.VideoTasks.OrderBy(x => x.Index).ToArray();
            var nextTask = videos.FirstOrDefault(IsPending);
            if (nextTask is not null)
                return FormatTask(nextTask, videos);

            var needsChapterInspection = videos.Length == 0 &&
                ((chapter.TaskType == TaskType.Video && (!chapter.CompletionKnown || !chapter.IsCompleted)) ||
                 (chapter.TaskType == TaskType.Unknown && (!chapter.CompletionKnown || !chapter.IsCompleted)));
            if (needsChapterInspection)
                return $"下一章节 · {chapter.DisplayTitle}（进入后定位未完成视频）";
        }

        return NoNextText;
    }

    private static ChapterItem? FindChapter(IEnumerable<ChapterItem> chapters, VideoTaskItem task)
    {
        if (!string.IsNullOrWhiteSpace(task.ChapterId))
        {
            var byId = chapters.FirstOrDefault(x =>
                string.Equals(x.ChapterId, task.ChapterId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        return chapters.FirstOrDefault(x => x.VideoTasks.Any(video => SameTask(video, task)));
    }

    private static bool IsPending(VideoTaskItem task)
        => !task.CompletionKnown || !task.IsCompleted;

    private static string FormatTask(VideoTaskItem task, IReadOnlyList<VideoTaskItem> chapterVideos)
    {
        var position = Array.FindIndex(chapterVideos.ToArray(), x => SameTask(x, task));
        var prefix = position >= 0 && chapterVideos.Count > 1
            ? $"第 {position + 1}/{chapterVideos.Count} 个视频"
            : "下一视频";
        return string.IsNullOrWhiteSpace(task.Title)
            ? prefix
            : $"{prefix} · {task.Title.Trim()}";
    }

    private static bool SameChapter(ChapterItem left, ChapterItem right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (!string.IsNullOrWhiteSpace(left.ChapterId) && !string.IsNullOrWhiteSpace(right.ChapterId))
            return string.Equals(left.ChapterId, right.ChapterId, StringComparison.OrdinalIgnoreCase);
        return left.Index == right.Index && string.Equals(left.Title, right.Title, StringComparison.Ordinal);
    }

    private static bool SameTask(VideoTaskItem left, VideoTaskItem right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (!string.IsNullOrWhiteSpace(left.TaskKey) && !string.IsNullOrWhiteSpace(right.TaskKey))
            return string.Equals(left.TaskKey, right.TaskKey, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(left.MediaId) && !string.IsNullOrWhiteSpace(right.MediaId))
            return string.Equals(left.MediaId, right.MediaId, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(left.Source) && !string.IsNullOrWhiteSpace(right.Source))
            return string.Equals(left.Source, right.Source, StringComparison.OrdinalIgnoreCase);
        return left.DomIndex >= 0 && left.DomIndex == right.DomIndex &&
               string.Equals(left.DocumentUrl, right.DocumentUrl, StringComparison.OrdinalIgnoreCase);
    }
}
