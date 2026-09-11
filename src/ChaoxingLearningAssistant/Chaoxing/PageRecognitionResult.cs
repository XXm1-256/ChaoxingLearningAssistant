using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Chaoxing;

public sealed class PageRecognitionResult
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public TaskType TaskType { get; set; } = TaskType.Unknown;
    public bool HasManualIntervention { get; set; }
    public string ManualInterventionReason { get; set; } = string.Empty;
    public string SelectorSummary { get; set; } = string.Empty;
}
