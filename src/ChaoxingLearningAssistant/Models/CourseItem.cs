using System.Collections.ObjectModel;
using ChaoxingLearningAssistant.ViewModels;

namespace ChaoxingLearningAssistant.Models;

public sealed class CourseItem : ObservableObject
{
    private int _videoCount;
    private int _completedCount;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int VideoCount
    {
        get => _videoCount;
        set
        {
            if (!SetProperty(ref _videoCount, value)) return;
            RaisePropertyChanged(nameof(ProgressPercent));
            RaisePropertyChanged(nameof(ProgressSummary));
        }
    }
    public int CompletedCount
    {
        get => _completedCount;
        set
        {
            if (!SetProperty(ref _completedCount, value)) return;
            RaisePropertyChanged(nameof(ProgressPercent));
            RaisePropertyChanged(nameof(ProgressSummary));
        }
    }
    public string LastChapter { get; set; } = string.Empty;
    public double ProgressPercent => VideoCount <= 0 ? 0 : Math.Clamp((double)CompletedCount / VideoCount * 100.0, 0, 100);
    public string ProgressSummary => VideoCount <= 0
        ? "进入课程后同步视频进度"
        : $"已完成 {CompletedCount} / {VideoCount}  ·  {ProgressPercent:F0}%";
    public ObservableCollection<ChapterItem> Chapters { get; } = new();

    public override string ToString() => Title;
}
