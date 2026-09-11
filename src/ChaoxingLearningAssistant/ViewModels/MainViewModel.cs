using System.Collections.ObjectModel;
using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private CourseItem? _selectedCourse;
    private ChapterItem? _selectedChapter;
    private string _statusText = "空闲";
    private string _playbackStatusText = "未检测到视频";
    private string _automationStatusText = "辅助待命";
    private string _networkText = "正常";
    private string _currentChapterText = "-";
    private string _currentVideoText = "-";
    private string _nextVideoText = "-";
    private string _playerTimeText = "00:00 / 00:00";
    private string _playbackRateText = "1.0x";
    private double _progressPercent;
    private bool _canContinueNext;

    public ObservableCollection<CourseItem> Courses { get; } = new();
    public ObservableCollection<ChapterItem> Chapters { get; } = new();
    public ObservableCollection<VideoTaskItem> VideoTasks { get; } = new();

    public CourseItem? SelectedCourse
    {
        get => _selectedCourse;
        set => SetProperty(ref _selectedCourse, value);
    }

    public ChapterItem? SelectedChapter
    {
        get => _selectedChapter;
        set => SetProperty(ref _selectedChapter, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string PlaybackStatusText
    {
        get => _playbackStatusText;
        set => SetProperty(ref _playbackStatusText, value);
    }

    public string AutomationStatusText
    {
        get => _automationStatusText;
        set => SetProperty(ref _automationStatusText, value);
    }

    public string NetworkText
    {
        get => _networkText;
        set => SetProperty(ref _networkText, value);
    }

    public string CurrentChapterText
    {
        get => _currentChapterText;
        set => SetProperty(ref _currentChapterText, value);
    }

    public string CurrentVideoText
    {
        get => _currentVideoText;
        set => SetProperty(ref _currentVideoText, value);
    }

    public string NextVideoText
    {
        get => _nextVideoText;
        set => SetProperty(ref _nextVideoText, value);
    }

    public string PlayerTimeText
    {
        get => _playerTimeText;
        set => SetProperty(ref _playerTimeText, value);
    }

    public string PlaybackRateText
    {
        get => _playbackRateText;
        set => SetProperty(ref _playbackRateText, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public bool CanContinueNext
    {
        get => _canContinueNext;
        set => SetProperty(ref _canContinueNext, value);
    }
}
