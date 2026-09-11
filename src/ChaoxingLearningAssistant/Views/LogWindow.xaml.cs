using System.Diagnostics;
using System.Windows;
using ChaoxingLearningAssistant.Services;

namespace ChaoxingLearningAssistant.Views;

public partial class LogWindow : Window
{
    private readonly FileLogger _logger;

    public LogWindow(FileLogger logger)
    {
        InitializeComponent();
        _logger = logger;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var path = _logger.GetLatestLogFile();
        PathText.Text = path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LogText.Text = "暂无日志。";
            return;
        }

        try
        {
            var text = File.ReadAllText(path);
            const int maxChars = 300_000;
            LogText.Text = text.Length > maxChars ? text[^maxChars..] : text;
            LogText.ScrollToEnd();
        }
        catch (Exception ex)
        {
            LogText.Text = $"日志读取失败：{ex.Message}";
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.LogDirectory,
            UseShellExecute = true
        });
    }
}
