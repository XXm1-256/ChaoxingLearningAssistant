using System.Windows;
using System.Windows.Controls;
using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _readyForFeedback;

    public bool ClearLoginRequested { get; private set; }

    public SettingsWindow(AppSettings source)
    {
        InitializeComponent();
        _settings = new AppSettings
        {
            Theme = source.Theme,
            EnableInAppNotifications = source.EnableInAppNotifications,
            EnableWindowsNotifications = source.EnableWindowsNotifications,
            EnableSound = source.EnableSound,
            EnableDeveloperMode = source.EnableDeveloperMode,
            AutoPlayNextVideo = source.AutoPlayNextVideo,
            PreservePlaybackRate = source.PreservePlaybackRate,
            LogRetentionDays = source.LogRetentionDays,
            PageTimeoutSeconds = source.PageTimeoutSeconds,
            MaxPageTimeoutSeconds = source.MaxPageTimeoutSeconds,
            FirstRunCompleted = source.FirstRunCompleted,
            LastCourseUrl = source.LastCourseUrl,
            LastCourseTitle = source.LastCourseTitle
        };

        SelectTheme(source.Theme);
        InAppCheck.IsChecked = source.EnableInAppNotifications;
        WindowsNotifyCheck.IsChecked = source.EnableWindowsNotifications;
        SoundCheck.IsChecked = source.EnableSound;
        DeveloperCheck.IsChecked = source.EnableDeveloperMode;
        AutoPlayNextCheck.IsChecked = source.AutoPlayNextVideo;
        PreserveRateCheck.IsChecked = source.PreservePlaybackRate;
        LogDaysBox.Text = source.LogRetentionDays.ToString();
        PageTimeoutBox.Text = source.PageTimeoutSeconds.ToString();
        MaxPageTimeoutBox.Text = source.MaxPageTimeoutSeconds.ToString();
        _readyForFeedback = true;
    }

    public AppSettings Result => _settings;

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (!_readyForFeedback || sender is not System.Windows.Controls.CheckBox option)
            return;

        SettingsFeedbackText.Text = $"{option.Content}：{(option.IsChecked == true ? "已开启" : "已关闭")}（保存后生效）";
        SettingsFeedback.Visibility = Visibility.Visible;
    }

    private void SelectTheme(ThemeMode theme)
    {
        foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), theme.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }

        ThemeCombo.SelectedIndex = ThemeCombo.SelectedIndex < 0 ? 0 : ThemeCombo.SelectedIndex;
    }

    private void ClearLogin_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "清除登录状态后，学习通网页需要重新登录。是否继续？",
            "确认清除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ClearLoginRequested = true;
            System.Windows.MessageBox.Show("保存设置后将清除 WebView2 登录状态。", "已标记", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LogDaysBox.Text, out var logDays) || logDays is < 1 or > 3650)
        {
            System.Windows.MessageBox.Show("日志保留天数请输入 1–3650。", "设置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PageTimeoutBox.Text, out var timeout) || timeout is < 5 or > 60)
        {
            System.Windows.MessageBox.Show("页面基础超时请输入 5–60 秒。", "设置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxPageTimeoutBox.Text, out var maxTimeout) || maxTimeout < timeout || maxTimeout > 120)
        {
            System.Windows.MessageBox.Show("页面最大超时必须不小于基础超时，且不超过 120 秒。", "设置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ThemeCombo.SelectedItem is ComboBoxItem themeItem &&
            Enum.TryParse<ThemeMode>(themeItem.Tag?.ToString(), true, out var theme))
            _settings.Theme = theme;

        _settings.EnableInAppNotifications = InAppCheck.IsChecked == true;
        _settings.EnableWindowsNotifications = WindowsNotifyCheck.IsChecked == true;
        _settings.EnableSound = SoundCheck.IsChecked == true;
        _settings.EnableDeveloperMode = DeveloperCheck.IsChecked == true;
        _settings.AutoPlayNextVideo = AutoPlayNextCheck.IsChecked == true;
        _settings.PreservePlaybackRate = PreserveRateCheck.IsChecked == true;
        _settings.LogRetentionDays = logDays;
        _settings.PageTimeoutSeconds = timeout;
        _settings.MaxPageTimeoutSeconds = maxTimeout;

        DialogResult = true;
    }
}
