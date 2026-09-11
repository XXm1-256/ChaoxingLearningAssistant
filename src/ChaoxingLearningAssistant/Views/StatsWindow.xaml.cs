using System.Windows;
using ChaoxingLearningAssistant.Services;

namespace ChaoxingLearningAssistant.Views;

public partial class StatsWindow : Window
{
    private readonly LearningStatsService _service;

    public StatsWindow(LearningStatsService service)
    {
        InitializeComponent();
        _service = service;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var data = _service.GetToday();
        StatsGrid.ItemsSource = data;
        var total = data.Sum(x => x.WatchedSeconds);
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, total));
        SummaryText.Text = $"今日记录 {data.Count} 条，累计播放 {(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
