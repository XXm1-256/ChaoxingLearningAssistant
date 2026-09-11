namespace ChaoxingLearningAssistant.Models;

public sealed class SessionSnapshot
{
    public bool WasRunning { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseUrl { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public string ChapterUrl { get; set; } = string.Empty;
    public double PositionSeconds { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
    public AppRunState State { get; set; } = AppRunState.Idle;
    public DateTime SavedAt { get; set; } = DateTime.Now;
}
