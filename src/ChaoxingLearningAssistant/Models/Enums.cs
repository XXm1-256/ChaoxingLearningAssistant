namespace ChaoxingLearningAssistant.Models;

public enum TaskType
{
    Unknown,
    Video,
    Document,
    Quiz,
    Homework,
    SignIn,
    Exam,
    Other
}

public enum AppRunState
{
    Idle,
    Loading,
    Playing,
    VideoPaused,
    AutomationPaused,
    Ended,
    FindingNextVideo,
    PreloadingNextVideo,
    WaitingForUser,
    NetworkError,
    Recovering,
    ManualIntervention,
    PageRecognitionFailed,
    CourseCompleted,
    Stopped
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}
