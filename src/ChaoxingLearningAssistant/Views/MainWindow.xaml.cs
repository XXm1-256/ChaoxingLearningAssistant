using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ChaoxingLearningAssistant.Chaoxing;
using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using ChaoxingLearningAssistant.StateMachine;
using ChaoxingLearningAssistant.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace ChaoxingLearningAssistant.Views;

public partial class MainWindow : Window
{
    private enum AdvanceAttemptResult
    {
        NoCandidate,
        Advanced,
        Failed
    }

    private enum CatalogRefreshResult
    {
        Stable,
        Navigated,
        Unstable
    }

    private readonly MainViewModel _vm = new();
    private readonly ThemeService _themeService = new();
    private readonly LearningStatsService _statsService;
    private readonly SessionRecoveryService _sessionService;
    private readonly CourseCacheService _courseCacheService;
    private readonly DiagnosticService _diagnosticService;
    private readonly RunStateMachine _stateMachine;
    private readonly TrayNotificationService _trayService;

    private ChaoxingAdapter? _adapter;
    private DispatcherTimer? _playerTimer;
    private CancellationTokenSource? _navigationTimeoutCts;
    private CancellationTokenSource? _navigationWorkCts;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly PlaybackEndGate _endGate = new();
    private readonly PlaybackWatchTracker _watchTracker = new();
    private readonly NotificationThrottle _notificationThrottle = new();
    private ulong _navigationId;
    private int _pageVersion;
    private long _automationGeneration;
    private bool _isNavigating;
    private bool _pollInProgress;
    private bool _playerCommandInProgress;
    private bool _retryReloadPending;
    private bool _fatalExit;
    private int _navigationRetryCount;
    private bool _automationPaused;
    private bool _handlingEnded;
    private bool _pendingNavigationRequested;
    private bool _pendingPreparationInProgress;
    private bool _isClosing;
    private bool _startAfterNavigation;
    private bool _scanCoursesAfterNavigation;
    private ChapterItem? _pendingNextVideo;
    private readonly HashSet<string> _autoAdvanceVisited = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _courseVerifiedChapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _courseUnresolvedChapters = new(StringComparer.OrdinalIgnoreCase);
    private int _courseTraversalStartIndex = -1;
    private double _lastObservedPlaybackRate = 1.0;
    private SessionSnapshot? _restoreSnapshot;
    private LearningStatRecord? _activeStat;
    private DateTime _lastPlayerPoll = DateTime.MinValue;
    private DateTime _lastChapterRefreshAttempt = DateTime.MinValue;
    private DateTime _lastManualInterventionCheckAt = DateTime.MinValue;
    private string _lastError = string.Empty;
    private PageRecognitionResult _lastRecognition = new();
    private string _lastStableStudyUrl = string.Empty;
    private string _loginRecoveryUrl = string.Empty;
    private bool _loginPageActive;

    // F11 focus mode gives the embedded study page the entire client area.
    private bool _compactViewingMode;
    private Thickness _workspaceMarginBeforeCompact;
    private GridLength _leftColumnBeforeCompact;
    private GridLength _leftSplitterBeforeCompact;
    private GridLength _rightSplitterBeforeCompact;
    private GridLength _telemetryColumnBeforeCompact;
    private Visibility _rightTelemetryVisibilityBeforeCompact;
    private WindowState _windowStateBeforeCompact;
    private bool _focusUnfinishedAfterNavigation;
    private bool _manualChapterOpenInProgress;
    private ChapterItem? _requestedChapter;
    private string _requestedOriginPlayerIdentity = string.Empty;
    private DateTime _requestedChapterAt = DateTime.MinValue;
    private ChapterItem? _manualPlaybackAfterNavigationTarget;
    private PlayerSnapshot? _manualPlaybackOriginSnapshot;
    private string _lastPlayerIdentity = string.Empty;
    private string _pendingOriginPlayerIdentity = string.Empty;
    private PlayerSnapshot? _pendingOriginPlayerSnapshot;
    private PlayerSnapshot? _lastObservedPlayerSnapshot;
    private PlayerSnapshot? _lastEndedPlayerSnapshot;
    private string _lastEndedMediaIdentity = string.Empty;
    private string _lastEndedSource = string.Empty;
    private DateTime _lastObservedPlayingAt = DateTime.MinValue;
    private string _lastObservedPlayingSource = string.Empty;
    private double _lastObservedPlayingPosition;
    private double _lastObservedPlayingDuration;
    private double _lastObservedPlayingRate = 1.0;
    private int _noticeVersion;
    private Storyboard? _ambientMotion;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        _statsService = new LearningStatsService(AppPaths.StatsFile, App.Logger);
        _sessionService = new SessionRecoveryService(AppPaths.SessionFile, App.Logger);
        _courseCacheService = new CourseCacheService(AppPaths.CacheFile, App.Logger);
        _diagnosticService = new DiagnosticService(App.Logger);
        _stateMachine = new RunStateMachine(App.Logger);
        _trayService = new TrayNotificationService();

        _themeService.Apply(App.Settings.Current.Theme);
        DeveloperPanel.Visibility = App.Settings.Current.EnableDeveloperMode
            ? Visibility.Visible
            : Visibility.Collapsed;

        _stateMachine.StateChanged += StateMachine_StateChanged;

