namespace ChaoxingLearningAssistant.Models;

public sealed class LearningStatRecord
{
    public string CourseTitle { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public double WatchedSeconds { get; set; }
    public double VideoDurationSeconds { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
    public string FinalStatus { get; set; } = string.Empty;
}
