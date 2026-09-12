using System.Windows;
using System.Windows.Threading;
using ChaoxingLearningAssistant.Services;
using ChaoxingLearningAssistant.Views;

namespace ChaoxingLearningAssistant;

public partial class App : System.Windows.Application
{
    private bool _fatalErrorHandled;
    public static FileLogger Logger { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();
        Logger = new FileLogger(AppPaths.LogDirectory);
        Settings = new SettingsService(AppPaths.SettingsFile, Logger);
        Logger.CleanupOldLogs(Settings.Current.LogRetentionDays);
        Logger.Info("APP-VERSION", "学习通课程视频播放助手 v1.41 (1.41.0)");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 布局错误可能在 MessageBox 的嵌套消息循环中再次发生。
        // 先设置一次性闩锁，保留恢复快照，然后只显示一次致命错误并退出。
        e.Handled = true;
        if (_fatalErrorHandled)
            return;
        _fatalErrorHandled = true;

        try { Logger.Error("APP-UNHANDLED", "发生未处理的 UI 异常，保存恢复状态后退出。", e.Exception); }
        catch { /* 日志磁盘故障不能递归触发另一个错误窗口。 */ }

        if (MainWindow is ChaoxingLearningAssistant.Views.MainWindow window)
            window.PrepareForFatalExit();

        var detail = e.Exception.GetBaseException().Message;
        if (detail.Length > 450) detail = detail[..450] + "…";
        Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
        {
            try
            {
                System.Windows.MessageBox.Show(
                    $"v1.41 遇到无法继续运行的界面错误，程序将关闭。\n\n{detail}\n\n日志目录：{AppPaths.LogDirectory}\n重新启动后可尝试恢复上次学习位置。",
                    "学习通课程视频播放助手 v1.41",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { Shutdown(1); }
        }));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("APP-TASK", "后台任务发生未观察异常。", e.Exception);
        e.SetObserved();
    }
}
