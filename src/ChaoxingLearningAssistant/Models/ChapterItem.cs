namespace ChaoxingLearningAssistant.Models;

public sealed class ChapterItem
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public bool IsSyntheticUrl { get; set; }
    public bool IsActive { get; set; }
    public TaskType TaskType { get; set; } = TaskType.Unknown;
    public bool IsCompleted { get; set; }
    public bool CompletionKnown { get; set; }
    public List<VideoTaskItem> VideoTasks { get; set; } = new();
    public int VideoCount => VideoTasks.Count;
    public int CompletedVideoCount => VideoTasks.Count(x => x.CompletionKnown && x.IsCompleted);
    public bool HasVideoTasks => VideoTasks.Count > 0;
    public bool IsVideo => TaskType == TaskType.Video || HasVideoTasks;
    public bool IsNavigationCandidate => IsVideo || TaskType == TaskType.Unknown;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"章节 {Index + 1}" : Title.Trim();
    public string StatusText => CompletionKnown && IsCompleted ? "已完成" : string.Empty;
    public string TaskTypeText => TaskType switch
    {
        TaskType.Video => "视频",
        TaskType.Document => "资料",
        TaskType.Quiz => "测验",
        TaskType.Homework => "作业",
        TaskType.SignIn => "签到",
        TaskType.Exam => "考试",
        TaskType.Other => "其他",
        _ => "章节"
    };
}
