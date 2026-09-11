namespace ChaoxingLearningAssistant.Models;

/// <summary>
/// 学习通页面中一个真实的视频任务点。它与“章节”分开建模，
/// 用于把课程目录、当前播放器和完成状态绑定到同一条真实视频。
/// </summary>
public sealed class VideoTaskItem
{
    public int Index { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string MediaId { get; set; } = string.Empty;
    public int DomIndex { get; set; } = -1;
    public bool IsVisible { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsCompleted { get; set; }
    public bool CompletionKnown { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
        ? $"视频 {Index + 1}"
        : Title.Trim();
}
