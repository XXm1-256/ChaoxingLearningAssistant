namespace ChaoxingLearningAssistant.Models;

public sealed class PlayerSnapshot
{
    public bool Found { get; set; }
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
    public bool Paused { get; set; }
    public bool Ended { get; set; }
    public int ReadyState { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MediaId { get; set; } = string.Empty;
    public int DomIndex { get; set; } = -1;
    public bool IsVisible { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string ChapterTitleHint { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;

    public double ProgressPercent => Duration <= 0 ? 0 : Math.Clamp(CurrentTime / Duration * 100.0, 0, 100);

    public string TimeText => $"{FormatTime(CurrentTime)} / {FormatTime(Duration)}";

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
    }
}
