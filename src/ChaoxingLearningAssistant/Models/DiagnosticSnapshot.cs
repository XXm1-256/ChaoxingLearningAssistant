namespace ChaoxingLearningAssistant.Models;

public sealed class DiagnosticSnapshot
{
    public string CurrentUrl { get; set; } = string.Empty;
    public string PageTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public TaskType TaskType { get; set; } = TaskType.Unknown;
    public AppRunState State { get; set; } = AppRunState.Idle;
    public PlayerSnapshot Player { get; set; } = new();
    public string SelectorSummary { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public int RecoveryAttempt { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}