        _trayService.OpenRequested += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayService.PauseRequested += (_, _) => Dispatcher.Invoke(() => SetAutomationPaused(true));
        _trayService.ResumeRequested += (_, _) => Dispatcher.Invoke(() => SetAutomationPaused(false));
        _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(() =>
        {
            _isClosing = true;
            Close();
        });

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        _vm.AutomationStatusText = "辅助运行中";
        StateMachine_StateChanged(this, AppRunState.Idle);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                _ambientMotion = (Storyboard)FindResource("AmbientMotion");
                _ambientMotion.Begin(this, true);
            }
            await InitializeWebViewAsync();
            StartPlayerMonitor();
            LoadCachedCourses();
            ContinueLastButton.IsEnabled = !string.IsNullOrWhiteSpace(App.Settings.Current.LastCourseUrl);

            if (!App.Settings.Current.FirstRunCompleted)
            {
                var firstRun = new FirstRunWindow { Owner = this };
                firstRun.ShowDialog();
                App.Settings.Current.FirstRunCompleted = true;
                App.Settings.Save();
            }

            var previous = _sessionService.Load();
            if (previous.WasRunning && !string.IsNullOrWhiteSpace(previous.ChapterUrl))
            {
                var result = System.Windows.MessageBox.Show(
                    $"检测到上次异常退出。\n\n课程：{previous.CourseTitle}\n章节：{previous.ChapterTitle}\n\n是否恢复到上次页面？",
                    "恢复上次学习",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WelcomePanel.Visibility = Visibility.Collapsed;
                    _restoreSnapshot = previous;
                    Navigate(previous.ChapterUrl);
                    return;
                }

                _sessionService.Clear();
            }

            Navigate(ChaoxingConstants.DefaultHomeUrl);
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            App.Logger.Error("CX-WEBVIEW-RUNTIME", "未检测到 WebView2 Runtime。", ex);
            System.Windows.MessageBox.Show(
                "未检测到 Microsoft Edge WebView2 Runtime。Windows 10/11 通常已预装；如被精简系统移除，请先安装 WebView2 Runtime。",
                "WebView2 Runtime 缺失",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _stateMachine.Transition(AppRunState.Stopped, "WebView2 Runtime 缺失");
        }
        catch (Exception ex)
        {
            App.Logger.Error("CX-WEBVIEW-INIT", "WebView2 初始化失败。", ex);
            _lastError = ex.Message;
            System.Windows.MessageBox.Show(
                $"内嵌浏览器初始化失败：{ex.Message}",
                "初始化失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _stateMachine.Transition(AppRunState.Stopped, "初始化失败");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var environmentOptions = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
        };
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: AppPaths.WebViewUserDataDirectory,
            options: environmentOptions);

        await Browser.EnsureCoreWebView2Async(environment);

        // The learning site was designed for a full browser window. A modest default
        // zoom keeps its navigation and task content visible inside the three-pane shell.
        Browser.ZoomFactor = 0.80;

        Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
        Browser.CoreWebView2.Settings.IsZoomControlEnabled = true;
        Browser.CoreWebView2.Profile.IsPasswordAutosaveEnabled = false;

        // Shift + mouse wheel becomes horizontal page movement. The handler only
        // activates while Shift is held, so normal vertical scrolling is untouched.
        await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("""
            (() => {
              if (window.__cxHorizontalWheelInstalled) return;
              window.__cxHorizontalWheelInstalled = true;
              window.addEventListener('wheel', (event) => {
                if (!event.shiftKey) return;
                const candidates = [document.scrollingElement, document.documentElement, document.body]
                  .filter(Boolean)
                  .concat(Array.from(document.querySelectorAll('*')));
                let target = null;
                let overflow = 0;
                for (const el of candidates) {
                  try {
                    const amount = el.scrollWidth - el.clientWidth;
                    if (amount > overflow + 4) {
                      overflow = amount;
                      target = el;
                    }
                  } catch {}
                }
                if (!target) return;
                event.preventDefault();
                target.scrollBy({ left: event.deltaY || event.deltaX, behavior: 'auto' });
              }, { passive: false });
            })();
            """);

        Browser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        Browser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        Browser.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
        Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

        _adapter = new ChaoxingAdapter(Browser, App.Logger);
        _adapter.Attach();

        App.Logger.Info("CX-WEBVIEW-READY",
            $"WebView2 初始化完成；Runtime={Browser.CoreWebView2.Environment.BrowserVersionString}");
    }

    private void StartPlayerMonitor()
    {
        _playerTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _playerTimer.Tick += async (_, _) => await PollPlayerAsync();
        _playerTimer.Start();
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_isClosing) { e.Cancel = true; return; }

        if (_activeStat is not null && _lastObservedPlayerSnapshot?.Found == true)
            FinishActiveStat("切换页面", _lastObservedPlayerSnapshot);

        var previousUrl = Browser.Source?.ToString() ?? string.Empty;
        if (ChaoxingUrlClassifier.IsLoginUri(e.Uri))
        {
            if (!_loginPageActive && ChaoxingUrlClassifier.IsStudyUri(previousUrl))
                _loginRecoveryUrl = previousUrl;
            _loginPageActive = true;
            App.Logger.Warn("CX-LOGIN-REDIRECT", $"导航进入登录页；保留学习页恢复地址：{_loginRecoveryUrl}");
        }
        _navigationId = e.NavigationId;
        _pageVersion++;
        _isNavigating = true;
        _endGate.Reset();
        if (!e.IsRedirected && !_retryReloadPending)
            _navigationRetryCount = 0;
        _retryReloadPending = false;
        if (_pendingNextVideo is not null && !e.IsRedirected &&
            !_pendingNavigationRequested && !ChapterMatchesUri(_pendingNextVideo, e.Uri))
        {
            ClearPendingNextVideo();
        }
        _navigationWorkCts?.Cancel();
        _navigationWorkCts?.Dispose();
        _navigationWorkCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        AddressBox.Text = e.Uri;
        _navigationTimeoutCts?.Cancel();
        _navigationTimeoutCts?.Dispose();
        _navigationTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_navigationWorkCts.Token);

        if (!_automationPaused)
            _stateMachine.Transition(AppRunState.Loading, "网页开始加载");

        _ = MonitorNavigationTimeoutAsync(e.Uri, _navigationTimeoutCts.Token);
        App.Logger.Info("CX-NAV-START", e.Uri);
    }

    private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // 被后续导航替换的完成事件不能取消新页面的计时器、覆盖状态或触发重试。
        if (_isClosing || e.NavigationId != _navigationId)
            return;
        _navigationTimeoutCts?.Cancel();
        _isNavigating = false;
        var pageVersion = _pageVersion;
        var pendingNavigationWasRequested = _pendingNavigationRequested;
        _pendingNavigationRequested = false;

        try
        {
            if (!e.IsSuccess)
            {
                if (e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    App.Logger.Debug("CX-NAV-CANCELLED", "页面导航已取消，不重试。");
                    return;
                }
                await HandleNavigationFailureAsync(e.WebErrorStatus.ToString(), pageVersion);
                return;
            }

            _navigationRetryCount = 0;
            _vm.NetworkText = "正常";
            AddressBox.Text = Browser.Source?.ToString() ?? AddressBox.Text;
            App.Logger.Info("CX-NAV-DONE", AddressBox.Text);

            var currentUrl = Browser.Source?.ToString() ?? string.Empty;
            if (ChaoxingUrlClassifier.IsLoginUri(currentUrl))
            {
                HandleLoginPageReached();
                return;
            }

            if (_loginPageActive)
            {
                _loginPageActive = false;
                if (ChaoxingUrlClassifier.IsStudyUri(currentUrl))
                {
                    _lastStableStudyUrl = currentUrl;
                    _loginRecoveryUrl = string.Empty;
                    App.Logger.Info("CX-LOGIN-RECOVERED", "登录完成后已回到学习页面。 ");
                }
                else if (ChaoxingUrlClassifier.IsGenericHomeUri(currentUrl) &&
                         !string.IsNullOrWhiteSpace(_loginRecoveryUrl) &&
                         ChaoxingUrlClassifier.IsStudyUri(_loginRecoveryUrl))
                {
                    var recoveryUrl = _loginRecoveryUrl;
                    _loginRecoveryUrl = string.Empty;
                    ClearPendingNextVideo();
                    App.Logger.Info("CX-LOGIN-RECOVERY", $"登录完成并回到平台首页，自动返回最近学习页：{recoveryUrl}");
                    Navigate(recoveryUrl);
                    return;
                }
                else
                {
                    App.Logger.Info("CX-LOGIN-EXIT", "已离开登录页，但当前不是学习页或平台首页；保留当前页面，避免跳过学校认证/绑定流程。");
                }
            }

            if (ChaoxingUrlClassifier.IsStudyUri(currentUrl))
                _lastStableStudyUrl = currentUrl;

            await AnalyzeCurrentPageAsync();
            if (!IsCurrentPage(pageVersion)) return;

            if (_focusUnfinishedAfterNavigation && _adapter is not null)
            {
                _focusUnfinishedAfterNavigation = false;
                if (await _adapter.FocusFirstUnfinishedVideoTaskAsync())
                {
                    await Task.Delay(250, _lifetimeCts.Token);
                    if (!IsCurrentPage(pageVersion)) return;
                    await PollPlayerAsync(force: true);
                }
            }

            if (_manualPlaybackAfterNavigationTarget is not null &&
                _manualPlaybackOriginSnapshot is not null && _adapter is not null)
            {
                var target = _manualPlaybackAfterNavigationTarget;
                var origin = _manualPlaybackOriginSnapshot;
                _manualPlaybackAfterNavigationTarget = null;
                _manualPlaybackOriginSnapshot = null;

                // 顶层导航已经提供了目标章节证据，因此即使平台复用了同一媒体地址，
                // 也可以在确认目标页面后继续等待播放器就绪并自动播放。
                var player = await WaitForManualPlayerSwitchAsync(target, origin, allowCurrentPlayer: true);
                if (!IsCurrentPage(pageVersion)) return;
                if (player is not null)
                {
                    await CompleteManualChapterSwitchAsync(target, player);
                }
                else
                {
                    _requestedChapter = null;
                    _requestedOriginPlayerIdentity = string.Empty;
                    _requestedChapterAt = DateTime.MinValue;
                    _stateMachine.Transition(AppRunState.PageRecognitionFailed, "目标章节页面已打开，但没有确认到可播放视频");
                    NotifyUser("章节已打开", $"“{target.DisplayTitle}”已经打开，但暂时没有识别到可播放视频。", false);
                }
            }

            if (_scanCoursesAfterNavigation)
            {
                _scanCoursesAfterNavigation = false;
                await RefreshCoursesInternalAsync(showMessage: true);
                if (!IsCurrentPage(pageVersion)) return;
            }

            if (_startAfterNavigation)
            {
                _startAfterNavigation = false;
                var catalogResult = await RefreshCatalogUntilStatusReadyAsync();
                if (!IsCurrentPage(pageVersion)) return;
                if (catalogResult != CatalogRefreshResult.Stable)
                {
                    if (catalogResult == CatalogRefreshResult.Unstable)
                        NotifyUser("课程目录仍在加载", "章节状态仍在变化，程序不会提前选择目标。页面稳定后可再次点击开始。", false);
                    return;
                }
                var first = FindFirstUnfinishedNavigationCandidate(_vm.Chapters) ??
                            FindFirstPendingNavigationCandidate(_vm.Chapters);

                if (first is not null)
                {
                    if (ChapterMatchesUri(first, Browser.Source?.ToString()))
                    {
                        _pendingNextVideo = first;
                        _vm.NextVideoText = first.Title;
                        await PreparePendingVideoAfterNavigationAsync();
                    }
                    else
                    {
                        await QueueAndOpenNextChapterAsync(first);
                        return;
                    }
                }
            }

            if (_restoreSnapshot is not null)
                await ApplyRestoreSnapshotAsync();
            if (!IsCurrentPage(pageVersion)) return;

            if (_pendingNextVideo is not null &&
                (pendingNavigationWasRequested || ChapterMatchesUri(_pendingNextVideo, Browser.Source?.ToString())))
            {
                await PreparePendingVideoAfterNavigationAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsCurrentPage(pageVersion)) return;
            _lastError = ex.Message;
            App.Logger.Error("CX-NAV-CALLBACK", "页面加载后的处理失败。", ex);
            _stateMachine.Transition(AppRunState.PageRecognitionFailed, "页面处理失败，可刷新后重试");
            NotifyUser("页面处理失败", "请刷新页面重试，详情已记录在日志中。", true);
        }
    }

    private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (_isClosing) return;
        _pageVersion++;
        _isNavigating = false;
        _navigationWorkCts?.Cancel();
        _navigationTimeoutCts?.Cancel();
        _lastError = e.ProcessFailedKind.ToString();
        App.Logger.Error("CX-WEBVIEW-PROCESS", $"WebView2 进程异常：{e.ProcessFailedKind}");
        _stateMachine.Transition(AppRunState.NetworkError, "WebView2 进程异常");
        NotifyUser("浏览器异常", "内嵌浏览器进程发生异常，可尝试刷新页面。", true);
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // 学习通偶尔使用 target=_blank。为保持登录环境，统一在当前 WebView2 中打开。
        e.Handled = true;
        Navigate(e.Uri);
    }

    private async Task MonitorNavigationTimeoutAsync(string uri, CancellationToken token)
    {
        var settings = App.Settings.Current;
        var first = Math.Clamp(settings.PageTimeoutSeconds, 5, 60);
        var max = Math.Clamp(settings.MaxPageTimeoutSeconds, first, 120);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(first), token);
            if (token.IsCancellationRequested)
                return;

            App.Logger.Warn("CX-NAV-SLOW", $"页面加载超过 {first} 秒：{uri}");
            _vm.NetworkText = "加载较慢";

            if (max > first)
                await Task.Delay(TimeSpan.FromSeconds(max - first), token);

            if (token.IsCancellationRequested)
                return;

            App.Logger.Error("CX-NAV-TIMEOUT", $"页面加载超过最大等待 {max} 秒：{uri}");
            _lastError = $"页面加载超时（{max}s）";
            Browser.CoreWebView2?.Stop();
            _stateMachine.Transition(AppRunState.NetworkError, "页面加载超时");
            _vm.NetworkText = "超时";
            NotifyUser("页面加载超时", "页面长时间未完成加载，请检查网络后刷新。", true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_isClosing)
                App.Logger.Warn("CX-NAV-WATCHDOG", $"加载计时器已停止：{ex.Message}");
        }
    }

    private async Task HandleNavigationFailureAsync(string reason, int pageVersion)
    {
        if (!IsCurrentPage(pageVersion)) return;
        _lastError = reason;
        _vm.NetworkText = "异常";
        App.Logger.Warn("CX-NAV-FAIL", $"页面加载失败：{reason}");

        if (_navigationRetryCount < 3 && Browser.CoreWebView2 is not null)
        {
            _navigationRetryCount++;
            _stateMachine.Transition(AppRunState.Recovering, $"网络恢复第 {_navigationRetryCount} 次");
            var delay = TimeSpan.FromSeconds(Math.Pow(2, _navigationRetryCount - 1));
            await Task.Delay(delay, _navigationWorkCts?.Token ?? _lifetimeCts.Token);
            if (!IsCurrentPage(pageVersion)) return;

            try
            {
                _retryReloadPending = true;
                Browser.CoreWebView2.Reload();
                return;
            }
            catch (Exception ex)
            {
                _retryReloadPending = false;
                App.Logger.Warn("CX-NAV-RETRY", $"重载失败：{ex.Message}");
            }
        }

        _stateMachine.Transition(AppRunState.NetworkError, "网络恢复失败");
        NotifyUser("网络异常", "自动重试 3 次后仍无法加载页面，辅助流程已暂停。", true);
        SetAutomationPaused(true);
    }

    private async Task AnalyzeCurrentPageAsync()
    {
        if (_adapter is null || _isClosing || _isNavigating)
            return;
        var pageVersion = _pageVersion;

        try
        {
            var recognition = await _adapter.RecognizePageAsync();
            if (!IsCurrentPage(pageVersion)) return;
            _lastRecognition = recognition;
            var effectiveCourseTitle = ResolveEffectiveCourseTitle(recognition.CourseTitle);
            if (!string.IsNullOrWhiteSpace(effectiveCourseTitle))
            {
                CurrentCourseText.Text = effectiveCourseTitle;
                SyncCurrentCourseCard(effectiveCourseTitle);
                if (IsGenericCourseTitle(App.Settings.Current.LastCourseTitle) ||
                    !string.Equals(App.Settings.Current.LastCourseTitle, effectiveCourseTitle, StringComparison.Ordinal))
                {
                    App.Settings.Current.LastCourseTitle = effectiveCourseTitle;
                }
            }
            else if (IsGenericCourseTitle(CurrentCourseText.Text))
            {
                CurrentCourseText.Text = "正在读取课程…";
            }
            UpdateDeveloperPanel();

            if (_lastRecognition.HasManualIntervention)
            {
                await _adapter.PauseVideoAsync();
                if (!IsCurrentPage(pageVersion)) return;
                _stateMachine.Transition(
                    AppRunState.ManualIntervention,
                    $"检测到需要人工处理：{_lastRecognition.ManualInterventionReason}");
                SetAutomationPaused(true);
                NotifyUser(
                    "需要人工处理",
                    $"页面检测到“{_lastRecognition.ManualInterventionReason}”，辅助流程已暂停。",
                    true);
                return;
            }

            await RefreshChaptersInternalAsync(silent: true);
            if (!IsCurrentPage(pageVersion)) return;
            await PollPlayerAsync(force: true);
            if (!IsCurrentPage(pageVersion)) return;

            if (_vm.Courses.Count == 0)
            {
                var discovered = await ScanCoursesWithRetryAsync(pageVersion, attempts: 3);
                if (!IsCurrentPage(pageVersion)) return;
                foreach (var course in discovered)
                    _vm.Courses.Add(course);
                if (discovered.Count > 0)
                    _courseCacheService.Save(_vm.Courses);
            }

            if (_vm.SelectedCourse is not null)
            {
                App.Settings.Current.LastCourseUrl = _vm.SelectedCourse.Url;
                App.Settings.Current.LastCourseTitle = _vm.SelectedCourse.Title;
                App.Settings.Save();
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            App.Logger.Error("CX-DOM-001", "页面识别失败。", ex);
            _stateMachine.Transition(AppRunState.PageRecognitionFailed, "页面 DOM 识别失败");
            UpdateDeveloperPanel();
        }
    }

    private async Task RefreshCoursesInternalAsync(bool showMessage)
    {
        if (_adapter is null || _isClosing || _isNavigating)
            return;

        var pageVersion = _pageVersion;
        var courses = await ScanCoursesWithRetryAsync(pageVersion, attempts: showMessage ? 3 : 1);
        if (!IsCurrentPage(pageVersion)) return;
        if (courses.Count > 0)
        {
            var selected = _vm.SelectedCourse;
            var refreshed = CourseListRefresh.Merge(_vm.Courses, courses);
            _vm.Courses.Clear();
            foreach (var course in refreshed)
                _vm.Courses.Add(course);
            _vm.SelectedCourse = selected is not null && refreshed.Contains(selected) ? selected : null;
            _courseCacheService.Save(_vm.Courses);
        }

        if (showMessage)
        {
            if (courses.Count == 0)
                NotifyUser("课程识别", _vm.Courses.Count > 0
                    ? "当前页面暂未读取到课程列表，已保留最近一次缓存；进入课程后会继续刷新。"
                    : "当前页面未识别到课程列表，可直接粘贴课程链接或打开学习通课程首页。", false);
            else
                NotifyUser("课程识别", $"识别到 {courses.Count} 个课程候选。", false);
        }
    }

    private async Task<IReadOnlyList<CourseItem>> ScanCoursesWithRetryAsync(int pageVersion, int attempts)
    {
        if (_adapter is null)
            return Array.Empty<CourseItem>();

        IReadOnlyList<CourseItem> courses = Array.Empty<CourseItem>();
        for (var attempt = 0; attempt < Math.Max(1, attempts); attempt++)
        {
            if (attempt > 0)
                await Task.Delay(700);
            if (!IsCurrentPage(pageVersion) || _adapter is null)
                return Array.Empty<CourseItem>();

            courses = await _adapter.ScanCoursesAsync();
            if (courses.Count > 0)
                break;
        }

        return courses;
    }

    private async Task RefreshChaptersInternalAsync(bool silent, PlayerSnapshot? playerEvidence = null, bool preserveSelection = true)
    {
        if (_adapter is null || _isClosing || _isNavigating)
            return;

        var pageVersion = _pageVersion;
        var currentUrl = Browser.Source?.ToString() ?? string.Empty;
        var previousIdentity = _vm.SelectedChapter is null ? string.Empty : ChapterIdentity(_vm.SelectedChapter);
        var chapters = await _adapter.ScanChaptersAsync();
        if (!IsCurrentPage(pageVersion)) return;

        if (chapters.Count == 0 && ChaoxingUrlClassifier.MayContainChapterCatalogUri(currentUrl))
        {
            for (var attempt = 1; attempt <= 4 && chapters.Count == 0; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(450 * attempt), _lifetimeCts.Token);
                if (!IsCurrentPage(pageVersion) || _isNavigating || _isClosing) return;
                chapters = await _adapter.ScanChaptersAsync();
                if (!IsCurrentPage(pageVersion)) return;
            }
        }

        var videoTasks = await _adapter.ScanVideoTasksAsync();
        if (!IsCurrentPage(pageVersion)) return;

        BindVideoTasksToChapters(chapters, videoTasks, playerEvidence, currentUrl);
        _vm.VideoTasks.Clear();
        foreach (var task in videoTasks)
            _vm.VideoTasks.Add(task);

        _vm.Chapters.Clear();
        if (chapters.Count == 0)
        {
            _vm.SelectedChapter = null;
            _vm.CurrentChapterText = "-";
            if (!silent)
                NotifyUser("章节识别", "当前页面未识别到真实课程目录；程序不会再用页面中的视频、资料项或猜测结果填充左侧。", false);
            return;
        }

        foreach (var chapter in chapters)
            _vm.Chapters.Add(chapter);

        ChapterItem? current = null;
        if (playerEvidence?.Found == true)
            current = ResolveChapterFromEvidence(chapters, playerEvidence, currentUrl);

        // 周期刷新目录时，已有“播放器确认章节”优先保持，不让 DOM/URL 的弱证据把左侧抢走。
        if (current is null && preserveSelection && !string.IsNullOrWhiteSpace(previousIdentity))
        {
            current = chapters.FirstOrDefault(x =>
                string.Equals(ChapterIdentity(x), previousIdentity, StringComparison.OrdinalIgnoreCase));
        }


        if (current is not null)
        {
            ConfirmCurrentChapter(current, playerEvidence, updateVideoTitle: playerEvidence?.Found == true);
            _ = Dispatcher.BeginInvoke(() => ChapterList.ScrollIntoView(current), DispatcherPriority.Background);
        }
        else if (!preserveSelection)
        {
            _vm.SelectedChapter = null;
            _vm.CurrentChapterText = "正在读取章节…";
        }

        if (_vm.SelectedCourse is not null)
        {
            var distinctTasks = videoTasks
                .GroupBy(VideoTaskIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
            var videoChapterCount = chapters.Count(x => x.IsVideo);
            var completedChapterCount = chapters.Count(x => x.IsVideo && x.CompletionKnown && x.IsCompleted);
            var completedTaskCount = distinctTasks.Count(x => x.CompletionKnown && x.IsCompleted);
            _vm.SelectedCourse.VideoCount = Math.Max(videoChapterCount, distinctTasks.Length);
            _vm.SelectedCourse.CompletedCount = Math.Min(
                _vm.SelectedCourse.VideoCount,
                Math.Max(completedChapterCount, completedTaskCount));
            if (current is not null)
                _vm.SelectedCourse.LastChapter = current.Title;
            _courseCacheService.Save(_vm.Courses);
        }
    }

    private async Task<CatalogRefreshResult> RefreshCatalogUntilStatusReadyAsync(
        PlayerSnapshot? playerEvidence = null,
        bool preserveSelection = true,
        bool bootstrapWhenEmpty = false)
    {
        string? previousFingerprint = null;
        var stableScanCount = 0;
        var bootstrapAttempted = false;

        // 标题、任务类型和完成状态可能分批到达。至少连续三次读到完全相同的目录状态
        // 才允许选目标，避免同一个按钮多点几次才偶然命中正确章节。
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await RefreshChaptersInternalAsync(silent: true, playerEvidence: playerEvidence, preserveSelection: preserveSelection);
            if (_isClosing || _isNavigating)
                return _isNavigating ? CatalogRefreshResult.Navigated : CatalogRefreshResult.Unstable;

            if (_vm.Chapters.Count == 0 && bootstrapWhenEmpty && !bootstrapAttempted && _adapter is not null)
            {
                bootstrapAttempted = true;
                var pageVersionBeforeBootstrap = _pageVersion;
                _startAfterNavigation = true;
                var opened = await _adapter.BootstrapChapterCatalogAsync();
                App.Logger.Info("CX-CATALOG-BOOTSTRAP", opened
                    ? "目录尚未初始化，已自动打开首个真实章节以加载完整目录。"
                    : "目录尚未初始化，页面中没有找到可安全打开的真实章节。 ");
                if (opened)
                {
                    await Task.Delay(800, _lifetimeCts.Token);
                    if (_isNavigating || !IsCurrentPage(pageVersionBeforeBootstrap))
                        return CatalogRefreshResult.Navigated;
                }
                _startAfterNavigation = false;
                previousFingerprint = null;
                stableScanCount = 0;
                continue;
            }

            var fingerprint = CatalogStatusFingerprint();
            if (string.Equals(fingerprint, previousFingerprint, StringComparison.Ordinal))
                stableScanCount++;
            else
            {
                previousFingerprint = fingerprint;
                stableScanCount = 1;
            }

            if (stableScanCount >= 3)
                return CatalogRefreshResult.Stable;

            await Task.Delay(TimeSpan.FromMilliseconds(550 + attempt * 150), _lifetimeCts.Token);
        }

        App.Logger.Warn("CX-CATALOG-UNSTABLE", "章节目录在限定等待时间内仍持续变化，本轮不确认完成也不自动选择目标。");
        return CatalogRefreshResult.Unstable;
    }

    private string CatalogStatusFingerprint()
    {
        var chapters = _vm.Chapters
            .OrderBy(x => x.Index)
            .Select(x => $"{ChapterIdentity(x)}:{x.TaskType}:{x.CompletionKnown}:{x.IsCompleted}");
        var tasks = _vm.VideoTasks
            .OrderBy(x => x.Index)
            .Select(x => $"{VideoTaskIdentity(x)}:{x.CompletionKnown}:{x.IsCompleted}");
        return string.Join("|", chapters.Concat(tasks));
    }

    private async Task PollPlayerAsync(bool force = false)
    {
        if (_isClosing || _isNavigating || _pollInProgress || _adapter is null || Browser.CoreWebView2 is null || _loginPageActive)
            return;

        if (!force && DateTime.Now - _lastPlayerPoll < TimeSpan.FromSeconds(1))
            return;
        _lastPlayerPoll = DateTime.Now;
        _pollInProgress = true;
        var pageVersion = _pageVersion;

        try
        {
            if (DateTime.Now - _lastManualInterventionCheckAt >= TimeSpan.FromSeconds(2))
            {
                _lastManualInterventionCheckAt = DateTime.Now;
                var recognition = await _adapter.RecognizePageAsync();
                if (!IsCurrentPage(pageVersion)) return;
                _lastRecognition = recognition;
                if (recognition.HasManualIntervention)
                {
                    await _adapter.PauseVideoAsync();
                    if (!IsCurrentPage(pageVersion)) return;
                    SetAutomationPaused(true);
                    _stateMachine.Transition(AppRunState.ManualIntervention,
                        $"播放期间检测到需要人工处理：{recognition.ManualInterventionReason}");
                    NotifyUser("需要人工处理",
                        $"页面出现“{recognition.ManualInterventionReason}”，辅助流程已暂停。处理完成后可重新开启。",
                        true);
                    return;
                }
            }

            if (_vm.Chapters.Count == 0 &&
                ChaoxingUrlClassifier.MayContainChapterCatalogUri(Browser.Source?.ToString()) &&
                DateTime.Now - _lastChapterRefreshAttempt >= TimeSpan.FromSeconds(8))
            {
                _lastChapterRefreshAttempt = DateTime.Now;
                await RefreshChaptersInternalAsync(silent: true);
                if (!IsCurrentPage(pageVersion)) return;
            }

            var snapshot = await _adapter.GetPlayerSnapshotAsync();
            if (!IsCurrentPage(pageVersion)) return;
            if (!snapshot.Found)
            {
                _vm.PlaybackStatusText = "未检测到视频";
                UpdateCompositeStatus();
                _vm.PlayerTimeText = "00:00 / 00:00";
                _vm.ProgressPercent = 0;
                _vm.CurrentVideoText = "-";
                _vm.NextVideoText = "等待视频载入…";
                SaveSession(snapshot);
                return;
            }

            var catalogRefreshInterval = _vm.SelectedChapter is null
                ? TimeSpan.FromSeconds(2)
                : TimeSpan.FromSeconds(8);
            var catalogRefreshed = false;
            if (DateTime.Now - _lastChapterRefreshAttempt >= catalogRefreshInterval)
            {
                _lastChapterRefreshAttempt = DateTime.Now;
                await RefreshChaptersInternalAsync(
                    silent: true,
                    playerEvidence: snapshot,
                    preserveSelection: _vm.SelectedChapter is not null);
                if (!IsCurrentPage(pageVersion)) return;
                catalogRefreshed = true;
            }

            var previousPlayerSnapshot = _lastObservedPlayerSnapshot;
            var playerIdentity = PlayerIdentity(snapshot);
            var playerChanged = !string.IsNullOrWhiteSpace(playerIdentity) &&
                                (previousPlayerSnapshot?.Found != true ||
                                 !PlayerMediaEvidence.IsSameMedia(previousPlayerSnapshot, snapshot));
            if (playerChanged)
            {
                if (_activeStat is not null && previousPlayerSnapshot?.Found == true)
                    FinishActiveStat("切换视频", previousPlayerSnapshot);
                var previous = _lastPlayerIdentity;
                _lastPlayerIdentity = playerIdentity;
                App.Logger.Info("CX-PLAYER-SWITCH", $"播放器身份变化：{ShortIdentity(previous)} -> {ShortIdentity(playerIdentity)}");
                if (!catalogRefreshed)
                {
                    _lastChapterRefreshAttempt = DateTime.Now;
                    await RefreshChaptersInternalAsync(silent: true, playerEvidence: snapshot, preserveSelection: false);
                    if (!IsCurrentPage(pageVersion)) return;
                }
                if (_vm.SelectedChapter is null && _requestedChapter is not null &&
                    await PlayerMatchesTargetChapterAsync(_requestedChapter, snapshot, allowCatalogEvidence: true))
                {
                    ConfirmCurrentChapter(_requestedChapter, snapshot, updateVideoTitle: true);
                }
            }
            else if (_vm.SelectedChapter is null)
            {
                var resolved = ResolveChapterFromEvidence(_vm.Chapters, snapshot, Browser.Source?.ToString() ?? string.Empty);
                if (resolved is not null)
                    ConfirmCurrentChapter(resolved, snapshot, updateVideoTitle: false);
            }
            _lastObservedPlayerSnapshot = snapshot;

            if (_requestedChapter is not null && _requestedChapterAt != DateTime.MinValue &&
                DateTime.Now - _requestedChapterAt > TimeSpan.FromSeconds(10) &&
                string.Equals(playerIdentity, _requestedOriginPlayerIdentity, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.Warn("CX-MANUAL-CHAPTER-TIMEOUT", $"章节请求超时且播放器未变化：{_requestedChapter.DisplayTitle}");
                _requestedChapter = null;
                _requestedOriginPlayerIdentity = string.Empty;
                _requestedChapterAt = DateTime.MinValue;
            }

            _lastObservedPlaybackRate = snapshot.PlaybackRate > 0 ? snapshot.PlaybackRate : _lastObservedPlaybackRate;
            _vm.PlayerTimeText = snapshot.TimeText;
            _vm.PlaybackRateText = $"{snapshot.PlaybackRate:0.##}x";
            _vm.ProgressPercent = snapshot.ProgressPercent;
            var playerReady = PlayerMediaEvidence.IsReadyForPlayback(snapshot);
            var currentVideoTask = playerReady ? ResolveVideoTaskFromPlayer(snapshot) : null;
            _vm.CurrentVideoText = playerReady
                ? ResolvePlayerVideoTitle(snapshot, currentVideoTask)
                : "视频正在载入…";
            _vm.NextVideoText = _pendingNextVideo?.DisplayTitle ?? (playerReady
                ? NextVideoPreviewResolver.Resolve(_vm.Chapters, _vm.SelectedChapter, currentVideoTask)
                : NextVideoPreviewResolver.IdentifyingText);

            if (_pendingNextVideo is not null &&
                IsConfirmedChapter(_pendingNextVideo) &&
                !App.Settings.Current.AutoPlayNextVideo)
            {
                if (!snapshot.Paused) await _adapter.PauseVideoAsync();
                if (!IsCurrentPage(pageVersion)) return;
                _stateMachine.Transition(AppRunState.WaitingForUser, "等待用户确认下一节");
                SaveSession(snapshot);
                return;
            }

            if (!snapshot.Paused && !snapshot.Ended)
            {
                _lastObservedPlayingAt = DateTime.Now;
                _lastObservedPlayingSource = snapshot.Source ?? string.Empty;
                _lastObservedPlayingPosition = snapshot.CurrentTime;
                _lastObservedPlayingDuration = snapshot.Duration;
                _lastObservedPlayingRate = snapshot.PlaybackRate > 0 ? snapshot.PlaybackRate : _lastObservedPlayingRate;
            }

            var naturalEnded = IsNaturalPlaybackEnd(snapshot);
            if (!naturalEnded)
                _endGate.Reset();

            if (naturalEnded)
            {
                _vm.PlaybackStatusText = "视频已结束";
                UpdateCompositeStatus();
                FinishActiveStat("正常结束", snapshot);

                if (!_automationPaused &&
                    !_handlingEnded &&
                    _stateMachine.Current != AppRunState.WaitingForUser &&
                    _pendingNextVideo is null &&
                    _endGate.TryHandle($"{Browser.Source}|{PlayerMediaEvidence.StableTaskIdentity(snapshot)}", naturalEnded))
                {
                    await HandleVideoEndedAsync(snapshot);
                }
            }
            else if (snapshot.Paused)
            {
                _watchTracker.Sample(playerIdentity, false, DateTimeOffset.Now);
                _vm.PlaybackStatusText = "视频已暂停";
                UpdateCompositeStatus();
                if (_stateMachine.Current is not AppRunState.WaitingForUser
                    and not AppRunState.AutomationPaused
                    and not AppRunState.ManualIntervention
                    and not AppRunState.Loading)
                {
                    _stateMachine.Transition(AppRunState.VideoPaused, "播放器暂停");
                }
            }
            else
            {
                _vm.PlaybackStatusText = "视频播放中";
                UpdateCompositeStatus();
                StartActiveStatIfNeeded(snapshot);
                _watchTracker.Sample(playerIdentity, true, DateTimeOffset.Now);
                if (!_automationPaused)
                    _stateMachine.Transition(AppRunState.Playing, "检测到视频正在播放");
            }

            if (!IsCurrentPage(pageVersion)) return;
            SaveSession(snapshot);
            UpdateDeveloperPanel();
        }
        catch (Exception ex)
        {
            if (_isClosing) return;
            _lastError = ex.Message;
            App.Logger.Debug("CX-VIDEO-POLL", $"播放器状态读取失败：{ex.Message}");
            UpdateDeveloperPanel();
        }
        finally { _pollInProgress = false; }
    }

    private async Task HandleVideoEndedAsync(PlayerSnapshot snapshot)
    {
        if (_adapter is null)
            return;

        _handlingEnded = true;
        var pageVersion = _pageVersion;
        var operationGeneration = BeginAutomationOperation();
        try
        {
            if (snapshot.PlaybackRate > 0)
                _lastObservedPlaybackRate = snapshot.PlaybackRate;
            _lastEndedSource = snapshot.Source ?? string.Empty;
            _lastEndedMediaIdentity = PlayerIdentity(snapshot);
            _lastEndedPlayerSnapshot = snapshot;

            _stateMachine.Transition(AppRunState.Ended, "当前视频自然结束");
            _stateMachine.Transition(AppRunState.FindingNextVideo, "寻找下一章节 / 视频");

            // 必须先按当前章节内部的任务顺序寻找下一条真实视频。
            // 测验、作业、签到和考试都不参与候选，章节内视频没有播完时绝不提前切章。
            if (App.Settings.Current.AutoPlayNextVideo)
            {
                var withinChapter = await TryAdvanceWithinCurrentChapterAsync(snapshot, operationGeneration);
                if (withinChapter == AdvanceAttemptResult.Advanced)
                    return;
                if (withinChapter == AdvanceAttemptResult.Failed)
                {
                    StopAfterUnconfirmedNextVideo("本章节仍有下一视频，但学习通没有确认切换成功。程序不会跳过该视频或把本章记为完成。");
                    return;
                }
            }

            // DOM 任务结构无法识别时，只允许点击播放器明确标注为“下一视频”的控件。
            // “下一节/下一个任务”可能进入测试题，不能作为安全的自动续播路径。
            if (App.Settings.Current.AutoPlayNextVideo)
            {
                var platformNext = await TryAdvanceUsingPlatformNextControlAsync(snapshot, operationGeneration);
                if (platformNext == AdvanceAttemptResult.Advanced)
                    return;
                if (platformNext == AdvanceAttemptResult.Failed)
                {
                    StopAfterUnconfirmedNextVideo("学习通显示了下一视频按钮，但没有确认切换成功。程序不会把本章记为完成。");
                    return;
                }
            }

            var previousIndex = _vm.SelectedChapter?.Index ?? -1;
            var previousChapterId = _vm.SelectedChapter?.ChapterId;
            if (string.IsNullOrWhiteSpace(previousChapterId)) previousChapterId = snapshot.ChapterId;
            if (string.IsNullOrWhiteSpace(previousChapterId)) previousChapterId = ExtractChapterId(snapshot.DocumentUrl);
            if (string.IsNullOrWhiteSpace(previousChapterId)) previousChapterId = ExtractChapterId(Browser.Source?.ToString());

            await RefreshChaptersInternalAsync(silent: true, playerEvidence: snapshot, preserveSelection: true);
            if (!IsCurrentPage(pageVersion) || _automationPaused) return;

            var endedChapter = ResolveChapterFromEvidence(_vm.Chapters, snapshot, Browser.Source?.ToString() ?? string.Empty);
            if (endedChapter is not null)
            {
                _vm.SelectedChapter = endedChapter;
                _vm.CurrentChapterText = endedChapter.DisplayTitle;
                previousIndex = endedChapter.Index;
                if (!string.IsNullOrWhiteSpace(endedChapter.ChapterId))
                    previousChapterId = endedChapter.ChapterId;
            }
            else if (!string.IsNullOrWhiteSpace(previousChapterId))
            {
                var current = _vm.Chapters.FirstOrDefault(x =>
                    string.Equals(x.ChapterId, previousChapterId, StringComparison.OrdinalIgnoreCase));
                if (current is not null)
                {
                    _vm.SelectedChapter = current;
                    _vm.CurrentChapterText = current.DisplayTitle;
                    previousIndex = current.Index;
                }
            }

            EnsureCourseTraversalStarted(previousIndex);
            if (_vm.SelectedChapter is not null)
            {
                _courseVerifiedChapters.Add(ChapterIdentity(_vm.SelectedChapter));
                _courseUnresolvedChapters.Remove(ChapterIdentity(_vm.SelectedChapter));
                App.Logger.Info("CX-COURSE-CHAPTER-VERIFIED", $"当前章节全部可达视频已处理：{_vm.SelectedChapter.Title}");
            }

            _autoAdvanceVisited.Clear();
            if (_vm.SelectedChapter is not null)
                _autoAdvanceVisited.Add(ChapterIdentity(_vm.SelectedChapter));

            var next = FindNextSequentialNavigationCandidate(previousIndex);
            while (next is not null && IsCurrentChapterIdentity(next, previousChapterId))
            {
                _autoAdvanceVisited.Add(ChapterIdentity(next));
                next = FindNextSequentialNavigationCandidate(next.Index);
            }
            if (next is null)
            {
                next = await _adapter.FindNextChapterCandidateAsync(previousChapterId);
                if (!IsCurrentPage(pageVersion) || _automationPaused) return;
                if (next is not null && IsCurrentChapterIdentity(next, previousChapterId))
                {
                    App.Logger.Warn("CX-AUTO-NEXT-SAME", $"网页返回的下一候选仍是当前章节，已拒绝重播：{next.Title}");
                    next = null;
                }
                if (next is not null)
                {
                    next.Index = previousIndex >= 0 ? previousIndex + 1 : 0;
                    App.Logger.Info("CX-AUTO-NEXT-FALLBACK", $"左侧目录未给出下一项，改用网页原生目录顺序定位：{next.Title}");
                }
            }

            if (next is null)
            {
                await VerifyCourseCompletionOrContinueAsync(snapshot, previousIndex);
                return;
            }

            await QueueAndOpenNextChapterAsync(next);
        }
        finally
        {
            _handlingEnded = false;
        }
    }


    private bool IsNaturalPlaybackEnd(PlayerSnapshot snapshot)
        => PlaybackEndDetector.IsNaturalEnd(
            snapshot,
            DateTime.Now,
            _lastObservedPlayingAt,
            _lastObservedPlayingSource,
            _lastObservedPlayingPosition,
            _lastObservedPlayingDuration,
            _lastObservedPlayingRate,
            _playerTimer?.Interval ?? TimeSpan.FromMilliseconds(1500));

    private async Task<AdvanceAttemptResult> TryAdvanceUsingPlatformNextControlAsync(
        PlayerSnapshot endedSnapshot,
        long operationGeneration)
    {
        if (_adapter is null || _isClosing || _automationPaused)
            return AdvanceAttemptResult.NoCandidate;

        var submitted = await _adapter.ClickNativeNextVideoControlAsync();
        if (!submitted)
            return AdvanceAttemptResult.NoCandidate;

        App.Logger.Info("CX-AUTO-NEXT-PLATFORM", "已点击学习通播放器明确标注的下一视频控件，等待真实播放器变化。 ");
        var advanced = await WaitForRealAutoAdvanceAsync(
            endedSnapshot,
            "CX-AUTO-NEXT-PLATFORM",
            "学习通播放器下一视频控件",
            operationGeneration: operationGeneration);
        return advanced ? AdvanceAttemptResult.Advanced : AdvanceAttemptResult.Failed;
    }

    private async Task<bool> WaitForRealAutoAdvanceAsync(
        PlayerSnapshot endedSnapshot,
        string logCode,
        string sourceLabel,
        Func<Task<bool>>? retryTargetAction = null,
        long? operationGeneration = null,
        PlayerSnapshot? expectedTarget = null)
    {
        if (_adapter is null) return false;
        var pageVersion = _pageVersion;
        var expectedGeneration = operationGeneration ?? _automationGeneration;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (_isClosing || _automationPaused || !IsCurrentPage(pageVersion) ||
                !IsAutomationOperationCurrent(expectedGeneration))
                return false;

            await Task.Delay(350, _lifetimeCts.Token);
            if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration))
                return false;
            var candidate = await _adapter.GetPlayerSnapshotAsync(expectedTarget);
            if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration))
                return false;

            if (retryTargetAction is not null && attempt is 5 or 14 or 23 &&
                (!candidate.Found || candidate.Ended || IsSameMediaEvidence(endedSnapshot, candidate) ||
                 !PlayerMediaEvidence.IsReadyForPlayback(candidate)))
            {
                await retryTargetAction();
                if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration))
                    return false;
                App.Logger.Debug(logCode, $"{sourceLabel}尚未形成可播放的新播放器，重新激活目标任务点；attempt={attempt + 1}");
            }

            if (!candidate.Found || candidate.Ended || IsSameMediaEvidence(endedSnapshot, candidate))
                continue;

            if (!PlayerMediaEvidence.IsReadyForPlayback(candidate))
                continue;

            var playback = await StartPlaybackWithRetryAsync(candidate, sourceLabel);
            if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration))
                return false;
            var playing = playback.Playing;
            candidate = playback.Snapshot;

            _lastPlayerIdentity = PlayerIdentity(candidate);
            _vm.CurrentVideoText = !string.IsNullOrWhiteSpace(candidate.VideoTitle)
                ? candidate.VideoTitle
                : "学习通已切换到下一视频";
            _vm.PlayerTimeText = candidate.TimeText;
            _vm.ProgressPercent = candidate.ProgressPercent;
            _vm.PlaybackRateText = $"{candidate.PlaybackRate:0.##}x";
            _vm.PlaybackStatusText = playing ? "视频播放中" : "下一视频已打开";
            UpdateCompositeStatus();

            await RefreshChaptersInternalAsync(silent: true, playerEvidence: candidate, preserveSelection: false);
            _vm.CurrentVideoText = ResolvePlayerVideoTitle(candidate, ResolveVideoTaskFromPlayer(candidate));
            var current = ResolveChapterFromEvidence(_vm.Chapters, candidate, Browser.Source?.ToString() ?? string.Empty);
            if (current is not null)
                ConfirmCurrentChapter(current, candidate, updateVideoTitle: true);

            _lastEndedSource = string.Empty;
            _lastEndedMediaIdentity = string.Empty;
            _lastObservedPlayingAt = playing ? DateTime.Now : DateTime.MinValue;
            _endGate.Reset();
            _stateMachine.Transition(playing ? AppRunState.Playing : AppRunState.VideoPaused,
                playing ? $"{sourceLabel}已真实切换并播放" : $"{sourceLabel}已真实切换，浏览器未允许自动播放");
            App.Logger.Info(logCode,
                $"学习通网页播放器真实推进：{ShortIdentity(PlayerIdentity(endedSnapshot))} -> {ShortIdentity(PlayerIdentity(candidate))}；playing={playing}");
            if (!playing)
                NotifyUser("下一视频已打开", "学习通网页已经切到下一视频，但浏览器阻止了自动播放；请直接点击网页播放。", false);
            return true;
        }

        App.Logger.Warn(logCode, $"{sourceLabel}动作已提交，但约 10 秒内学习通真实播放器没有变化；继续其他导航路径。 ");
        return false;
    }

    private async Task<AdvanceAttemptResult> TryAdvanceWithinCurrentChapterAsync(
        PlayerSnapshot endedSnapshot,
        long operationGeneration)
    {
        if (_adapter is null || _isClosing || _automationPaused)
            return AdvanceAttemptResult.NoCandidate;

        VideoTaskItem? nextTask = null;
        try
        {
            var freshTasks = await _adapter.ScanVideoTasksAsync();
            var currentTask = ResolveVideoTaskFromPlayer(endedSnapshot, freshTasks);
            var currentChapter = ResolveChapterFromEvidence(
                _vm.Chapters,
                endedSnapshot,
                Browser.Source?.ToString() ?? string.Empty) ?? _vm.SelectedChapter;
            if (currentTask is not null && currentChapter is not null)
            {
                var chapterTasks = freshTasks
                    .Where(x => VideoTaskBelongsToChapter(x, currentChapter))
                    .OrderBy(x => x.Index)
                    .ToArray();
                var currentIndex = Array.FindIndex(chapterTasks, x =>
                    ReferenceEquals(x, currentTask) ||
                    string.Equals(VideoTaskIdentity(x), VideoTaskIdentity(currentTask), StringComparison.OrdinalIgnoreCase));
                if (currentIndex >= 0)
                {
                    nextTask = chapterTasks
                        .Skip(currentIndex + 1)
                        .FirstOrDefault(x => !x.CompletionKnown || !x.IsCompleted);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.Debug("CX-AUTO-NEXT-IN-CHAPTER", $"同章目标预扫描失败，继续使用页面相对顺序：{ex.Message}");
        }

        var submitted = await _adapter.AdvanceToNextVideoTaskAsync(
            endedSnapshot.MediaId,
            endedSnapshot.Source,
            nextTask?.MediaId,
            nextTask?.DocumentUrl,
            nextTask?.Source);
        if (!submitted)
            return nextTask is null ? AdvanceAttemptResult.NoCandidate : AdvanceAttemptResult.Failed;

        App.Logger.Info("CX-AUTO-NEXT-IN-CHAPTER",
            nextTask is null
                ? "已按页面相对顺序提交当前章节内下一视频切换，等待真实播放器核验。"
                : $"已按扫描证据提交同章下一视频：{nextTask.DisplayTitle}；等待真实播放器核验。");
        Func<Task<bool>> retryTargetAction = () => _adapter.AdvanceToNextVideoTaskAsync(
                endedSnapshot.MediaId,
                endedSnapshot.Source,
                nextTask?.MediaId,
                nextTask?.DocumentUrl,
                nextTask?.Source);
        var expectedTarget = nextTask is not null && nextTask.DomIndex >= 0
            ? new PlayerSnapshot
            {
                Found = true,
                TaskKey = nextTask.TaskKey,
                ChapterId = nextTask.ChapterId,
                DocumentUrl = nextTask.DocumentUrl,
                Source = nextTask.Source,
                MediaId = nextTask.MediaId,
                DomIndex = nextTask.DomIndex
            }
            : null;
        var advanced = await WaitForRealAutoAdvanceAsync(
            endedSnapshot,
            "CX-AUTO-NEXT-IN-CHAPTER",
            "同章节下一视频任务点",
            retryTargetAction,
            operationGeneration,
            expectedTarget);
        return advanced ? AdvanceAttemptResult.Advanced : AdvanceAttemptResult.Failed;
    }

    private async Task<(bool Playing, PlayerSnapshot Snapshot)> StartPlaybackWithRetryAsync(
        PlayerSnapshot initial,
        string sourceLabel)
    {
        if (_adapter is null)
            return (false, initial);

        var pageVersion = _pageVersion;
        var expectedGeneration = _automationGeneration;
        var target = initial;
        var current = initial;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (_isClosing || _automationPaused || !IsCurrentPage(pageVersion) ||
                !IsAutomationOperationCurrent(expectedGeneration))
                return (false, current);

            if (current.Found && !current.Ended && !current.Paused &&
                PlayerMediaEvidence.MatchesPlaybackTarget(target, current))
                return (true, current);

            if (App.Settings.Current.PreservePlaybackRate &&
                _lastObservedPlaybackRate > 0 &&
                Math.Abs(_lastObservedPlaybackRate - current.PlaybackRate) > 0.001)
            {
                await _adapter.RestorePlaybackRateUsingUiAsync(_lastObservedPlaybackRate);
                if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration)) return (false, current);
            }

            var submitted = await _adapter.PlayVideoAsync(target);
            if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration)) return (false, current);
            await Task.Delay(submitted ? 250 : 400, _lifetimeCts.Token);
            if (!IsCurrentPage(pageVersion) || !IsAutomationOperationCurrent(expectedGeneration)) return (false, current);

            var verified = await _adapter.GetPlayerSnapshotAsync(target);
            if (verified.Found && PlayerMediaEvidence.MatchesPlaybackTarget(target, verified))
                current = verified;
            if (current.Found && !current.Ended && !current.Paused &&
                PlayerMediaEvidence.MatchesPlaybackTarget(target, current))
            {
                App.Logger.Info("CX-AUTO-PLAY", $"{sourceLabel}自动播放确认成功；attempt={attempt + 1}");
                return (true, current);
            }

            if (attempt < 3)
                await Task.Delay(300, _lifetimeCts.Token);
        }

        App.Logger.Warn("CX-AUTO-PLAY", $"{sourceLabel}已切换播放器，但 4 次播放确认均未成功。 ");
        return (false, current);
    }

    private static bool IsSameMediaEvidence(PlayerSnapshot a, PlayerSnapshot b)
        => PlayerMediaEvidence.IsSameMedia(a, b);

    private async Task QueueAndOpenNextChapterAsync(ChapterItem next)
    {
        if (_adapter is null || _isClosing || _automationPaused)
            return;

        _pendingNextVideo = next;
        _pendingOriginPlayerIdentity = !string.IsNullOrWhiteSpace(_lastPlayerIdentity)
            ? _lastPlayerIdentity
            : _lastEndedMediaIdentity;
        _pendingOriginPlayerSnapshot = _lastObservedPlayerSnapshot;
        _vm.NextVideoText = next.Title;
        _vm.CanContinueNext = false;
        ContinueNextButton.Visibility = Visibility.Collapsed;
        _stateMachine.Transition(AppRunState.PreloadingNextVideo, $"请求切换下一章节：{next.Title}");

        var beforePageVersion = _pageVersion;
        _pendingNavigationRequested = true;
        var openedByPage = await _adapter.OpenChapterAsync(next);
        if (_isClosing || _automationPaused || _pendingNextVideo != next) return;

        if (openedByPage)
        {
            await Task.Delay(700, _lifetimeCts.Token);
            if (_isClosing || _automationPaused || _pendingNextVideo != next) return;

            if (_pageVersion == beforePageVersion)
            {
                _pendingNavigationRequested = false;
                await PreparePendingVideoAfterNavigationAsync();
            }
            return;
        }

        _pendingNavigationRequested = false;

        // DOM click 没有产生导航时，优先用当前已登录学习页保留全部鉴权参数，仅替换目标章节 ID。
        // 这比旧版残缺 synthetic URL 更可靠，也能解决“目录高亮变了但学习通视频完全没换”。
        var contextUrl = await _adapter.BuildContextPreservingChapterUrlAsync(next);
        if (!string.IsNullOrWhiteSpace(contextUrl) && !UriEquivalent(contextUrl, Browser.Source?.ToString()))
        {
            App.Logger.Info("CX-AUTO-NEXT-DIRECT", $"目录 click 未切播放器，改用当前学习上下文真实导航：{next.Title}");
            _pendingNavigationRequested = true;
            _focusUnfinishedAfterNavigation = true;
            Navigate(contextUrl);
            return;
        }

        if (!next.IsSyntheticUrl && IsSafeChapterFallbackUrl(next.Url))
        {
            App.Logger.Info("CX-AUTO-NEXT-HREF", $"上下文导航不可用，改用网页真实 href：{next.Title}");
            _pendingNavigationRequested = true;
            _focusUnfinishedAfterNavigation = true;
            Navigate(next.Url);
            return;
        }

        if (next.IsSyntheticUrl)
            App.Logger.Warn("CX-CHAPTER-NAV", $"旧式残缺 synthetic URL 不直接使用：{next.Title}");
        await HandlePendingVideoUnavailableAsync(next, "网页目录点击、原生动作与真实学习页导航均未产生有效切换");
    }

    private async Task PreparePendingVideoAfterNavigationAsync()
    {
        if (_adapter is null || _pendingNextVideo is null || _pendingPreparationInProgress)
            return;

        _pendingPreparationInProgress = true;
        var pending = _pendingNextVideo;
        try
        {
            await Task.Delay(250, _lifetimeCts.Token);
            if (_isClosing || _automationPaused || _pendingNextVideo != pending) return;

            var player = await WaitForPendingPlayerSwitchAsync(pending, _pendingOriginPlayerSnapshot);
            if (_isClosing || _automationPaused || _pendingNextVideo != pending) return;

            if (player is null || !player.Found)
            {
                // 普通 DOM click 可能只改变目录高亮。再直接调用页面原生 toOld(...) 章节函数一次，
                // 然后仍以学习通真实播放器变化作为唯一成功标准。
                var invokedNative = await _adapter.InvokeNativeChapterActionAsync(pending);
                if (invokedNative)
                    player = await WaitForPendingPlayerSwitchAsync(pending, _pendingOriginPlayerSnapshot);
            }

            if (player is null || !player.Found)
            {
                // toOld/DOM click 仍只改目录高亮时，不在这里停下来报错。
                // 直接使用当前已登录 studentstudy 地址的完整上下文，只替换 chapterId，再让 WebView2 真正导航。
                var contextUrl = await _adapter.BuildContextPreservingChapterUrlAsync(pending);
                if (!string.IsNullOrWhiteSpace(contextUrl) && !UriEquivalent(contextUrl, Browser.Source?.ToString()))
                {
                    App.Logger.Info("CX-AUTO-NEXT-DIRECT", $"原生章节动作未切播放器，升级为真实学习页导航：{pending.Title}");
                    _pendingNavigationRequested = true;
                    _focusUnfinishedAfterNavigation = true;
                    Navigate(contextUrl);
                    return;
                }

                if (!pending.IsSyntheticUrl && IsSafeChapterFallbackUrl(pending.Url) && !UriEquivalent(pending.Url, Browser.Source?.ToString()))
                {
                    App.Logger.Info("CX-AUTO-NEXT-HREF", $"上下文导航不可用，改用目录真实 href：{pending.Title}");
                    _pendingNavigationRequested = true;
                    _focusUnfinishedAfterNavigation = true;
                    Navigate(pending.Url);
                    return;
                }

                await HandlePendingVideoUnavailableAsync(pending, "DOM click、完整 toOld 参数和真实学习页导航均未切出新播放器");
                return;
            }

            // 目录的“章节未完成”也可能只代表测验或作业。进入候选章节后必须重新读取真实视频状态；
            // 若视频全部完成，直接继续后续章节，不能把当前已完成播放器重新播放。
            var taskScan = await ScanTargetVideoTasksUntilStableAsync(pending);
            if (_isClosing || _automationPaused || _pendingNextVideo != pending) return;
            if (!taskScan.Stable)
            {
                await HandlePendingVideoUnavailableAsync(pending, "目标章节的视频任务仍在分批加载");
                return;
            }

            var targetTasks = taskScan.Tasks;
            if (targetTasks.Length > 0 && CoursePlaybackPlan.AllKnownVideosCompleted(targetTasks))
            {
                await SkipChapterWithCompletedVideosAsync(pending);
                return;
            }

            // 章节本身可能包含多个视频。章节切换确认后再把真实页面聚焦到该章节第一条待核验视频，
            // 避免只进入章节却停留在该章节已完成的第一条视频。
            if (targetTasks.Any(x => !x.CompletionKnown || !x.IsCompleted) &&
                await _adapter.FocusFirstUnfinishedVideoTaskAsync())
            {
                await Task.Delay(250, _lifetimeCts.Token);
                if (_isClosing || _automationPaused || _pendingNextVideo != pending) return;
                var focused = await _adapter.GetPlayerSnapshotAsync();
                if (PlayerMediaEvidence.IsReadyForPlayback(focused) && !IsStaleEndedPlayer(focused) &&
                    await PlayerMatchesTargetChapterAsync(pending, focused, allowCatalogEvidence: true))
                {
                    player = focused;
                }
            }

            ConfirmCurrentChapter(pending, player, updateVideoTitle: true);

            if (!App.Settings.Current.AutoPlayNextVideo)
            {
                await _adapter.PauseVideoAsync();
                if (_pendingNextVideo != pending) return;
                ShowContinueNextFallback(pending, "下一节已真实切换，等待确认播放", chapterAlreadyConfirmed: true);
                return;
            }

            await ContinuePendingVideoAsync(pending, userInitiated: false, player);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _pendingPreparationInProgress = false;
        }
    }

    private async Task SkipChapterWithCompletedVideosAsync(ChapterItem completedChapter)
    {
        if (_adapter is null || _pendingNextVideo != completedChapter || _isClosing)
            return;

        var identity = ChapterIdentity(completedChapter);
        _courseVerifiedChapters.Add(identity);
        _courseUnresolvedChapters.Remove(identity);
        _autoAdvanceVisited.Add(identity);
        App.Logger.Info("CX-COURSE-VIDEOS-COMPLETE",
            $"章节总状态仍未完成，但真实视频均已完成，继续检查后续章节：{completedChapter.Title}");
        ShowInAppNotice("本章视频已完成", $"“{completedChapter.DisplayTitle}”只剩测验或其他任务，正在继续查找未看完视频。", false);

        var next = FindNextSequentialNavigationCandidate(completedChapter.Index);
        if (next is null)
        {
            var catalogResult = await RefreshCatalogUntilStatusReadyAsync(preserveSelection: true);
            if (_isClosing || _automationPaused || _pendingNextVideo != completedChapter) return;
            if (catalogResult != CatalogRefreshResult.Stable)
            {
                ShowContinueNextFallback(completedChapter, "后续章节目录仍在加载，尚未确认课程结束", chapterAlreadyConfirmed: true);
                return;
            }
            next = FindNextSequentialNavigationCandidate(completedChapter.Index);
        }
        if (next is null && !string.IsNullOrWhiteSpace(completedChapter.ChapterId))
        {
            next = await _adapter.FindNextChapterCandidateAsync(completedChapter.ChapterId);
            if (next is not null)
                next.Index = completedChapter.Index == int.MaxValue ? int.MaxValue : completedChapter.Index + 1;
        }

        if (next is not null && !_autoAdvanceVisited.Contains(ChapterIdentity(next)))
        {
            _pendingPreparationInProgress = false;
            await QueueAndOpenNextChapterAsync(next);
            return;
        }

        ClearPendingNextVideo(clearSelection: false);
        _stateMachine.Transition(AppRunState.WaitingForUser, "后续未找到未完成视频");
        NotifyUser("没有未看完视频", "已跳过视频全部完成的章节，后续暂未识别到仍需播放的视频。", false);
    }

    private async Task<(VideoTaskItem[] Tasks, bool Stable)> ScanTargetVideoTasksUntilStableAsync(ChapterItem target)
    {
        if (_adapter is null)
            return (Array.Empty<VideoTaskItem>(), false);

        string? previousFingerprint = null;
        var stableCount = 0;
        VideoTaskItem[] latest = Array.Empty<VideoTaskItem>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (_isClosing || _automationPaused || _pendingNextVideo != target)
                return (latest, false);

            latest = (await _adapter.ScanVideoTasksAsync())
                .Where(x => VideoTaskBelongsToChapter(x, target))
                .OrderBy(x => x.Index)
                .ToArray();
            var fingerprint = string.Join("|", latest.Select(x =>
                $"{VideoTaskIdentity(x)}:{x.Index}:{x.CompletionKnown}:{x.IsCompleted}"));
            if (string.Equals(previousFingerprint, fingerprint, StringComparison.Ordinal))
                stableCount++;
            else
            {
                previousFingerprint = fingerprint;
                stableCount = 1;
            }

            if (stableCount >= 3)
                return (latest, true);

            await Task.Delay(450 + attempt * 100, _lifetimeCts.Token);
        }

        return (latest, false);
    }

    private async Task<PlayerSnapshot?> WaitForPendingPlayerSwitchAsync(ChapterItem target, PlayerSnapshot? originSnapshot)
    {
        if (_adapter is null) return null;
        var expectedGeneration = _automationGeneration;

        for (var attempt = 0; attempt < 14; attempt++)
        {
            if (_isClosing || _automationPaused || _pendingNextVideo != target ||
                !IsAutomationOperationCurrent(expectedGeneration))
                return null;

            var player = await _adapter.GetPlayerSnapshotAsync();
            if (!IsAutomationOperationCurrent(expectedGeneration)) return null;
            if (PlayerMediaEvidence.IsReadyForPlayback(player))
            {
                var changed = originSnapshot?.Found != true || !IsSameMediaEvidence(originSnapshot, player);
                var refreshStructure = attempt is 0 or 4 or 8 or 12;
                if (changed && !IsStaleEndedPlayer(player) &&
                    await PlayerMatchesTargetChapterAsync(target, player, allowCatalogEvidence: refreshStructure))
                    return player;
            }

            if ((attempt == 4 || attempt == 8) && await HasTargetChapterPageEvidenceAsync(target))
                await _adapter.FocusFirstUnfinishedVideoTaskAsync();

            await Task.Delay(350, _lifetimeCts.Token);
        }

        App.Logger.Warn("CX-CHAPTER-SWITCH-NOOP", $"目录目标未造成播放器切换：{target.Title}");
        return null;
    }

    private async Task ContinuePendingVideoAsync(ChapterItem target, bool userInitiated, PlayerSnapshot? knownSnapshot = null)
    {
        if (_adapter is null || _pendingNextVideo != target || _isClosing)
            return;

        ContinueNextButton.Visibility = Visibility.Collapsed;
        _vm.CanContinueNext = false;

        var snapshot = knownSnapshot ?? await WaitForPendingPlayerSwitchAsync(target, _pendingOriginPlayerSnapshot);
        if (_pendingNextVideo != target || _isClosing) return;
        if (snapshot is null || !PlayerMediaEvidence.IsReadyForPlayback(snapshot) || IsStaleEndedPlayer(snapshot))
        {
            await HandlePendingVideoUnavailableAsync(target, "未确认播放器已经切到目标章节");
            return;
        }

        ConfirmCurrentChapter(target, snapshot, updateVideoTitle: true);
        _courseUnresolvedChapters.Remove(ChapterIdentity(target));

        var playback = await StartPlaybackWithRetryAsync(snapshot, userInitiated ? "手动继续下一节" : "自动切换下一节");
        var played = playback.Playing;
        snapshot = playback.Snapshot;
        if (_pendingNextVideo != target || _isClosing) return;
        if (!played)
        {
            ShowContinueNextFallback(target, "浏览器媒体策略阻止自动播放", chapterAlreadyConfirmed: true);
            return;
        }

        var verified = await _adapter.GetPlayerSnapshotAsync();
        if (verified.Found && !IsStaleEndedPlayer(verified))
        {
            _lastPlayerIdentity = PlayerIdentity(verified);
            ConfirmCurrentChapter(target, verified, updateVideoTitle: true);
        }

        ClearPendingNextVideo(clearSelection: false);
        _autoAdvanceVisited.Clear();
        _lastEndedSource = string.Empty;
        _lastEndedMediaIdentity = string.Empty;
        _lastEndedPlayerSnapshot = null;
        _stateMachine.Transition(AppRunState.Playing, userInitiated ? "用户继续已确认的下一节" : "已确认切换并继续播放下一视频");
        App.Logger.Info("CX-AUTO-NEXT", $"播放器真实切换后开始播放：{target.Title}；继承倍速={_lastObservedPlaybackRate:0.##}x");
    }

    private async Task HandlePendingVideoUnavailableAsync(ChapterItem pending, string reason)
    {
        if (_pendingNextVideo != pending) return;

        App.Logger.Warn("CX-AUTO-NEXT", $"候选章节未确认播放器切换：{pending.Title}；{reason}");
        _autoAdvanceVisited.Add(ChapterIdentity(pending));

        var confirmedNoVideo = await ConfirmChapterHasNoVideoAsync(pending);
        if (confirmedNoVideo)
        {
            _courseVerifiedChapters.Add(ChapterIdentity(pending));
            _courseUnresolvedChapters.Remove(ChapterIdentity(pending));
            App.Logger.Info("CX-COURSE-NO-VIDEO", $"章节已确认没有真实视频，继续检查后续章节：{pending.Title}");
        }
        else
        {
            _courseUnresolvedChapters[ChapterIdentity(pending)] = reason;
        }

        var adapter = _adapter;
        if (App.Settings.Current.AutoPlayNextVideo &&
            !_automationPaused && adapter is not null)
        {
            var next = FindNextSequentialNavigationCandidate(pending.Index);
            if (next is null && !string.IsNullOrWhiteSpace(pending.ChapterId))
            {
                next = await adapter.FindNextChapterCandidateAsync(pending.ChapterId);
                if (next is not null)
                {
                    next.Index = pending.Index == int.MaxValue ? int.MaxValue : pending.Index + 1;
                    App.Logger.Info("CX-AUTO-NEXT-FALLBACK", $"当前候选所有真实导航路径都未切出新播放器，继续按网页原生目录尝试后续章节：{next.Title}");
                }
            }

            if (next is not null && !_autoAdvanceVisited.Contains(ChapterIdentity(next)))
            {
                _pendingPreparationInProgress = false;
                await QueueAndOpenNextChapterAsync(next);
                return;
            }
        }

        ShowContinueNextFallback(pending, reason, chapterAlreadyConfirmed: false);
    }

    private async Task<bool> ConfirmChapterHasNoVideoAsync(ChapterItem target)
    {
        if (_adapter is null || _isClosing || !await HasTargetChapterPageEvidenceAsync(target))
            return false;

        var player = await _adapter.GetPlayerSnapshotAsync();
        if (player.Found && !IsStaleEndedPlayer(player) &&
            await PlayerMatchesTargetChapterAsync(target, player, allowCatalogEvidence: true))
            return false;

        // 对视频/未知章节而言，一次空扫描不能证明没有视频；懒加载任务可能稍后才出现。
        if (target.TaskType is TaskType.Video or TaskType.Unknown)
            return false;

        var tasks = await _adapter.ScanVideoTasksAsync();
        return !tasks.Any(x => VideoTaskBelongsToChapter(x, target));
    }

    private async Task VerifyCourseCompletionOrContinueAsync(PlayerSnapshot endedSnapshot, int currentIndex)
    {
        EnsureCourseTraversalStarted(currentIndex);
        var catalogResult = await RefreshCatalogUntilStatusReadyAsync(endedSnapshot, preserveSelection: true);
        if (_isClosing || _automationPaused) return;
        if (catalogResult != CatalogRefreshResult.Stable)
        {
            ClearPendingNextVideo();
            _stateMachine.Transition(AppRunState.WaitingForUser, "全课程复核等待稳定目录");
            NotifyUser("课程目录仍在加载", "章节状态尚未稳定，因此不会提前显示课程完成。页面稳定后可再次点击开始。", false);
            return;
        }

        if (_vm.Chapters.Count == 0)
        {
            ClearPendingNextVideo();
            _stateMachine.Transition(AppRunState.WaitingForUser, "全课程复核时未读取到章节目录");
            NotifyUser("课程目录暂时无法复核", "程序没有读取到完整章节目录，因此不会提前显示课程完成。页面稳定后可点击“开始”继续。", false);
            return;
        }

        foreach (var completed in _vm.Chapters.Where(x => !ChapterHasPendingVideo(x)))
            _courseUnresolvedChapters.Remove(ChapterIdentity(completed));

        var pending = CoursePlaybackPlan.BuildPendingChapters(
            _vm.Chapters,
            _courseTraversalStartIndex,
            _courseVerifiedChapters);
        App.Logger.Info("CX-COURSE-AUDIT",
            $"全课程复核：起始序号={_courseTraversalStartIndex}；待核验章节={pending.Count}；无法确认={_courseUnresolvedChapters.Count}");

        var actionable = pending.FirstOrDefault(x => !_courseUnresolvedChapters.ContainsKey(ChapterIdentity(x)));
        if (actionable is not null)
        {
            _autoAdvanceVisited.Clear();
            await QueueAndOpenNextChapterAsync(actionable);
            return;
        }

        var unresolved = pending.FirstOrDefault(x => _courseUnresolvedChapters.ContainsKey(ChapterIdentity(x)));
        if (unresolved is not null)
        {
            var reason = _courseUnresolvedChapters[ChapterIdentity(unresolved)];
            ShowContinueNextFallback(unresolved, $"全课程复核仍无法确认该章节：{reason}", chapterAlreadyConfirmed: false);
            return;
        }

        if (_courseUnresolvedChapters.Count > 0)
        {
            ClearPendingNextVideo();
            _stateMachine.Transition(AppRunState.WaitingForUser, "全课程复核仍有目录外的未确认章节");
            NotifyUser("仍有章节未能核验", "部分章节此前未能打开，并且当前目录没有重新提供可靠证据，因此不会提前显示课程完成。", false);
            return;
        }

        ClearPendingNextVideo();
        _stateMachine.Transition(AppRunState.CourseCompleted, "全课程待播清单已清空并完成复核");
        NotifyUser("课程视频已全部处理", "从本次起点开始，目录中的待播视频已经全部核验完成。", false);
    }

    private void EnsureCourseTraversalStarted(int startIndex)
    {
        if (_courseTraversalStartIndex >= 0) return;
        _courseTraversalStartIndex = Math.Max(0, startIndex);
        App.Logger.Info("CX-COURSE-QUEUE", $"建立全课程待播清单；起始章节序号={_courseTraversalStartIndex}");
    }

    private void ResetCourseTraversal(ChapterItem? start)
    {
        _courseVerifiedChapters.Clear();
        _courseUnresolvedChapters.Clear();
        _courseTraversalStartIndex = start is null ? -1 : Math.Max(0, start.Index);
        App.Logger.Info("CX-COURSE-QUEUE", start is null
            ? "已清空全课程待播清单"
            : $"已从章节重新建立全课程待播清单：{start.Title}");
    }

    private void ShowContinueNextFallback(ChapterItem target, string reason, bool chapterAlreadyConfirmed)
    {
        if (_pendingNextVideo != target)
            _pendingNextVideo = target;
        _vm.NextVideoText = target.Title;
        _vm.CanContinueNext = true;
        ContinueNextButton.Visibility = Visibility.Visible;
        _stateMachine.Transition(AppRunState.WaitingForUser, reason);
        if (chapterAlreadyConfirmed)
            NotifyUser("下一节等待播放", $"“{target.Title}”的播放器已经真实切换，可点击“继续下一节”。", false);
        else
            NotifyUser("章节尚未切换", $"点击了“{target.Title}”，但播放器没有变化；左侧和右侧不会提前跳过去。可在网页目录手动点该章节后再继续。", false);
    }

    private ChapterItem? FindNextNavigationCandidate(int afterIndex)
    {
        return _vm.Chapters
            .Where(x => x.Index > afterIndex && x.IsNavigationCandidate && ChapterHasExplicitUnfinishedVideo(x))
            .Where(x => !_autoAdvanceVisited.Contains(ChapterIdentity(x)))
            .OrderBy(x => x.Index)
            .FirstOrDefault();
    }

    private ChapterItem? FindNextSequentialNavigationCandidate(int afterIndex)
    {
        return CoursePlaybackPlan.BuildPendingChapters(
                _vm.Chapters,
                afterIndex == int.MaxValue ? int.MaxValue : afterIndex + 1,
                _courseVerifiedChapters)
            .Where(x => !_autoAdvanceVisited.Contains(ChapterIdentity(x)))
            .FirstOrDefault();
    }

    private static bool ChapterHasExplicitUnfinishedVideo(ChapterItem chapter)
    {
        if (chapter.VideoTasks.Count > 0)
            return chapter.VideoTasks.Any(x => x.CompletionKnown && !x.IsCompleted);
        return (chapter.TaskType == TaskType.Video || chapter.TaskType == TaskType.Unknown) &&
               chapter.CompletionKnown && !chapter.IsCompleted;
    }

    private static bool ChapterHasPendingVideo(ChapterItem chapter)
        => CoursePlaybackPlan.HasPendingVideo(chapter);

    private ChapterItem? FindFirstPendingNavigationCandidate(IEnumerable<ChapterItem> chapters)
        => CoursePlaybackPlan.BuildPendingChapters(chapters, 0, _courseVerifiedChapters).FirstOrDefault();

    private ChapterItem? FindFirstUnfinishedNavigationCandidate(IEnumerable<ChapterItem> chapters)
    {
        var items = chapters.OrderBy(x => x.Index).ToArray();

        // 先按真实视频任务点找第一条明确未完成，再映射回所属章节；
        // 只有平台没有暴露任务级信息时才退回章节级完成状态。
        var unfinishedTask = _vm.VideoTasks
            .Where(x => x.CompletionKnown && !x.IsCompleted)
            .OrderBy(x => x.Index)
            .FirstOrDefault();
        if (unfinishedTask is not null)
        {
            if (!string.IsNullOrWhiteSpace(unfinishedTask.ChapterId))
            {
                var byId = items.FirstOrDefault(x =>
                    string.Equals(x.ChapterId, unfinishedTask.ChapterId, StringComparison.OrdinalIgnoreCase));
                if (byId is not null) return byId;
            }

            if (!string.IsNullOrWhiteSpace(unfinishedTask.ChapterTitle))
            {
                var byTitle = items.FirstOrDefault(x => TitlesLikelyMatch(x.Title, unfinishedTask.ChapterTitle));
                if (byTitle is not null) return byTitle;
            }
        }

        return items
            .Where(x => x.IsNavigationCandidate && ChapterHasExplicitUnfinishedVideo(x))
            .FirstOrDefault();
    }

    private static bool IsCurrentChapterIdentity(ChapterItem chapter, string? currentChapterId)
        => !string.IsNullOrWhiteSpace(currentChapterId) &&
           !string.IsNullOrWhiteSpace(chapter.ChapterId) &&
           string.Equals(chapter.ChapterId, currentChapterId, StringComparison.OrdinalIgnoreCase);

    private bool IsStaleEndedPlayer(PlayerSnapshot snapshot)
    {
        if (!snapshot.Found) return false;
        if (_lastEndedPlayerSnapshot?.Found == true &&
            PlayerMediaEvidence.IsSameMedia(_lastEndedPlayerSnapshot, snapshot))
            return true;
        var identity = PlayerIdentity(snapshot);
        if (!string.IsNullOrWhiteSpace(_lastEndedMediaIdentity) && !string.IsNullOrWhiteSpace(identity))
            return string.Equals(identity, _lastEndedMediaIdentity, StringComparison.OrdinalIgnoreCase);
        return !string.IsNullOrWhiteSpace(_lastEndedSource) &&
               !string.IsNullOrWhiteSpace(snapshot.Source) &&
               string.Equals(snapshot.Source, _lastEndedSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string ChapterIdentity(ChapterItem chapter)
        => CoursePlaybackPlan.Identity(chapter);

    private static string PlayerIdentity(PlayerSnapshot snapshot)
        => PlayerMediaEvidence.StableIdentity(snapshot);

    private static string ShortIdentity(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return "(none)";
        return identity.Length <= 120 ? identity : identity[..120] + "...";
    }

    private string ResolveEffectiveCourseTitle(string? recognizedTitle)
    {
        var currentUrl = Browser.Source?.ToString() ?? string.Empty;
        var currentCourseId = ExtractCourseId(currentUrl);

        var selected = _vm.SelectedCourse;
        if (selected is not null && !IsGenericCourseTitle(selected.Title) &&
            (string.IsNullOrWhiteSpace(currentCourseId) || SameCourseContext(selected.Url, currentUrl)))
            return selected.Title.Trim();

        var matched = _vm.Courses.FirstOrDefault(x =>
            !IsGenericCourseTitle(x.Title) && SameCourseContext(x.Url, currentUrl));
        if (matched is not null) return matched.Title.Trim();

        if (!IsGenericCourseTitle(recognizedTitle)) return recognizedTitle!.Trim();

        // 上次课程只允许在 URL 能证明仍是同一门课时兜底，避免手动进入另一门课后右侧还显示旧课程名。
        var last = App.Settings.Current.LastCourseTitle;
        if (!IsGenericCourseTitle(last) && SameCourseContext(App.Settings.Current.LastCourseUrl, currentUrl))
            return last.Trim();

        return string.Empty;
    }

    private static bool IsGenericCourseTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var title = value.Trim();
        return title.Equals("学习通", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("课程", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("我的课程", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("学生学习页面", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("学生学习", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("学习页面", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("课程学习", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("章节学习", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("任务学习", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("学生课程", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("返回课程", StringComparison.OrdinalIgnoreCase) ||
               title.Equals("提示", StringComparison.OrdinalIgnoreCase);
    }

    private void SyncCurrentCourseCard(string courseTitle)
    {
        var currentUrl = Browser.Source?.ToString() ?? string.Empty;
        if (!ChaoxingUrlClassifier.IsStudyUri(currentUrl) || IsGenericCourseTitle(courseTitle))
            return;

        var matching = _vm.Courses.FirstOrDefault(x => SameCourseContext(x.Url, currentUrl));
        if (matching is null)
        {
            matching = new CourseItem { Title = courseTitle.Trim(), Url = currentUrl };
            _vm.Courses.Add(matching);
        }
        else
        {
            matching.Title = courseTitle.Trim();
        }

        foreach (var invalid in _vm.Courses.Where(x => IsGenericCourseTitle(x.Title)).ToArray())
            _vm.Courses.Remove(invalid);
        _vm.SelectedCourse = matching;
        _courseCacheService.Save(_vm.Courses);
    }

    private static string NormalizeTitleKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Trim()
            .Where(ch => !char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch))
            .ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static bool TitlesLikelyMatch(string? left, string? right)
    {
        var a = NormalizeTitleKey(left);
        var b = NormalizeTitleKey(right);
        if (a.Length < 2 || b.Length < 2) return false;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
               (a.Length >= 4 && b.Contains(a, StringComparison.OrdinalIgnoreCase)) ||
               (b.Length >= 4 && a.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    private static string VideoTaskIdentity(VideoTaskItem task)
    {
        if (!string.IsNullOrWhiteSpace(task.TaskKey)) return task.TaskKey;
        if (!string.IsNullOrWhiteSpace(task.Source)) return $"src:{task.Source}";
        if (!string.IsNullOrWhiteSpace(task.MediaId))
            return $"media:{task.MediaId}|doc:{task.DocumentUrl}|dom:{task.DomIndex}";
        return $"doc:{task.DocumentUrl}|dom:{task.DomIndex}|title:{task.Title}";
    }

    private void BindVideoTasksToChapters(
        IReadOnlyList<ChapterItem> chapters,
        IReadOnlyList<VideoTaskItem> tasks,
        PlayerSnapshot? playerEvidence,
        string currentUrl)
    {
        foreach (var chapter in chapters)
            chapter.VideoTasks = new List<VideoTaskItem>();

        var active = chapters.Where(x => x.IsActive).ToArray();
        var singleActive = active.Length == 1 ? active[0] : null;
        var currentUrlChapterId = ExtractChapterId(currentUrl);

        foreach (var task in tasks)
        {
            ChapterItem? chapter = null;
            var taskChapterId = !string.IsNullOrWhiteSpace(task.ChapterId)
                ? task.ChapterId
                : ExtractChapterId(task.DocumentUrl);

            if (!string.IsNullOrWhiteSpace(taskChapterId))
            {
                chapter = chapters.FirstOrDefault(x =>
                    string.Equals(x.ChapterId, taskChapterId, StringComparison.OrdinalIgnoreCase));
            }

            if (chapter is null && !string.IsNullOrWhiteSpace(task.ChapterTitle))
                chapter = chapters.FirstOrDefault(x => TitlesLikelyMatch(x.Title, task.ChapterTitle));

            // 对没有 chapterId 的 iframe，只把“当前可见/正在播放”的真实任务绑定给唯一 active 章节。
            // 不把整页所有未知任务都塞给 active 章节，避免统计和下一视频再次错位。
            if (chapter is null && singleActive is not null && (task.IsPlaying || task.IsVisible))
            {
                var playerDocumentMatches = playerEvidence?.Found == true &&
                    !string.IsNullOrWhiteSpace(playerEvidence.DocumentUrl) &&
                    string.Equals(task.DocumentUrl, playerEvidence.DocumentUrl, StringComparison.OrdinalIgnoreCase);
                var activeMatchesUrl = !string.IsNullOrWhiteSpace(currentUrlChapterId) &&
                    string.Equals(singleActive.ChapterId, currentUrlChapterId, StringComparison.OrdinalIgnoreCase);
                if (playerDocumentMatches || activeMatchesUrl || string.IsNullOrWhiteSpace(currentUrlChapterId))
                    chapter = singleActive;
            }

            if (chapter is null) continue;
            if (string.IsNullOrWhiteSpace(task.ChapterId)) task.ChapterId = chapter.ChapterId;
            if (string.IsNullOrWhiteSpace(task.ChapterTitle)) task.ChapterTitle = chapter.Title;
            chapter.VideoTasks.Add(task);
        }

        foreach (var chapter in chapters)
        {
            if (chapter.VideoTasks.Count == 0) continue;
            chapter.TaskType = TaskType.Video;

            if (chapter.VideoTasks.Any(x => x.CompletionKnown && !x.IsCompleted))
            {
                chapter.CompletionKnown = true;
                chapter.IsCompleted = false;
            }
            else if (chapter.VideoTasks.All(x => x.CompletionKnown) &&
                     chapter.VideoTasks.All(x => x.IsCompleted))
            {
                chapter.CompletionKnown = true;
                chapter.IsCompleted = true;
            }
            else
            {
                // 只要仍有状态未知的视频，就不能沿用目录行可能过时的“已完成”标记。
                // 未知视频保留为待核验候选，避免自动续播把真正没看的章节跳过去。
                chapter.CompletionKnown = false;
                chapter.IsCompleted = false;
            }
        }
    }

    private VideoTaskItem? ResolveVideoTaskFromPlayer(PlayerSnapshot? player)
        => ResolveVideoTaskFromPlayer(player, _vm.VideoTasks);

    private static VideoTaskItem? ResolveVideoTaskFromPlayer(
        PlayerSnapshot? player,
        IEnumerable<VideoTaskItem> candidates)
    {
        if (player?.Found != true) return null;
        var tasks = candidates.ToArray();
        if (tasks.Length == 0) return null;

        // TaskKey 是同一轮扫描里最具体的身份，优先于 Source。
        // 学习通可能让多个任务复用同一个媒体 URL，只按 Source 会把播放器绑到错误章节。
        if (!string.IsNullOrWhiteSpace(player.TaskKey))
        {
            var byKey = tasks.FirstOrDefault(x =>
                string.Equals(x.TaskKey, player.TaskKey, StringComparison.OrdinalIgnoreCase));
            if (byKey is not null) return byKey;
        }

        var exactDom = tasks.FirstOrDefault(x =>
            x.DomIndex == player.DomIndex &&
            string.Equals(x.DocumentUrl, player.DocumentUrl, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(player.MediaId) ||
             string.IsNullOrWhiteSpace(x.MediaId) ||
             string.Equals(x.MediaId, player.MediaId, StringComparison.OrdinalIgnoreCase)));
        if (exactDom is not null) return exactDom;

        if (!string.IsNullOrWhiteSpace(player.Source))
        {
            var sameSource = tasks.Where(x => !string.IsNullOrWhiteSpace(x.Source) &&
                string.Equals(x.Source, player.Source, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (sameSource.Length == 1) return sameSource[0];

            var sourceInSameDocument = sameSource.FirstOrDefault(x =>
                string.Equals(x.DocumentUrl, player.DocumentUrl, StringComparison.OrdinalIgnoreCase) &&
                (x.DomIndex == player.DomIndex || x.DomIndex < 0));
            if (sourceInSameDocument is not null) return sourceInSameDocument;
        }

        if (!string.IsNullOrWhiteSpace(player.MediaId))
        {
            var sameMedia = tasks.Where(x =>
                string.Equals(x.MediaId, player.MediaId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (sameMedia.Length == 1) return sameMedia[0];

            var mediaInSameDocument = sameMedia.FirstOrDefault(x =>
                string.Equals(x.DocumentUrl, player.DocumentUrl, StringComparison.OrdinalIgnoreCase) &&
                x.DomIndex == player.DomIndex);
            if (mediaInSameDocument is not null) return mediaInSameDocument;
        }

        return null;
    }

    private string ResolvePlayerVideoTitle(PlayerSnapshot player, VideoTaskItem? task)
    {
        var title = !IsGenericVideoTitle(player.VideoTitle)
            ? player.VideoTitle.Trim()
            : task is not null && !IsGenericVideoTitle(task.Title)
                ? task.Title.Trim()
                : string.Empty;

        var chapter = task is null
            ? _vm.SelectedChapter
            : _vm.Chapters.FirstOrDefault(x => VideoTaskBelongsToChapter(task, x)) ?? _vm.SelectedChapter;
        if (chapter is not null && task is not null)
        {
            var videos = chapter.VideoTasks.OrderBy(x => x.Index).ToArray();
            var position = Array.FindIndex(videos, x =>
                ReferenceEquals(x, task) ||
                string.Equals(VideoTaskIdentity(x), VideoTaskIdentity(task), StringComparison.OrdinalIgnoreCase));
            if (position >= 0)
            {
                var prefix = $"第 {position + 1}/{videos.Length} 个视频";
                return string.IsNullOrWhiteSpace(title) ? prefix : $"{prefix} · {title}";
            }
        }

        return string.IsNullOrWhiteSpace(title) ? "视频已检测，标题暂未读取" : title;
    }

    private static bool IsGenericVideoTitle(string? title)
    {
        var value = (title ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ||
               value.Equals("视频", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("播放视频", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("播放器", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("video", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("player", StringComparison.OrdinalIgnoreCase);
    }

    private static bool VideoTaskBelongsToChapter(VideoTaskItem task, ChapterItem target)
    {
        if (!string.IsNullOrWhiteSpace(target.ChapterId) && !string.IsNullOrWhiteSpace(task.ChapterId) &&
            string.Equals(target.ChapterId, task.ChapterId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (TitlesLikelyMatch(target.Title, task.ChapterTitle)) return true;

        return !string.IsNullOrWhiteSpace(task.DocumentUrl) && ChapterMatchesUri(target, task.DocumentUrl);
    }

    private async Task<bool> PlayerMatchesTargetChapterAsync(
        ChapterItem target,
        PlayerSnapshot player,
        bool allowCatalogEvidence)
    {
        if (!player.Found) return false;

        if (!string.IsNullOrWhiteSpace(target.ChapterId))
        {
            if (!string.IsNullOrWhiteSpace(player.ChapterId) &&
                string.Equals(target.ChapterId, player.ChapterId, StringComparison.OrdinalIgnoreCase))
                return true;

            var documentChapterId = ExtractChapterId(player.DocumentUrl);
            if (!string.IsNullOrWhiteSpace(documentChapterId) &&
                string.Equals(target.ChapterId, documentChapterId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var task = ResolveVideoTaskFromPlayer(player);
        if (task is not null && VideoTaskBelongsToChapter(task, target)) return true;

        if (TitlesLikelyMatch(target.Title, player.ChapterTitleHint)) return true;
        if (!string.IsNullOrWhiteSpace(player.DocumentUrl) && ChapterMatchesUri(target, player.DocumentUrl)) return true;

        if (!allowCatalogEvidence || _adapter is null) return false;

        // 目录 active 本身不能证明播放器已经切到目标章节。
        // 只有重新扫描后的“真实播放器 -> 视频任务 -> 章节”结构能闭环时才确认，
        // 避免目录先高亮、播放器仍停在旧视频却被误判成功。
        var freshChapters = await _adapter.ScanChaptersAsync();
        var freshTasks = await _adapter.ScanVideoTasksAsync();
        BindVideoTasksToChapters(
            freshChapters,
            freshTasks,
            player,
            Browser.Source?.ToString() ?? string.Empty);

        var freshTask = ResolveVideoTaskFromPlayer(player, freshTasks);
        if (freshTask is not null && VideoTaskBelongsToChapter(freshTask, target))
            return true;

        // URL 是平台真实学习页且明确携带目标 chapterId 时，也属于强证据；
        // 仅 active class、仅左侧高亮永远不在这里直接返回 true。
        return ChapterMatchesUri(target, Browser.Source?.ToString()) &&
               (!string.IsNullOrWhiteSpace(ExtractChapterId(Browser.Source?.ToString())) ||
                !string.IsNullOrWhiteSpace(target.Url));
    }

    private ChapterItem? ResolveChapterFromEvidence(IEnumerable<ChapterItem> chapters, PlayerSnapshot? player, string currentUrl)
    {
        var items = chapters.ToArray();
        if (player?.Found == true)
        {
            if (!string.IsNullOrWhiteSpace(player.ChapterId))
            {
                var byPlayerId = items.FirstOrDefault(x =>
                    string.Equals(x.ChapterId, player.ChapterId, StringComparison.OrdinalIgnoreCase));
                if (byPlayerId is not null) return byPlayerId;
            }

            var playerDocumentChapterId = ExtractChapterId(player.DocumentUrl);
            if (!string.IsNullOrWhiteSpace(playerDocumentChapterId))
            {
                var byPlayerDocumentId = items.FirstOrDefault(x =>
                    string.Equals(x.ChapterId, playerDocumentChapterId, StringComparison.OrdinalIgnoreCase));
                if (byPlayerDocumentId is not null) return byPlayerDocumentId;
            }

            var task = ResolveVideoTaskFromPlayer(player);
            if (task is not null)
            {
                if (!string.IsNullOrWhiteSpace(task.ChapterId))
                {
                    var byTaskId = items.FirstOrDefault(x =>
                        string.Equals(x.ChapterId, task.ChapterId, StringComparison.OrdinalIgnoreCase));
                    if (byTaskId is not null) return byTaskId;
                }

                if (!string.IsNullOrWhiteSpace(task.ChapterTitle))
                {
                    var byTaskTitle = items.FirstOrDefault(x => TitlesLikelyMatch(x.Title, task.ChapterTitle));
                    if (byTaskTitle is not null) return byTaskTitle;
                }
            }

            if (!string.IsNullOrWhiteSpace(player.ChapterTitleHint))
            {
                var byHint = items.FirstOrDefault(x => TitlesLikelyMatch(x.Title, player.ChapterTitleHint));
                if (byHint is not null) return byHint;
            }

            if (!string.IsNullOrWhiteSpace(player.DocumentUrl))
            {
                var byPlayerDocumentUrl = items.FirstOrDefault(x => UriEquivalent(x.Url, player.DocumentUrl));
                if (byPlayerDocumentUrl is not null) return byPlayerDocumentUrl;
            }

            // 正常进入视频页面时，目录唯一 active 项可以作为最后一层“播放器所在章节”证据；
            // 但手动/自动切换正在进行时不采纳 active，避免目录先高亮而视频尚未切换造成假确认。
            if (_requestedChapter is null && _pendingNextVideo is null)
            {
                var activeWithPlayer = items.Where(x => x.IsActive).ToArray();
                if (activeWithPlayer.Length == 1) return activeWithPlayer[0];
            }

            return null;
        }

        var currentChapterId = ExtractChapterId(currentUrl);
        if (!string.IsNullOrWhiteSpace(currentChapterId))
        {
            var byCurrentId = items.FirstOrDefault(x =>
                string.Equals(x.ChapterId, currentChapterId, StringComparison.OrdinalIgnoreCase));
            if (byCurrentId is not null) return byCurrentId;
        }

        var active = items.Where(x => x.IsActive).ToArray();
        if (active.Length == 1) return active[0];
        return items.FirstOrDefault(x => UriEquivalent(x.Url, currentUrl));
    }

    private void ConfirmCurrentChapter(ChapterItem chapter, PlayerSnapshot? player, bool updateVideoTitle)
    {
        var confirmed = _vm.Chapters.FirstOrDefault(x =>
            string.Equals(ChapterIdentity(x), ChapterIdentity(chapter), StringComparison.OrdinalIgnoreCase));
        if (confirmed is null && player?.Found == true)
            confirmed = ResolveChapterFromEvidence(_vm.Chapters, player, Browser.Source?.ToString() ?? string.Empty);
        confirmed ??= chapter;

        _vm.SelectedChapter = confirmed;
        _vm.CurrentChapterText = confirmed.DisplayTitle;
        _requestedChapter = null;
        _requestedOriginPlayerIdentity = string.Empty;
        _requestedChapterAt = DateTime.MinValue;
        if (updateVideoTitle && player?.Found == true)
        {
            _vm.CurrentVideoText = ResolvePlayerVideoTitle(player, ResolveVideoTaskFromPlayer(player));
        }
    }

    private bool IsConfirmedChapter(ChapterItem chapter)
        => _vm.SelectedChapter is not null &&
           string.Equals(ChapterIdentity(_vm.SelectedChapter), ChapterIdentity(chapter), StringComparison.OrdinalIgnoreCase);

    private void ClearPendingNextVideo(bool clearSelection = false)
    {
        _pendingNextVideo = null;
        _pendingNavigationRequested = false;
        _pendingOriginPlayerIdentity = string.Empty;
        _pendingOriginPlayerSnapshot = null;
        _vm.NextVideoText = "-";
        _vm.CanContinueNext = false;
        ContinueNextButton.Visibility = Visibility.Collapsed;
        if (clearSelection)
            _vm.SelectedChapter = null;
    }

    private void HandleLoginPageReached()
    {
        _loginPageActive = true;
        if (string.IsNullOrWhiteSpace(_loginRecoveryUrl) &&
            ChaoxingUrlClassifier.IsStudyUri(_lastStableStudyUrl))
            _loginRecoveryUrl = _lastStableStudyUrl;

        _stateMachine.Transition(AppRunState.ManualIntervention, "检测到登录页，等待完成登录");
        _vm.NetworkText = "需登录";
        NotifyUser(
            "登录状态需要确认",
            string.IsNullOrWhiteSpace(_loginRecoveryUrl)
                ? "平台进入登录页，请正常完成登录；程序会保留现有 WebView2 登录数据。"
                : "平台进入登录页，请正常完成登录；完成后会自动返回最近学习页面。",
            false);
    }

    private static bool IsSafeChapterFallbackUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme is "http" or "https" &&
               ChaoxingConstants.IsChaoxingUri(uri) &&
               !ChaoxingUrlClassifier.IsLoginUri(uri);
    }

    private async Task ApplyRestoreSnapshotAsync()
    {
        if (_adapter is null || _restoreSnapshot is null)
            return;

        var snapshot = _restoreSnapshot;
        _restoreSnapshot = null;

        var pageVersion = _pageVersion;
        await Task.Delay(800, _lifetimeCts.Token);
        if (!IsCurrentPage(pageVersion)) return;
        await _adapter.PauseVideoAsync();
        if (!IsCurrentPage(pageVersion)) return;

        if (snapshot.PositionSeconds > 0)
            await _adapter.SeekAsync(snapshot.PositionSeconds);
        if (!IsCurrentPage(pageVersion)) return;

        if (snapshot.PlaybackRate > 0 && Math.Abs(snapshot.PlaybackRate - 1.0) > 0.001)
        {
            var restored = await _adapter.RestorePlaybackRateUsingUiAsync(snapshot.PlaybackRate);
            if (!IsCurrentPage(pageVersion)) return;
            if (!restored)
                App.Logger.Warn("CX-SPEED-RESTORE", $"恢复倍速失败：{snapshot.PlaybackRate:0.##}x；保留平台默认值。");
        }

        _stateMachine.Transition(AppRunState.VideoPaused, "已恢复页面和播放位置，等待人工继续");
        NotifyUser("恢复完成", "已恢复上次页面与播放位置，视频保持暂停。", false);
    }

    private void StartActiveStatIfNeeded(PlayerSnapshot snapshot)
    {
        if (_activeStat is not null)
            return;

        _activeStat = new LearningStatRecord
        {
            CourseTitle = _vm.SelectedCourse?.Title ?? App.Settings.Current.LastCourseTitle,
            ChapterTitle = _vm.SelectedChapter?.Title ?? _vm.CurrentChapterText,
            VideoTitle = !string.IsNullOrWhiteSpace(_vm.CurrentVideoText) && _vm.CurrentVideoText != "-"
                ? _vm.CurrentVideoText
                : (!string.IsNullOrWhiteSpace(snapshot.VideoTitle) ? snapshot.VideoTitle : _lastRecognition.Title),
            StartedAt = DateTime.Now,
            PlaybackRate = snapshot.PlaybackRate,
            VideoDurationSeconds = snapshot.Duration
        };
        _watchTracker.Reset(PlayerIdentity(snapshot), DateTimeOffset.Now, isPlaying: true);
    }

    private void FinishActiveStat(string status, PlayerSnapshot snapshot)
    {
        if (_activeStat is null)
            return;

        _watchTracker.Sample(PlayerIdentity(snapshot), false, DateTimeOffset.Now);
        _activeStat.EndedAt = DateTime.Now;
        _activeStat.WatchedSeconds = Math.Max(0, _watchTracker.Elapsed.TotalSeconds);
        _activeStat.VideoDurationSeconds = snapshot.Duration;
        _activeStat.PlaybackRate = snapshot.PlaybackRate;
        _activeStat.FinalStatus = status;
        _statsService.Append(_activeStat);
        _activeStat = null;
        _watchTracker.Reset();
    }

    private void SaveSession(PlayerSnapshot snapshot)
    {
        if (_stateMachine.Current is AppRunState.Idle or AppRunState.Stopped)
            return;

        _sessionService.Save(new SessionSnapshot
        {
            WasRunning = true,
            CourseTitle = _vm.SelectedCourse?.Title ?? App.Settings.Current.LastCourseTitle,
            CourseUrl = _vm.SelectedCourse?.Url ?? App.Settings.Current.LastCourseUrl,
            ChapterTitle = _vm.SelectedChapter?.Title ?? _lastRecognition.Title,
            ChapterUrl = Browser.Source?.ToString() ?? string.Empty,
            PositionSeconds = snapshot.CurrentTime,
            PlaybackRate = snapshot.PlaybackRate > 0 ? snapshot.PlaybackRate : _lastObservedPlaybackRate,
            State = _stateMachine.Current,
            SavedAt = DateTime.Now
        });
    }

    private void StateMachine_StateChanged(object? sender, AppRunState state)
    {
        var text = state switch
        {
            AppRunState.Idle => "空闲",
            AppRunState.Loading => "页面加载中",
            AppRunState.Playing => "正在播放",
            AppRunState.VideoPaused => "视频已暂停",
            AppRunState.AutomationPaused => "辅助流程已暂停",
            AppRunState.Ended => "当前视频已结束",
            AppRunState.FindingNextVideo => "查找下一视频",
            AppRunState.PreloadingNextVideo => "预加载下一视频",
            AppRunState.WaitingForUser => "等待确认下一节",
            AppRunState.NetworkError => "网络异常",
            AppRunState.Recovering => "正在恢复",
            AppRunState.ManualIntervention => "等待人工处理",
            AppRunState.PageRecognitionFailed => "页面识别失败",
            AppRunState.CourseCompleted => "课程视频已全部核验",
            AppRunState.Stopped => "已停止",
            _ => state.ToString()
        };

        _vm.StatusText = text;
        UpdateCompositeStatus();
    }

    private void UpdateCompositeStatus()
    {
        _vm.AutomationStatusText = _automationPaused ? "辅助未启用" : "辅助运行中";
        var playback = string.IsNullOrWhiteSpace(_vm.PlaybackStatusText) ? "未检测到视频" : _vm.PlaybackStatusText;
        TopStatusText.Text = $"{playback} · {_vm.AutomationStatusText}";
        _trayService.SetState($"{playback} / {_vm.StatusText}");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_adapter is null || _isClosing || _isNavigating || _playerCommandInProgress ||
            _manualChapterOpenInProgress || _pendingPreparationInProgress)
            return;
        _playerCommandInProgress = true;
        BeginAutomationOperation();
        ShowInAppNotice("开始辅助", "正在识别当前课程和待播放视频。", false);
        var pageVersion = _pageVersion;

        try
        {
            if (_automationPaused)
                SetAutomationPaused(false);

            // “开始”代表建立一轮新的课程遍历；不得让上一轮已核验集合隐藏本轮候选。
            _autoAdvanceVisited.Clear();
            ResetCourseTraversal(null);

            if (_vm.SelectedCourse is not null &&
                !UriEquivalent(Browser.Source?.ToString(), _vm.SelectedCourse.Url) &&
                !ChaoxingUrlClassifier.IsStudyUri(Browser.Source?.ToString()))
            {
                CurrentCourseText.Text = _vm.SelectedCourse.Title;
                _startAfterNavigation = true;
                Navigate(_vm.SelectedCourse.Url);
                return;
            }

            if (ChaoxingUrlClassifier.IsStudyUri(Browser.Source?.ToString()))
            {
                var catalogResult = await RefreshCatalogUntilStatusReadyAsync(bootstrapWhenEmpty: true);
                if (catalogResult != CatalogRefreshResult.Stable)
                {
                    if (catalogResult == CatalogRefreshResult.Unstable)
                        NotifyUser("课程目录仍在加载", "章节状态仍在变化，程序不会提前选择目标。页面稳定后可再次点击开始。", false);
                    return;
                }
                if (!IsCurrentPage(pageVersion)) return;
                var preferred = FindFirstUnfinishedNavigationCandidate(_vm.Chapters);
                if (preferred is not null && !ChapterMatchesUri(preferred, Browser.Source?.ToString()) && !IsConfirmedChapter(preferred))
                {
                    _autoAdvanceVisited.Clear();
                    ResetCourseTraversal(preferred);
                    await QueueAndOpenNextChapterAsync(preferred);
                    return;
                }
                if (preferred is not null && (ChapterMatchesUri(preferred, Browser.Source?.ToString()) || IsConfirmedChapter(preferred)))
                {
                    await _adapter.FocusFirstUnfinishedVideoTaskAsync();
                    await Task.Delay(200, _lifetimeCts.Token);
                    if (!IsCurrentPage(pageVersion)) return;
                }
            }

            var snapshot = await _adapter.GetPlayerSnapshotAsync();
            if (!IsCurrentPage(pageVersion)) return;
            var selectedIsCompleted = _vm.SelectedChapter?.CompletionKnown == true && _vm.SelectedChapter.IsCompleted;
            if (snapshot.Found && !snapshot.Ended && !selectedIsCompleted)
            {
                ClearPendingNextVideo();
                _autoAdvanceVisited.Clear();
                ResetCourseTraversal(_vm.SelectedChapter);
                var played = !snapshot.Paused || await _adapter.PlayVideoAsync(snapshot);
                if (!IsCurrentPage(pageVersion)) return;
                if (played)
                {
                    _stateMachine.Transition(AppRunState.Playing, "用户点击开始，播放当前未完成视频");
                    return;
                }
                NotifyUser("请点击网页播放", "已找到视频，但浏览器尚未确认播放；请直接点击网页中的播放按钮。", false);
                return;
            }

            var first = FindFirstUnfinishedNavigationCandidate(_vm.Chapters);
            if (first is not null)
            {
                _autoAdvanceVisited.Clear();
                ResetCourseTraversal(first);
                await QueueAndOpenNextChapterAsync(first);
                return;
            }

            var pending = FindFirstPendingNavigationCandidate(_vm.Chapters);
            if (pending is not null)
            {
                _autoAdvanceVisited.Clear();
                ResetCourseTraversal(pending);
                await QueueAndOpenNextChapterAsync(pending);
                return;
            }

            NotifyUser("未找到未完成视频", "当前页面没有识别到明确的未完成章节 / 视频。可在网页目录中选择目标后继续使用。", false);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            App.Logger.Error("CX-START", "开始流程失败。", ex);
            NotifyUser("开始失败", ex.Message, true);
        }
        finally { _playerCommandInProgress = false; }
    }

    private async void PauseVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_adapter is null)
            return;

        if (await _adapter.PauseVideoAsync())
        {
            _vm.PlaybackStatusText = "视频已暂停";
            _stateMachine.Transition(AppRunState.VideoPaused, "用户暂停视频");
            UpdateCompositeStatus();
            ShowInAppNotice("视频已暂停", "网页视频已经暂停，辅助流程状态保持不变。", false);
        }
        else
            NotifyUser("未识别播放器", "当前页面没有发现可暂停的视频元素。", false);
    }

    private void PauseAutomation_Click(object sender, RoutedEventArgs e)
        => SetAutomationPaused(!_automationPaused);

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        _automationPaused = true;
        if (_adapter is not null)
            await _adapter.PauseVideoAsync();

        if (_activeStat is not null && _lastObservedPlayerSnapshot?.Found == true)
            FinishActiveStat("用户停止", _lastObservedPlayerSnapshot);

        ClearPendingNextVideo();
        _autoAdvanceVisited.Clear();
        ResetCourseTraversal(null);
        _vm.PlaybackStatusText = "视频已暂停";
        _stateMachine.Transition(AppRunState.Stopped, "用户停止当前辅助流程");
        UpdateCompositeStatus();
        ShowInAppNotice("辅助已停止", "视频已暂停，本轮自动查找和切换已经结束。", false);
    }

    private async void ContinueNext_Click(object sender, RoutedEventArgs e)
    {
        if (_adapter is null || _pendingNextVideo is null || _isClosing || _isNavigating || _playerCommandInProgress)
            return;

        _playerCommandInProgress = true;
        var target = _pendingNextVideo;
        try
        {
            await ContinuePendingVideoAsync(target, userInitiated: true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_isClosing) return;
            App.Logger.Error("CX-CONTINUE", "继续播放失败。", ex);
            NotifyUser("继续播放失败", "可直接点击网页播放按钮，或点击“开始”重试。", true);
        }
        finally { _playerCommandInProgress = false; }
    }

    private async void RefreshCourses_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshCoursesInternalAsync(showMessage: true);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            App.Logger.Error("CX-COURSE-REFRESH", "课程列表刷新失败。", ex);
            NotifyUser("课程刷新失败", ex.Message, true);
        }
    }

    private void ReturnCourses_Click(object sender, RoutedEventArgs e)
        => Navigate(ChaoxingConstants.DefaultHomeUrl);

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(App.Settings.Current) { Owner = this };
        if (window.ShowDialog() != true)
            return;

        App.Settings.Replace(window.Result);
        App.Logger.CleanupOldLogs(App.Settings.Current.LogRetentionDays);
        _themeService.Apply(App.Settings.Current.Theme);
        DeveloperPanel.Visibility = App.Settings.Current.EnableDeveloperMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        ShowInAppNotice("设置已保存", "外观、通知和连续播放设置已经生效。", false);

        if (window.ClearLoginRequested && Browser.CoreWebView2 is not null)
        {
            try
            {
                await Browser.CoreWebView2.Profile.ClearBrowsingDataAsync();
                App.Settings.Current.LastCourseTitle = string.Empty;
                App.Settings.Current.LastCourseUrl = string.Empty;
                App.Settings.Save();
                NotifyUser("登录状态已清除", "浏览数据已清除，网页将返回学习通首页。", false);
                Navigate(ChaoxingConstants.DefaultHomeUrl);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                App.Logger.Error("CX-PROFILE-CLEAR", "登录状态清除失败。", ex);
                NotifyUser("清除失败", ex.Message, true);
            }
        }
    }

    private void Stats_Click(object sender, RoutedEventArgs e)
        => new StatsWindow(_statsService) { Owner = this }.ShowDialog();

    private void Logs_Click(object sender, RoutedEventArgs e)
        => new LogWindow(App.Logger) { Owner = this }.ShowDialog();

    private void Tutorial_Click(object sender, RoutedEventArgs e)
        => new FirstRunWindow { Owner = this }.ShowDialog();

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "反馈包可能包含当前页面地址、课程或章节名称以及程序日志，但不会主动包含账号密码或浏览器登录资料。\n\n是否导出？",
            "导出反馈包",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var snapshot = BuildDiagnosticSnapshot();
            var path = _diagnosticService.Export(snapshot);
            System.Windows.MessageBox.Show($"反馈包已保存：\n{path}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);

            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            App.Logger.Error("DIAG-001", "反馈包导出失败。", ex);
            System.Windows.MessageBox.Show(ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ContinueLast_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(App.Settings.Current.LastCourseUrl))
        {
            NotifyUser("没有上次课程", "当前还没有可恢复的课程记录。", false);
            return;
        }

        WelcomePanel.Visibility = Visibility.Collapsed;
        CurrentCourseText.Text = App.Settings.Current.LastCourseTitle;
        Navigate(App.Settings.Current.LastCourseUrl);
    }

    private void ChooseCourse_Click(object sender, RoutedEventArgs e)
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        _scanCoursesAfterNavigation = true;
        Navigate(ChaoxingConstants.DefaultHomeUrl);
    }

    private void WelcomeStats_Click(object sender, RoutedEventArgs e)
        => new StatsWindow(_statsService) { Owner = this }.ShowDialog();

    private void CourseList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_vm.SelectedCourse is not null)
            CurrentCourseText.Text = _vm.SelectedCourse.Title;
    }

    private async void CourseList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_adapter is null || _isClosing || _isNavigating)
            return;

        var source = e.OriginalSource as DependencyObject;
        var item = source is null ? null : System.Windows.Controls.ItemsControl.ContainerFromElement(CourseList, source) as System.Windows.Controls.ListBoxItem;
        if (item?.DataContext is not CourseItem course)
            return;

        e.Handled = true;
        _vm.SelectedCourse = course;
        CurrentCourseText.Text = course.Title;
        WelcomePanel.Visibility = Visibility.Collapsed;
        LibraryTabs.SelectedIndex = 1;

        if (UriEquivalent(Browser.Source?.ToString(), course.Url))
        {
            ShowInAppNotice("正在读取章节", $"正在刷新“{course.Title}”的章节目录。", false);
            await RefreshChaptersInternalAsync(silent: false, preserveSelection: false);
            return;
        }

        _vm.Chapters.Clear();
        _vm.VideoTasks.Clear();
        _vm.SelectedChapter = null;
        _vm.CurrentChapterText = "正在读取章节…";
        ShowInAppNotice("正在进入课程", $"正在打开“{course.Title}”并读取章节目录。", false);
        Navigate(course.Url);
    }

    private async void ChapterList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_manualChapterOpenInProgress || _adapter is null)
            return;
        var source = e.OriginalSource as DependencyObject;
        var item = source is null ? null : System.Windows.Controls.ItemsControl.ContainerFromElement(ChapterList, source) as System.Windows.Controls.ListBoxItem;
        if (item?.DataContext is not ChapterItem chapter)
            return;

        // PreviewMouseLeftButtonDown 阶段拦截 ListBox 默认选中，避免“左边先跳、播放器没变”。
        e.Handled = true;
        await OpenChapterFromLibraryAsync(chapter);
    }

    private async void JumpToUnfinished_Click(object sender, RoutedEventArgs e)
    {
        if (_adapter is null || _manualChapterOpenInProgress || _playerCommandInProgress ||
            _pendingPreparationInProgress)
            return;
        JumpToUnfinishedButton.IsEnabled = false;
        BeginAutomationOperation();
        JumpToUnfinishedButton.Content = "正在查找…";
        ShowInAppNotice("查找未完成视频", "正在刷新任务点和章节状态。", false);
        try
        {
            var catalogResult = await RefreshCatalogUntilStatusReadyAsync(bootstrapWhenEmpty: true);
            if (catalogResult != CatalogRefreshResult.Stable)
            {
                if (catalogResult == CatalogRefreshResult.Unstable)
                    NotifyUser("课程目录仍在加载", "章节状态仍在变化，程序不会提前选择目标。页面稳定后可再次点击查找。", false);
                return;
            }
            var chapter = FindFirstUnfinishedNavigationCandidate(_vm.Chapters) ??
                          FindFirstPendingNavigationCandidate(_vm.Chapters);
            if (chapter is null)
            {
                if (_vm.Chapters.Count == 0 &&
                    ChaoxingUrlClassifier.MayContainChapterCatalogUri(Browser.Source?.ToString()))
                {
                    NotifyUser("章节目录未能加载", "程序已经尝试自动打开首个真实章节，但页面暂时没有提供可用的章节入口。可等待页面加载完成后再试。", false);
                    return;
                }
                NotifyUser("没有可打开的视频", "当前目录没有识别到未完成或待核验的视频任务；可先在网页目录打开目标章节，再点击开始。", false);
                return;
            }
            ShowInAppNotice("正在打开章节", $"正在进入“{chapter.DisplayTitle}”并寻找首个待播视频。", false);
            ChapterList.ScrollIntoView(chapter);
            // 按钮目标是寻找真正待播的视频，不是强制打开某个指定章节。
            // 使用与自然结束相同的待播准备链，进入后可复核该章视频并继续向后跳过只剩测验的章节。
            _autoAdvanceVisited.Clear();
            ResetCourseTraversal(chapter);
            await QueueAndOpenNextChapterAsync(chapter);
        }
        finally
        {
            JumpToUnfinishedButton.Content = "打开未完成章节";
            JumpToUnfinishedButton.IsEnabled = true;
        }
    }

    private async Task OpenChapterFromLibraryAsync(ChapterItem chapter)
    {
        if (_adapter is null || _manualChapterOpenInProgress || _playerCommandInProgress ||
            _pendingPreparationInProgress || _isClosing)
            return;
        _manualChapterOpenInProgress = true;
        BeginAutomationOperation();
        try
        {
            WelcomePanel.Visibility = Visibility.Collapsed;
            ClearPendingNextVideo(clearSelection: false);
            _autoAdvanceVisited.Clear();
            ResetCourseTraversal(chapter);
            _requestedChapter = chapter;
            _requestedChapterAt = DateTime.Now;
            _stateMachine.Transition(AppRunState.Loading, $"请求打开章节：{chapter.DisplayTitle}");

            var before = await _adapter.GetPlayerSnapshotAsync();
            var beforeIdentity = PlayerIdentity(before);
            _requestedOriginPlayerIdentity = beforeIdentity;
            var targetWasCurrent = IsConfirmedChapter(chapter);

            if (targetWasCurrent)
            {
                var focusRequested = await _adapter.FocusFirstUnfinishedVideoTaskAsync();
                if (focusRequested)
                {
                    // 当前章节里若还有另一个未完成视频，必须等真实播放器身份变化；
                    // 不再用 allowCurrentPlayer=true 把旧播放器直接当成“已切换成功”。
                    var sameChapterPlayer = await WaitForManualPlayerSwitchAsync(chapter, before, allowCurrentPlayer: false);
                    if (sameChapterPlayer is not null)
                    {
                        await CompleteManualChapterSwitchAsync(chapter, sameChapterPlayer);
                        return;
                    }
                }

                // 如果目标本来就是当前章节且没有其它可切的视频，保持当前真实播放器；
                // 这里只维持已确认状态，不记录为一次“播放器切换”。
                var currentPlayer = await _adapter.GetPlayerSnapshotAsync();
                if (currentPlayer.Found && await PlayerMatchesTargetChapterAsync(chapter, currentPlayer, allowCatalogEvidence: true))
                    await CompleteManualChapterSwitchAsync(chapter, currentPlayer);
                else
                {
                    _requestedChapter = null;
                    _requestedOriginPlayerIdentity = string.Empty;
                    _requestedChapterAt = DateTime.MinValue;
                }
                return;
            }

            var openedByPage = await _adapter.OpenChapterAsync(chapter);
            if (openedByPage)
            {
                await Task.Delay(300, _lifetimeCts.Token);
                var switched = await WaitForManualPlayerSwitchAsync(chapter, before, allowCurrentPlayer: false);
                if (switched is not null)
                {
                    await CompleteManualChapterSwitchAsync(chapter, switched);
                    return;
                }
            }

            // 普通 click 只改高亮时，继续执行网页原始 toOld(...)，并保留其完整参数（含常见第 4 参数）。
            var invokedNative = await _adapter.InvokeNativeChapterActionAsync(chapter);
            if (invokedNative)
            {
                var switched = await WaitForManualPlayerSwitchAsync(chapter, before, allowCurrentPlayer: false);
                if (switched is not null)
                {
                    await CompleteManualChapterSwitchAsync(chapter, switched);
                    return;
                }
            }

            // 最后升级为当前学习页真实导航：完整保留 cpi/enc/openc 等已有参数，只替换目标 chapterId。
            var contextUrl = await _adapter.BuildContextPreservingChapterUrlAsync(chapter);
            if (!string.IsNullOrWhiteSpace(contextUrl) && !UriEquivalent(contextUrl, Browser.Source?.ToString()))
            {
                App.Logger.Info("CX-CHAPTER-NAV-DIRECT", $"目录 click/toOld 未切播放器，使用当前学习上下文直接进入章节：{chapter.DisplayTitle}");
                _focusUnfinishedAfterNavigation = true;
                _manualPlaybackAfterNavigationTarget = chapter;
                _manualPlaybackOriginSnapshot = before;
                Navigate(contextUrl);
                return;
            }

            if (!chapter.IsSyntheticUrl && IsSafeChapterFallbackUrl(chapter.Url) && !UriEquivalent(chapter.Url, Browser.Source?.ToString()))
            {
                App.Logger.Info("CX-CHAPTER-NAV-FALLBACK", $"上下文真实导航不可用，尝试目录 href：{chapter.DisplayTitle}");
                _focusUnfinishedAfterNavigation = true;
                _manualPlaybackAfterNavigationTarget = chapter;
                _manualPlaybackOriginSnapshot = before;
                Navigate(chapter.Url);
                return;
            }

            _requestedChapter = null;
            _requestedOriginPlayerIdentity = string.Empty;
            _requestedChapterAt = DateTime.MinValue;
            _manualPlaybackAfterNavigationTarget = null;
            _manualPlaybackOriginSnapshot = null;
            _stateMachine.Transition(AppRunState.PageRecognitionFailed, "章节所有真实导航路径均未造成播放器切换");
            NotifyUser("章节暂时无法打开", $"“{chapter.DisplayTitle}”已尝试目录点击、网页原始章节函数和真实学习页导航，但平台仍未加载目标视频。", false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _requestedChapter = null;
            _requestedOriginPlayerIdentity = string.Empty;
            _requestedChapterAt = DateTime.MinValue;
            _manualPlaybackAfterNavigationTarget = null;
            _manualPlaybackOriginSnapshot = null;
            _lastError = ex.Message;
            SetAutomationPaused(true);
            App.Logger.Error("CX-CHAPTER-OPEN", "章节打开失败，可重新选择章节重试。", ex);
            _stateMachine.Transition(AppRunState.PageRecognitionFailed, "章节打开失败");
            NotifyUser("章节打开失败", "网页暂时无法完成切换，请等待页面稳定后重新点击章节。详情已记录到日志。", false);
        }
        finally
        {
            _manualChapterOpenInProgress = false;
        }
    }

    private async Task CompleteManualChapterSwitchAsync(ChapterItem chapter, PlayerSnapshot snapshot)
    {
        if (_adapter is null || _isClosing)
            return;

        if (!PlayerMediaEvidence.IsReadyForPlayback(snapshot))
        {
            NotifyUser("视频仍在加载", "章节已经打开，但真实播放器尚未准备完成。程序不会提前确认切换，可稍后重新点击该章节。", false);
            return;
        }

        _manualPlaybackAfterNavigationTarget = null;
        _manualPlaybackOriginSnapshot = null;
        _lastPlayerIdentity = PlayerIdentity(snapshot);
        ConfirmCurrentChapter(chapter, snapshot, updateVideoTitle: true);
        await RefreshChaptersInternalAsync(silent: true, playerEvidence: snapshot, preserveSelection: true);

        var playback = await StartPlaybackWithRetryAsync(snapshot, $"手动切换章节“{chapter.DisplayTitle}”");
        var current = playback.Snapshot;
        if (current.Found)
        {
            _lastPlayerIdentity = PlayerIdentity(current);
            ConfirmCurrentChapter(chapter, current, updateVideoTitle: true);
        }

        if (playback.Playing)
        {
            _vm.PlaybackStatusText = "视频播放中";
            _stateMachine.Transition(AppRunState.Playing, "手动切换章节后已自动播放");
            App.Logger.Info("CX-MANUAL-CHAPTER-AUTOPLAY", $"章节切换并自动播放成功：{chapter.DisplayTitle}");
        }
        else
        {
            _vm.PlaybackStatusText = "章节已打开，等待播放";
            _stateMachine.Transition(AppRunState.VideoPaused, "章节已切换，但浏览器未确认自动播放");
            NotifyUser("章节已打开", $"“{chapter.DisplayTitle}”已经切换成功，但浏览器没有允许自动播放；可直接点击网页播放按钮。", false);
        }

        UpdateCompositeStatus();
        await PollPlayerAsync(force: true);
    }

    private async Task<bool> HasTargetChapterPageEvidenceAsync(ChapterItem target)
    {
        if (_adapter is null || _isClosing) return false;
        if (ChapterMatchesUri(target, Browser.Source?.ToString())) return true;

        var chapters = await _adapter.ScanChaptersAsync();
        return chapters.Any(x => x.IsActive &&
            string.Equals(ChapterIdentity(x), ChapterIdentity(target), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PlayerSnapshot?> WaitForManualPlayerSwitchAsync(ChapterItem target, PlayerSnapshot beforeSnapshot, bool allowCurrentPlayer)
    {
        if (_adapter is null) return null;
        var expectedGeneration = _automationGeneration;

        for (var attempt = 0; attempt < 18; attempt++)
        {
            if (_isClosing || !IsAutomationOperationCurrent(expectedGeneration)) return null;
            if (_isNavigating)
            {
                await Task.Delay(300, _lifetimeCts.Token);
                continue;
            }

            var snapshot = await _adapter.GetPlayerSnapshotAsync();
            if (!IsAutomationOperationCurrent(expectedGeneration)) return null;
            if (PlayerMediaEvidence.IsReadyForPlayback(snapshot))
            {
                var identity = PlayerIdentity(snapshot);
                var changed = !beforeSnapshot.Found || !IsSameMediaEvidence(beforeSnapshot, snapshot);
                var refreshStructure = attempt is 0 or 4 or 9 or 13 or 17;
                var belongsToTarget = await PlayerMatchesTargetChapterAsync(target, snapshot, allowCatalogEvidence: refreshStructure);
                if (belongsToTarget && (changed || allowCurrentPlayer))
                {
                    App.Logger.Info("CX-MANUAL-CHAPTER-CONFIRMED",
                        $"目标章节与真实播放器同时确认：{target.DisplayTitle}；changed={changed}；{ShortIdentity(identity)}");
                    return snapshot;
                }
            }

            if ((attempt == 4 || attempt == 9 || attempt == 13) && await HasTargetChapterPageEvidenceAsync(target))
                await _adapter.FocusFirstUnfinishedVideoTaskAsync();

            await Task.Delay(300, _lifetimeCts.Token);
        }
        return null;
    }

    private async void ScrollLeft_Click(object sender, RoutedEventArgs e)
        => await ScrollPageHorizontallyAsync(-1);

    private async void ScrollRight_Click(object sender, RoutedEventArgs e)
        => await ScrollPageHorizontallyAsync(1);

    private async Task ScrollPageHorizontallyAsync(int direction)
    {
        if (Browser.CoreWebView2 is null)
            return;

        var signedDirection = direction < 0 ? -1 : 1;
        var script = $$"""
            (() => {
              const direction = {{signedDirection}};
              const docs = [document];
              for (const frame of document.querySelectorAll('iframe')) {
                try {
                  if (frame.contentDocument) docs.push(frame.contentDocument);
                } catch {}
              }

              let best = null;
              let bestOverflow = 0;
              for (const doc of docs) {
                const candidates = [doc.scrollingElement, doc.documentElement, doc.body]
                  .filter(Boolean)
                  .concat(Array.from(doc.querySelectorAll('*')));
                for (const el of candidates) {
                  try {
                    const overflow = el.scrollWidth - el.clientWidth;
                    if (overflow > bestOverflow + 4) {
                      bestOverflow = overflow;
                      best = el;
                    }
                  } catch {}
                }
              }

              if (!best) return false;
              const amount = Math.max(360, Math.round(window.innerWidth * 0.68));
              best.scrollBy({ left: direction * amount, behavior: 'smooth' });
              return true;
            })();
            """;

        try
        {
            await Browser.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            App.Logger.Warn("UI-HSCROLL", $"网页横向移动失败：{ex.Message}");
        }
    }

    private void ToggleViewingMode_Click(object sender, RoutedEventArgs e)
        => ToggleCompactViewingMode();

    private void ExitFullscreen_Click(object sender, RoutedEventArgs e)
        => ExitCompactViewingMode();

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleCompactViewingMode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _compactViewingMode)
        {
            ExitCompactViewingMode();
            e.Handled = true;
        }
    }

    private void ToggleCompactViewingMode()
    {
        if (_compactViewingMode)
            ExitCompactViewingMode();
        else
            EnterCompactViewingMode();
    }

    private void EnterCompactViewingMode()
    {
        if (_compactViewingMode)
            return;
        _compactViewingMode = true;
        _workspaceMarginBeforeCompact = MainWorkspace.Margin;
        _leftColumnBeforeCompact = LeftNavigationColumn.Width;
        _leftSplitterBeforeCompact = LeftSplitterColumn.Width;
        _rightSplitterBeforeCompact = RightSplitterColumn.Width;
        _telemetryColumnBeforeCompact = TelemetryColumn.Width;
        _rightTelemetryVisibilityBeforeCompact = RightTelemetryCard.Visibility;
        _windowStateBeforeCompact = WindowState;

        TopCommandDeck.Visibility = Visibility.Collapsed;
        FooterBar.Visibility = Visibility.Collapsed;
        LeftNavigationCard.Visibility = Visibility.Collapsed;
        LeftWorkspaceSplitter.Visibility = Visibility.Collapsed;
        BrowserToolbar.Visibility = Visibility.Collapsed;
        BrowserFooterDashboard.Visibility = Visibility.Collapsed;
        FullscreenExitBar.Visibility = Visibility.Visible;
        TopCommandRow.Height = new GridLength(46);
        FooterRow.Height = new GridLength(0);
        BrowserHeaderRow.Height = new GridLength(0);
        BrowserFooterRow.Height = new GridLength(0);
        LeftNavigationColumn.Width = new GridLength(0);
        LeftSplitterColumn.Width = new GridLength(0);
        RightSplitterColumn.Width = new GridLength(0);
        TelemetryColumn.Width = new GridLength(0);
        RightWorkspaceSplitter.Visibility = Visibility.Collapsed;
        RightTelemetryCard.Visibility = Visibility.Collapsed;
        BrowserWorkspaceCard.Padding = new Thickness(0);
        BrowserWorkspaceCard.BorderThickness = new Thickness(0);
        BrowserWorkspaceCard.CornerRadius = new CornerRadius(0);
        MainWorkspace.Margin = new Thickness(0);
        WindowState = WindowState.Maximized;

        ViewingModeButton.Content = "▤";
        ViewingModeButton.ToolTip = "退出全屏观看（Esc / F11）";
        BottomHintText.Text = "全屏网页观看已启用；按 Esc 或 F11 退出。";
        ShowInAppNotice("全屏观看已开启", "课程网页已经铺满窗口；按 Esc 或 F11 返回。", false);
        App.Logger.Info("UI-COMPACT-VIEW", "进入全屏网页观看模式。");
    }

    private void ExitCompactViewingMode()
    {
        if (!_compactViewingMode)
            return;
        _compactViewingMode = false;
        LeftNavigationColumn.Width = _leftColumnBeforeCompact;
        LeftSplitterColumn.Width = _leftSplitterBeforeCompact;
        RightSplitterColumn.Width = _rightSplitterBeforeCompact;
        TelemetryColumn.Width = _telemetryColumnBeforeCompact;
        MainWorkspace.Margin = _workspaceMarginBeforeCompact;
        TopCommandRow.Height = new GridLength(92);
        FooterRow.Height = new GridLength(38);
        BrowserHeaderRow.Height = new GridLength(96);
        BrowserFooterRow.Height = new GridLength(132);
        TopCommandDeck.Visibility = Visibility.Visible;
        FullscreenExitBar.Visibility = Visibility.Collapsed;
        FooterBar.Visibility = Visibility.Visible;
        LeftNavigationCard.Visibility = Visibility.Visible;
        LeftWorkspaceSplitter.Visibility = Visibility.Visible;
        BrowserToolbar.Visibility = Visibility.Visible;
        BrowserFooterDashboard.Visibility = Visibility.Visible;
        RightWorkspaceSplitter.Visibility = Visibility.Visible;
        RightTelemetryCard.Visibility = _rightTelemetryVisibilityBeforeCompact;
        BrowserWorkspaceCard.Padding = new Thickness(7);
        BrowserWorkspaceCard.BorderThickness = new Thickness(2);
        BrowserWorkspaceCard.CornerRadius = new CornerRadius(14);
        WindowState = _windowStateBeforeCompact;

        ViewingModeButton.Content = "▣";
        ViewingModeButton.ToolTip = "全屏观看（F11）";
        BottomHintText.Text = "已退出全屏观看。";
        ShowInAppNotice("全屏观看已关闭", "课程目录、播放舱和运行状态已经恢复。", false);
        App.Logger.Info("UI-COMPACT-VIEW", "退出全屏网页观看模式。");
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true)
            Browser.CoreWebView2.GoBack();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
        => Browser.CoreWebView2?.Reload();

    private void OpenAddress_Click(object sender, RoutedEventArgs e)
    {
        var text = AddressBox.Text.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            System.Windows.MessageBox.Show("请输入完整的 http/https 网页地址。", "地址无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        WelcomePanel.Visibility = Visibility.Collapsed;
        Navigate(uri.ToString());
    }

    private void Navigate(string? url)
    {
        if (Browser.CoreWebView2 is null || string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            App.Logger.Warn("CX-NAV-URL", $"拒绝无效地址：{url}");
            return;
        }

        Browser.CoreWebView2.Navigate(uri.ToString());
    }

    private void SetAutomationPaused(bool paused)
    {
        if (paused && !_automationPaused)
            BeginAutomationOperation();
        _automationPaused = paused;
        PauseAutomationButton.Content = paused ? "继续辅助" : "暂停辅助";
        _vm.AutomationStatusText = paused ? "辅助未启用" : "辅助运行中";

        if (paused)
            _stateMachine.Transition(AppRunState.AutomationPaused, "辅助流程暂停");
        else
        {
            _stateMachine.Transition(AppRunState.Idle, "辅助流程恢复");
            _ = AnalyzeCurrentPageAsync();
        }
        UpdateCompositeStatus();
        ShowInAppNotice(
            paused ? "自动辅助已暂停" : "自动辅助已开启",
            paused ? "当前视频不受影响，但不会自动查找和切换下一视频。" : "将继续识别未完成视频，并在自然结束后自动续播。",
            false);
    }

    private long BeginAutomationOperation()
        => Interlocked.Increment(ref _automationGeneration);

    private bool IsAutomationOperationCurrent(long generation)
        => generation == Interlocked.Read(ref _automationGeneration);

    private void StopAfterUnconfirmedNextVideo(string message)
    {
        _stateMachine.Transition(AppRunState.WaitingForUser, "下一视频切换尚未确认");
        _vm.PlaybackStatusText = "下一视频尚未切换成功";
        UpdateCompositeStatus();
        NotifyUser("下一视频没有跳过", message + " 可等待页面稳定后再次点击开始。", false);
    }

    private async void ShowInAppNotice(string title, string message, bool isError)
    {
        if (_isClosing || !IsLoaded)
            return;

        var version = ++_noticeVersion;
        NoticeTitleText.Text = title;
        NoticeMessageText.Text = message;
        NoticeAccentBar.Background = (System.Windows.Media.Brush)FindResource(isError ? "DangerBrush" : "AccentBrush");
        InAppNotice.Visibility = Visibility.Visible;
        InAppNotice.Opacity = 1;

        try
        {
            await Task.Delay(isError ? 6000 : 3600, _lifetimeCts.Token);
            if (!_isClosing && version == _noticeVersion)
                InAppNotice.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void NotifyUser(string title, string message, bool isError)
    {
        if (_isClosing) return;
        if (App.Settings.Current.EnableInAppNotifications || isError)
        {
            BottomHintText.Text = message;
            BottomHintText.ToolTip = message;
            ShowInAppNotice(title, message, isError);
        }

        // 网页回调中的模态对话框会触发 WebView2 不支持的嵌套消息循环。
        // 状态栏保留详情，相同通知限频；致命 UI 错误由 App 统一处理一次。
        if (!_notificationThrottle.ShouldNotify($"{title}|{message}", DateTimeOffset.UtcNow)) return;
        if (isError) App.Logger.Warn("UI-NOTICE", $"{title}：{message}");
        else App.Logger.Info("UI-NOTICE", $"{title}：{message}");

        if (App.Settings.Current.EnableWindowsNotifications)
        {
            _trayService.Show(
                title,
                message,
                isError ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.Info);
        }

        if (App.Settings.Current.EnableSound)
        {
            try
            {
                if (isError)
                    SystemSounds.Exclamation.Play();
                else
                    SystemSounds.Asterisk.Play();
            }
            catch
            {
            }
        }
    }

    private DiagnosticSnapshot BuildDiagnosticSnapshot()
        => new()
        {
            CurrentUrl = Browser.Source?.ToString() ?? string.Empty,
            PageTitle = _lastRecognition.Title,
            CourseTitle = _vm.SelectedCourse?.Title ?? App.Settings.Current.LastCourseTitle,
            ChapterTitle = _vm.SelectedChapter?.Title ?? _lastRecognition.Title,
            TaskType = _lastRecognition.TaskType,
            State = _stateMachine.Current,
            Player = _lastObservedPlayerSnapshot ?? new PlayerSnapshot { PlaybackRate = _lastObservedPlaybackRate },
            SelectorSummary = _lastRecognition.SelectorSummary,
            LastError = _lastError,
            RecoveryAttempt = _navigationRetryCount,
            CapturedAt = DateTime.Now
        };

    private void UpdateDeveloperPanel()
    {
        if (!App.Settings.Current.EnableDeveloperMode)
            return;

        DevUrlText.Text = $"URL：{Browser.Source}";
        DevTaskText.Text = $"页面类型：{_lastRecognition.TaskType}；状态：{_stateMachine.Current}";
        DevSelectorText.Text = $"识别器：{_lastRecognition.SelectorSummary}";
        DevErrorText.Text = $"最近错误：{(string.IsNullOrWhiteSpace(_lastError) ? "-" : _lastError)}";
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = _compactViewingMode ? WindowState.Maximized : WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void LoadCachedCourses()
    {
        if (_vm.Courses.Count > 0) return;
        foreach (var course in _courseCacheService.Load().Where(x => !IsGenericCourseTitle(x.Title)))
            _vm.Courses.Add(course);
        if (_vm.Courses.Count > 0)
            App.Logger.Info("COURSE-CACHE", $"已载入 {_vm.Courses.Count} 条最近课程缓存。");
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // v1.16: 最小化继续保持普通任务栏窗口，不自动 Hide 到托盘。
        // 托盘图标仍用于通知和快速恢复，但不会让 EXE 从任务栏“消失”。
        if (WindowState == WindowState.Minimized)
        {
            _ambientMotion?.Pause(this);
            ShowInTaskbar = true;
            App.Logger.Debug("UI-MINIMIZE", "窗口已最小化并保留任务栏入口。");
        }
        else
        {
            _ambientMotion?.Resume(this);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _playerTimer?.Stop();
        _ambientMotion?.Stop(this);
        _lifetimeCts.Cancel();
        _navigationWorkCts?.Cancel();
        _navigationTimeoutCts?.Cancel();

        if (!_fatalExit)
        {
            if (_activeStat is not null)
            {
                var snapshot = _lastObservedPlayerSnapshot ?? new PlayerSnapshot
                {
                    Found = true,
                    PlaybackRate = _lastObservedPlaybackRate,
                    Duration = _activeStat.VideoDurationSeconds
                };
                FinishActiveStat("程序退出", snapshot);
            }

            _sessionService.MarkCleanExit();
            App.Settings.Save();
        }
        _adapter?.Dispose();
        _trayService.Dispose();
        Browser.Dispose();
        _navigationTimeoutCts?.Dispose();
        _navigationWorkCts?.Dispose();
    }

    public void PrepareForFatalExit()
    {
        _fatalExit = true;
        _isClosing = true;
        _playerTimer?.Stop();
        try
        {
            _lifetimeCts.Cancel();
            _navigationWorkCts?.Cancel();
            _navigationTimeoutCts?.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    private bool IsCurrentPage(int pageVersion)
        => !_isClosing && !_isNavigating && pageVersion == _pageVersion;

    private static string ExtractCourseId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return string.Empty;

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            var key = Uri.UnescapeDataString(pair[0]);
            if (!key.Equals("courseId", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("courseid", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("courseId_", StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(pair[1]);
        }

        return string.Empty;
    }

    private static bool SameCourseContext(string? left, string? right)
    {
        var a = ExtractCourseId(left);
        var b = ExtractCourseId(right);
        if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        return UriEquivalent(left, right);
    }

    private static string ExtractChapterId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return string.Empty;

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            var key = Uri.UnescapeDataString(pair[0]);
            if (!key.Equals("chapterId", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("knowledgeId", StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(pair[1]);
        }

        return string.Empty;
    }

    private static bool ChapterMatchesUri(ChapterItem chapter, string? uri)
    {
        if (chapter is null || string.IsNullOrWhiteSpace(uri))
            return false;

        if (!string.IsNullOrWhiteSpace(chapter.ChapterId))
        {
            var currentId = ExtractChapterId(uri);
            if (!string.IsNullOrWhiteSpace(currentId) &&
                string.Equals(chapter.ChapterId, currentId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return !string.IsNullOrWhiteSpace(chapter.Url) && UriEquivalent(chapter.Url, uri);
    }

    private static bool UriEquivalent(string? left, string? right)
    {
        if (!Uri.TryCreate(left, UriKind.Absolute, out var a) ||
            !Uri.TryCreate(right, UriKind.Absolute, out var b))
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.AbsolutePath.TrimEnd('/'), b.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Query, b.Query, StringComparison.OrdinalIgnoreCase);
    }
}
