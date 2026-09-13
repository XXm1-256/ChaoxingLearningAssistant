using System.Collections.ObjectModel;
using ChaoxingLearningAssistant.ViewModels;

namespace ChaoxingLearningAssistant.Models;

public sealed class CourseItem : ObservableObject
{
    private string _title = string.Empty;
    private int _videoCount;
    private int _completedCount;
    private int? _taskCount;
    private int _completedTaskCount;
    public int? TaskCount
    {
        get => _taskCount;
        set { if (SetProperty(ref _taskCount, value)) { RaisePropertyChanged(nameof(TaskProgressPercent)); RaisePropertyChanged(nameof(TaskProgressSummary)); } }
    }
    public int CompletedTaskCount
    {
        get => _completedTaskCount;
        set { if (SetProperty(ref _completedTaskCount, value)) { RaisePropertyChanged(nameof(TaskProgressPercent)); RaisePropertyChanged(nameof(TaskProgressSummary)); } }
    }
    public double TaskProgressPercent => TaskCount > 0 ? Math.Clamp(CompletedTaskCount * 100.0 / TaskCount.Value, 0, 100) : 0;
    public string TaskProgressSummary => TaskCount is null ? "任务点进度尚未读取" : $"已完成任务点 {CompletedTaskCount} / {TaskCount}";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
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
