from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors = []

for path in list(ROOT.rglob("*.xaml")) + list(ROOT.rglob("*.csproj")):
    try:
        ET.parse(path)
    except Exception as exc:
        errors.append(f"XML/XAML 解析失败: {path.relative_to(ROOT)}: {exc}")

for xaml in ROOT.rglob("*.xaml"):
    codebehind = Path(str(xaml) + ".cs")
    if not codebehind.exists():
        continue

    xaml_text = xaml.read_text(encoding="utf-8")
    cs_text = codebehind.read_text(encoding="utf-8")
    handlers = set(re.findall(
        r'\b(?:Click|Loaded|SelectionChanged|MouseDoubleClick|PreviewMouseLeftButtonUp|PreviewMouseLeftButtonDown|Closing)="([A-Za-z_][A-Za-z0-9_]*)"',
        xaml_text))

    for handler in handlers:
        if re.search(rf'\b{re.escape(handler)}\s*\(', cs_text) is None:
            errors.append(f"缺少事件处理器: {xaml.relative_to(ROOT)} -> {handler}")

for path in ROOT.rglob("*"):
    if not path.is_file() or path.suffix.lower() not in {".cs", ".xaml", ".ps1", ".iss"}:
        continue

    text = path.read_text(encoding="utf-8", errors="ignore")
    for token in ("TODO", "FIXME", "自行实现"):
        if token in text:
            errors.append(f"发现占位标记 {token}: {path.relative_to(ROOT)}")


# WPF + WinForms projects can make several type names ambiguous.
# Keep these WPF references explicitly qualified so a future refactor does not
# reintroduce CS0104 errors when <UseWindowsForms>true</UseWindowsForms> is enabled.
ambiguity_patterns = {
    r"public\s+partial\s+class\s+App\s*:\s*Application\b": "unqualified WPF Application base type",
    r"(?<![.\w])Application\.Current\b": "unqualified Application.Current",
    r"(?<![.\w])MessageBox\.Show\s*\(": "unqualified MessageBox.Show",
    r"\(Color\)\s*ColorConverter\.ConvertFromString": "unqualified WPF Color cast",
}
for path in ROOT.rglob("*.cs"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    for pattern, label in ambiguity_patterns.items():
        if re.search(pattern, text):
            errors.append(f"Potential WPF/WinForms ambiguity ({label}): {path.relative_to(ROOT)}")


# v1.5 regression guards for real Windows build failures.
global_usings = ROOT / "src" / "ChaoxingLearningAssistant" / "GlobalUsings.cs"
if not global_usings.exists():
    errors.append("Missing GlobalUsings.cs for explicit System.IO aliases")
else:
    gu = global_usings.read_text(encoding="utf-8", errors="ignore")
    for required in (
        "global using File = System.IO.File;",
        "global using Path = System.IO.Path;",
        "global using Directory = System.IO.Directory;",
    ):
        if required not in gu:
            errors.append(f"Missing required System.IO alias: {required}")

for path in ROOT.rglob("*.cs"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    if re.search(r'(?<![.\w])ColorConverter\.ConvertFromString', text):
        errors.append(f"Unqualified ColorConverter may cause CS0104: {path.relative_to(ROOT)}")


# v1.6 regression guard: dotnet clean does not accept --no-restore.
build_ps1 = ROOT / "scripts" / "build.ps1"
if build_ps1.exists():
    ps = build_ps1.read_text(encoding="utf-8-sig", errors="ignore")
    if re.search(r"Run-DotNet\s+-Name\s+['\"]Clean['\"].*--no-restore", ps):
        errors.append("ERR-SCRIPT-002 regression: dotnet clean must not use --no-restore")




# v1.9 regression guard for ERR-BUILD-004.
# In a project that enables both WPF and WinForms, an unqualified Color cast can
# resolve ambiguously between System.Drawing.Color and System.Windows.Media.Color.
for path in ROOT.rglob("*.cs"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    if re.search(r"\(\s*Color\s*\)", text):
        errors.append(f"ERR-BUILD-004 regression: unqualified Color cast: {path.relative_to(ROOT)}")

# v1.8 UI regression guards: high-contrast visual system.
app_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "App.xaml"
main_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "Views" / "MainWindow.xaml"
if app_xaml.exists():
    ui = app_xaml.read_text(encoding="utf-8", errors="ignore")
    required_ui_tokens = (
        'x:Key="SurfaceElevatedBrush"',
        'x:Key="AccentSecondaryBrush"',
        'x:Key="PrimaryButtonStyle"',
        'x:Key="GradientButtonStyle"',
        'x:Key="CardBorderStyle"',
        'Color="#EAF7F8"',
        'Color="#B8DDE1"',
    )
    for token in required_ui_tokens:
        if token not in ui:
            errors.append(f"UI-REV regression: missing visual-system token {token}")
if main_xaml.exists():
    main_ui = main_xaml.read_text(encoding="utf-8", errors="ignore")
    for token in ('学习通课程视频播放助手', '运行状态', 'HeroGradientBrush', 'GradientButtonStyle'):
        if token not in main_ui:
            errors.append(f"UI-REV regression: MainWindow missing {token}")


# v1.8 native-control and launcher regression guards.
if app_xaml.exists():
    ui = app_xaml.read_text(encoding="utf-8", errors="ignore")
    for token in ('Style TargetType="ComboBoxItem"', 'Style TargetType="DataGridColumnHeader"', 'Style TargetType="DataGridCell"'):
        if token not in ui:
            errors.append(f"UI-REV regression: missing native-control style {token}")
    # Implicit styles are keyed by TargetType. Duplicate declarations can fail WPF resource loading.
    for target in ("ComboBoxItem", "DataGridColumnHeader", "DataGridCell"):
        count = len(re.findall(rf'<Style\s+TargetType="{target}"', ui))
        if count != 1:
            errors.append(f"UI-REV regression: expected exactly one implicit {target} style, found {count}")

launcher = ROOT / "BUILD_V1_39.bat"
if not launcher.exists():
    errors.append("Missing BUILD_V1_39.bat")
else:
    raw = launcher.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf") or any(ch >= 128 for ch in raw):
        errors.append("BUILD_V1_39.bat must remain ASCII/no-BOM")
    if b"Build v1.39" not in raw:
        errors.append("BUILD_V1_39.bat version banner mismatch")


# v1.10 real-runtime UI acceptance regression guards.
if main_xaml.exists():
    main_ui = main_xaml.read_text(encoding="utf-8", errors="ignore")
    required_v20_tokens = (
        'x:Name="ViewingModeButton"',
        'Click="ToggleViewingMode_Click"',
        'Click="ScrollLeft_Click"',
        'Click="ScrollRight_Click"',
        'x:Name="TopCommandDeck"',
        'x:Name="MainWorkspace"',
        'x:Name="LeftNavigationColumn"',
        'x:Name="TelemetryColumn"',
        'ToolTip="{Binding PlayerTimeText}"',
        'FontFamily="Consolas"',
    )
    for token in required_v20_tokens:
        if token not in main_ui:
            errors.append(f"UI-REV v1.10 regression: MainWindow missing {token}")

main_cs = ROOT / "src" / "ChaoxingLearningAssistant" / "Views" / "MainWindow.xaml.cs"
if main_cs.exists():
    main_code = main_cs.read_text(encoding="utf-8", errors="ignore")
    for token in (
        "ToggleCompactViewingMode",
        "ScrollPageHorizontallyAsync",
        "Key.F11",
        "Key.Escape",
        "UI-COMPACT-VIEW",
        "UI-HSCROLL",
    ):
        if token not in main_code:
            errors.append(f"UI-REV v1.10 regression: MainWindow code missing {token}")


# v1.11 regression guard for mixed WPF/WinForms input-event ambiguity.
for path in ROOT.rglob("*.cs"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    patterns = {
        r"(?<![\w.])KeyEventArgs(?![\w.])": "KeyEventArgs",
        r"(?<![\w.])MouseEventArgs(?![\w.])": "MouseEventArgs",
    }
    for pattern, name in patterns.items():
        if re.search(pattern, text):
            errors.append(f"ERR-BUILD-005 family regression: unqualified {name}: {path.relative_to(ROOT)}")


# v1.12 regression guard for runtime ProgressBar binding failures.
if main_xaml.exists():
    main_ui_v112 = main_xaml.read_text(encoding="utf-8", errors="ignore")
    for match in re.finditer(r"<ProgressBar\b[^>]*Value=\"\{Binding\s+ProgressPercent([^}]*)\}\"[^>]*/?>", main_ui_v112):
        binding_tail = match.group(1)
        if "Mode=OneWay" not in binding_tail.replace(" ", ""):
            errors.append("ERR-RUNTIME-001 regression: ProgressPercent ProgressBar binding must be Mode=OneWay")


launcher_v112 = ROOT / "BUILD_V1_39.bat"
if launcher_v112.exists():
    launcher_text_v112 = launcher_v112.read_text(encoding="ascii", errors="ignore")
    if "Build Log v1.39" not in launcher_text_v112:
        errors.append("VER-MGMT-002 regression: BUILD_LOG header must match v1.39")

# v1.13: inspect XML attribute values rather than stopping at StringFormat's nested braces.
for path in ROOT.rglob("*.xaml"):
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        continue
    for node in root.iter():
        tag = node.tag.rsplit("}", 1)[-1]
        for name, value in node.attrib.items():
            if not value.startswith("{Binding"):
                continue
            display_binding = (tag == "Run" and name == "Text") or (tag == "ProgressBar" and name == "Value")
            if display_binding and not re.search(r"Mode\s*=\s*OneWay(?:\s*[,}]|$)", value):
                errors.append(f"ERR-RUNTIME-002: display-only {tag}.{name} requires OneWay: {path.relative_to(ROOT)}")

project_xml = ET.parse(ROOT / "src/ChaoxingLearningAssistant/ChaoxingLearningAssistant.csproj")
if project_xml.findtext(".//Version") != "1.39.0":
    errors.append("Project version must be 1.39.0")

# v1.14: chapter catalog, continuous playback, and login-stability guards.
chapter_model = ROOT / "src/ChaoxingLearningAssistant/Models/ChapterItem.cs"
adapter_cs = ROOT / "src/ChaoxingLearningAssistant/Chaoxing/ChaoxingAdapter.cs"
url_classifier = ROOT / "src/ChaoxingLearningAssistant/Chaoxing/ChaoxingUrlClassifier.cs"
settings_model = ROOT / "src/ChaoxingLearningAssistant/Models/AppSettings.cs"
for path, tokens in {
    chapter_model: ("ChapterId", "IsNavigationCandidate", "IsSyntheticUrl"),
    adapter_cs: ('.chapter_item[onclick*="toOld"]', '.ncells h4 > a', "OpenChapterAsync", "isSyntheticUrl", "documentVersionBeforeClick"),
    main_cs: ("AutoPlayNextVideo", "PreservePlaybackRate", "IsSafeChapterFallbackUrl", "HandleLoginPageReached", "CX-LOGIN-RECOVERY"),
    url_classifier: ("IsLoginUri", "IsStudyUri", "passport", "/cas/login"),
    settings_model: ("AutoPlayNextVideo", "PreservePlaybackRate"),
}.items():
    if not path.exists():
        errors.append(f"v1.14 missing file: {path.relative_to(ROOT)}")
        continue
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in text:
            errors.append(f"v1.14 regression: {path.relative_to(ROOT)} missing {token}")



# v1.15: runtime-status clarity, taskbar-minimize behavior, broader catalog scan,
# native next-chapter fallback, and dark-settings readability.
main_vm = ROOT / "src/ChaoxingLearningAssistant/ViewModels/MainViewModel.cs"
settings_xaml = ROOT / "src/ChaoxingLearningAssistant/Views/SettingsWindow.xaml"
theme_cs = ROOT / "src/ChaoxingLearningAssistant/Services/ThemeService.cs"
for path, tokens in {
    adapter_cs: ('.posCatalog_select[id^="cur"]', '.menulist-menu-title[id^="cur"]', '.catalog_title', 'FindNextChapterCandidateAsync'),
    main_cs: ('UpdateCompositeStatus', 'CX-AUTO-NEXT-FALLBACK', 'ShowInTaskbar = true', 'UI-MINIMIZE', '_lastChapterRefreshAttempt'),
    main_vm: ('PlaybackStatusText', 'AutomationStatusText'),
    settings_xaml: ('TextElement.Foreground="{DynamicResource PrimaryTextBrush}"', 'Background="{DynamicResource WindowBackgroundBrush}"'),
    theme_cs: ('#CDE1D4', '#9FC5AE'),
}.items():
    if not path.exists():
        errors.append(f"v1.15 missing file: {path.relative_to(ROOT)}")
        continue
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in text:
            errors.append(f"v1.15 regression: {path.relative_to(ROOT)} missing {token}")

if main_cs.exists():
    main_code_v115 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    state_block = re.search(r"protected override void OnStateChanged\(EventArgs e\)([\s\S]*?)private void MainWindow_Closing", main_code_v115)
    if state_block and re.search(r"\bHide\s*\(", state_block.group(1)):
        errors.append("v1.15 regression: minimizing must not Hide the main taskbar window")


# v1.16+: viewing mode, single-click chapter navigation, reliable unfinished-state priority,
# cleaner titles/course display, and ended-source replay guard.
page_result = ROOT / "src/ChaoxingLearningAssistant/Chaoxing/PageRecognitionResult.cs"
course_model = ROOT / "src/ChaoxingLearningAssistant/Models/CourseItem.cs"
for path, tokens in {
    adapter_cs: ("CompletionKnown", "FocusFirstUnfinishedVideoTaskAsync", "cleanTitle", "courseTitle", "x.id !== currentId"),
    main_cs: ("ChapterList_PreviewMouseLeftButtonDown", "JumpToUnfinished_Click", "FindFirstUnfinishedNavigationCandidate", "IsStaleEndedPlayer", "ToggleCompactViewingMode", "UI-COMPACT-VIEW"),
    chapter_model: ("CompletionKnown", "DisplayTitle", "IsActive"),
    course_model: ("ProgressSummary", "进入课程后同步视频进度"),
    page_result: ("CourseTitle",),
}.items():
    if not path.exists():
        errors.append(f"v1.16 missing file: {path.relative_to(ROOT)}")
        continue
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in text:
            errors.append(f"v1.16 regression: {path.relative_to(ROOT)} missing {token}")

if main_xaml.exists():
    main_ui_v116 = main_xaml.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ('PreviewMouseLeftButtonDown="ChapterList_PreviewMouseLeftButtonDown"', 'Click="JumpToUnfinished_Click"', '全屏观看（F11）', 'ProgressSummary'):
        if token not in main_ui_v116:
            errors.append(f"v1.16 UI regression: MainWindow missing {token}")

if main_cs.exists():
    main_code_v116 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    compact = re.search(r"private void EnterCompactViewingMode\(\)([\s\S]*?)private void ExitCompactViewingMode", main_code_v116)
    if compact:
        for token in ("WindowState = WindowState.Maximized", "LeftNavigationColumn.Width = new GridLength(0)",
                      "BrowserHeaderRow.Height = new GridLength(0)", "BrowserFooterRow.Height = new GridLength(0)"):
            if token not in compact.group(1):
                errors.append(f"v1.34 regression: fullscreen viewing mode missing {token}")
    else:
        errors.append("v1.16 regression: EnterCompactViewingMode block missing")


# v1.17: Windows v1.16 evidence showed the runtime regression test was stale after the UI switched
# from a Run bound to ProgressPercent to a TextBlock bound to ProgressSummary. Keep the test aligned
# with the shipped template and keep the two warning fixes from regressing.
runtime_test = ROOT / "tests/ChaoxingLearningAssistant.Tests/RuntimeRegressionTests.cs"
if runtime_test.exists():
    runtime_text = runtime_test.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        'nameof(CourseItem.ProgressSummary)',
        'nameof(CourseItem.ProgressPercent)',
        'BindingOperations.GetBindingExpression(progressSummary, TextBlock.TextProperty)',
        'BindingOperations.GetBindingExpression(progressBar, ProgressBar.ValueProperty)',
    ):
        if token not in runtime_text:
            errors.append(f"v1.17 regression: runtime template test missing {token}")
    if '.SelectMany(t => t.Inlines.OfType<Run>())' in runtime_text and 'Path.Path == "ProgressPercent"' in runtime_text:
        errors.append("v1.17 regression: stale ProgressPercent Run lookup returned")
if main_cs.exists():
    main_code_v117 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    if '_ = Dispatcher.BeginInvoke(() => ChapterList.ScrollIntoView(current)' not in main_code_v117:
        errors.append("v1.17 regression: Dispatcher BeginInvoke warning suppression missing")
    if 'next = await adapter.FindNextChapterCandidateAsync(pending.ChapterId);' not in main_code_v117:
        errors.append("v1.17 regression: nullable adapter local guard missing")


# v1.18: UI chapter state must follow verified player identity, never optimistic ListBox selection.
player_model = ROOT / "src" / "ChaoxingLearningAssistant" / "Models" / "PlayerSnapshot.cs"
if player_model.exists():
    player_text = player_model.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("MediaId", "DocumentUrl", "ChapterId", "VideoTitle"):
        if token not in player_text:
            errors.append(f"v1.18 regression: PlayerSnapshot missing {token}")
if main_vm.exists():
    vm_text_v118 = main_vm.read_text(encoding="utf-8-sig", errors="ignore")
    if "CurrentChapterText" not in vm_text_v118:
        errors.append("v1.18 regression: MainViewModel missing CurrentChapterText")
if main_cs.exists():
    code_v118 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "PlayerIdentity",
        "ConfirmCurrentChapter",
        "ResolveChapterFromEvidence",
        "WaitForManualPlayerSwitchAsync",
        "WaitForPendingPlayerSwitchAsync",
        "CX-CHAPTER-SWITCH-NOOP",
        "CX-MANUAL-CHAPTER-CONFIRMED",
    ):
        if token not in code_v118:
            errors.append(f"v1.18 regression: MainWindow code missing {token}")
    if "current ??= FindFirstUnfinishedNavigationCandidate(chapters)" in code_v118:
        errors.append("v1.18 regression: chapter refresh must not jump to first unfinished item")
    fn = re.search(r"private(?: static)? ChapterItem\? FindFirstUnfinishedNavigationCandidate[\s\S]*?\n\s*private static bool IsCurrentChapterIdentity", code_v118)
    if not fn or "x.CompletionKnown && !x.IsCompleted" not in fn.group(0):
        errors.append("v1.18 regression: unfinished locator must require explicit CompletionKnown")
    fallback = re.search(r"private void ShowContinueNextFallback[\s\S]*?\n\s*private ChapterItem\? FindNextNavigationCandidate", code_v118)
    if fallback and "_vm.SelectedChapter = target" in fallback.group(0):
        errors.append("v1.18 regression: failed chapter switch must not advance SelectedChapter")
if main_xaml.exists():
    ui_v118 = main_xaml.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        'SelectedItem="{Binding SelectedChapter, Mode=OneWay}"',
        'PreviewMouseLeftButtonDown="ChapterList_PreviewMouseLeftButtonDown"',
        'Text="当前章节"',
        'Text="{Binding CurrentChapterText}"',
        'Content="打开未完成章节"',
    ):
        if token not in ui_v118:
            errors.append(f"v1.18 UI regression: MainWindow missing {token}")
    chapter_template = re.search(r'<ListBox x:Name="ChapterList"[\s\S]*?</ListBox>', ui_v118)
    if chapter_template:
        if 'TaskTypeText' in chapter_template.group(0) or 'StatusText' in chapter_template.group(0):
            errors.append("v1.18 UI regression: chapter list must not render task/status noise")


# v1.20: visible product copy should stay plain and study-oriented, not game/console themed.
visible_xaml = [
    ROOT / "src/ChaoxingLearningAssistant/Views/MainWindow.xaml",
    ROOT / "src/ChaoxingLearningAssistant/Views/SettingsWindow.xaml",
    ROOT / "src/ChaoxingLearningAssistant/Views/FirstRunWindow.xaml",
    ROOT / "src/ChaoxingLearningAssistant/Views/LogWindow.xaml",
    ROOT / "src/ChaoxingLearningAssistant/Views/StatsWindow.xaml",
]
banned_ui_terms = (
    "建筑群", "CX Learning Console", "LIBRARY", "TELEMETRY", "VIDEO STATE",
    "NEXT VIDEO", "DEVELOPER DIAGNOSTICS", "PREFERENCES", "GET STARTED",
    "LOCAL-FIRST", "工作台", "学习工作区", "科技主题",
)
for path in visible_xaml:
    if not path.exists():
        continue
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for term in banned_ui_terms:
        if term in text:
            errors.append(f"v1.20 copy regression: visible UI still contains {term}: {path.relative_to(ROOT)}")

if main_xaml.exists():
    ui_v119 = main_xaml.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        'Title="学习通课程视频播放助手 v1.39"',
        'Text="课程目录"',
        'Text="运行状态"',
        'Text="下一视频"',
        'Text="安全保护已开启"',
        'Text="详细日志"',
    ):
        if token not in ui_v119:
            errors.append(f"v1.20 copy regression: MainWindow missing {token}")


# v1.20: automatic next-video must support multiple videos inside one chapter and player IDs reused by the platform.
if player_model.exists():
    player_text_v120 = player_model.read_text(encoding="utf-8-sig", errors="ignore")
    if "DomIndex" not in player_text_v120:
        errors.append("v1.20 regression: PlayerSnapshot must expose DomIndex")
if adapter_cs.exists():
    adapter_text_v120 = adapter_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "ClickNativeNextVideoControlAsync",
        "InvokeNativeChapterActionAsync",
        "AdvanceToNextVideoTaskAsync",
        "source 比容器 ID 更能区分真实媒体",
        "currentIndex + 1",
    ):
        if token not in adapter_text_v120:
            errors.append(f"v1.20 regression: adapter missing {token}")
if main_cs.exists():
    main_text_v120 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "IsNaturalPlaybackEnd",
        "TryAdvanceUsingPlatformNextControlAsync",
        "WaitForRealAutoAdvanceAsync",
        "TryAdvanceWithinCurrentChapterAsync",
        "IsSameMediaEvidence",
        "CX-AUTO-NEXT-PLATFORM",
        "CX-AUTO-NEXT-IN-CHAPTER",
        "snapshot.ChapterId",
        "ExtractChapterId(snapshot.DocumentUrl)",
        "PlayerMediaEvidence.StableIdentity",
    ):
        if token not in main_text_v120:
            errors.append(f"v1.20 regression: MainWindow auto-next missing {token}")



# v1.21: a failed highlight/click is not an endpoint; escalate to real study-page navigation.
if adapter_cs.exists():
    adapter_text_v121 = adapter_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "BuildContextPreservingChapterUrlAsync",
        "scope.toOld.apply(scope, args)",
        "searchParams.set('chapterId', targetId)",
        "root.querySelectorAll?.('button,a,[role=\"button\"],[onclick]')",
    ):
        if token not in adapter_text_v121:
            errors.append(f"v1.21 regression: real chapter-navigation escalation missing {token}")
if main_cs.exists():
    main_text_v121 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "CX-AUTO-NEXT-DIRECT",
        "BuildContextPreservingChapterUrlAsync(pending)",
        "BuildContextPreservingChapterUrlAsync(next)",
        "完整 toOld 参数",
    ):
        if token not in main_text_v121:
            errors.append(f"v1.21 regression: auto-next must keep escalating until real navigation: {token}")


# v1.22: course -> chapter -> real video-task -> player binding must be explicit.
video_task_model = ROOT / "src" / "ChaoxingLearningAssistant" / "Models" / "VideoTaskItem.cs"
if not video_task_model.exists():
    errors.append("v1.22 regression: missing VideoTaskItem model")
else:
    video_task_text = video_task_model.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("TaskKey", "ChapterId", "ChapterTitle", "DocumentUrl", "Source", "CompletionKnown"):
        if token not in video_task_text:
            errors.append(f"v1.22 regression: VideoTaskItem missing {token}")

if chapter_model.exists():
    chapter_text_v122 = chapter_model.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("List<VideoTaskItem> VideoTasks", "VideoCount", "CompletedVideoCount", "HasVideoTasks"):
        if token not in chapter_text_v122:
            errors.append(f"v1.22 regression: ChapterItem missing task-level structure {token}")

if player_model.exists():
    player_text_v122 = player_model.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("IsVisible", "TaskKey", "ChapterTitleHint"):
        if token not in player_text_v122:
            errors.append(f"v1.22 regression: PlayerSnapshot missing {token}")

if adapter_cs.exists():
    adapter_text_v122 = adapter_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "ScanVideoTasksAsync",
        "CX-VIDEO-TASK-SCAN",
        "genericCourseTitle",
        "学生学习页面",
        ".ThenByDescending(x => !x.Ended)",
        "chapterTitleHint",
        "taskKey",
    ):
        if token not in adapter_text_v122:
            errors.append(f"v1.22 regression: adapter missing structural binding token {token}")
    if "const id = getId(el);\n    const id = getId(el);" in adapter_text_v122:
        errors.append("v1.22 regression: duplicate const id returned to FindNextChapterCandidateAsync")

if main_cs.exists():
    main_text_v122 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "BindVideoTasksToChapters",
        "ResolveVideoTaskFromPlayer",
        "VideoTaskBelongsToChapter",
        "PlayerMatchesTargetChapterAsync",
        "ResolveEffectiveCourseTitle",
        "SameCourseContext",
        "ExtractCourseId",
        "正在读取课程…",
        "正在读取章节…",
    ):
        if token not in main_text_v122:
            errors.append(f"v1.22 regression: MainWindow missing structural synchronization token {token}")
    # Final player confirmation must not accept a catalog active class by itself.
    match_fn = re.search(r"private async Task<bool> PlayerMatchesTargetChapterAsync[\s\S]*?\n\s*private ChapterItem\? ResolveChapterFromEvidence", main_text_v122)
    if not match_fn:
        errors.append("v1.22 regression: PlayerMatchesTargetChapterAsync block missing")
    else:
        body = match_fn.group(0)
        if "HasTargetChapterPageEvidenceAsync(target)" in body:
            errors.append("v1.22 regression: active catalog evidence alone must not confirm the real player")
        if "ScanVideoTasksAsync" not in body or "VideoTaskBelongsToChapter" not in body:
            errors.append("v1.22 regression: target confirmation must rescan real video-task evidence")
    current_branch = re.search(r"if \(targetWasCurrent\)[\s\S]*?\n\s*var openedByPage", main_text_v122)
    if current_branch and "allowCurrentPlayer: true" in current_branch.group(0):
        errors.append("v1.22 regression: clicking current chapter must not label an unchanged player as a successful switch")
    if "var unfinishedTask = _vm.VideoTasks" not in main_text_v122 or ".Where(x => x.CompletionKnown && !x.IsCompleted)" not in main_text_v122:
        errors.append("v1.22 regression: unfinished locator must prefer explicit task-level completion evidence")

runtime_model_test = ROOT / "tests" / "ChaoxingLearningAssistant.Tests" / "ModelTests.cs"
if runtime_model_test.exists():
    model_test_text = runtime_model_test.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("ChapterVideoTasks_AreTrackedSeparatelyFromChapterType", "VideoTaskDisplayTitle_FallsBackWithoutChangingChapterTitle"):
        if token not in model_test_text:
            errors.append(f"v1.22 regression: model test missing {token}")

adapter_regression = ROOT / "scripts" / "adapter_regression.mjs"
if adapter_regression.exists():
    regression_text_v122 = adapter_regression.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "v1.22 video-task scanner keeps chapter, title and explicit unfinished state separate from chapter rows",
        "v1.22 page recognition never promotes generic 学生学习页面 into a course name",
        "v1.22 player snapshot exposes visibility and task identity used by chapter synchronization",
    ):
        if token not in regression_text_v122:
            errors.append(f"v1.22 regression: adapter test missing {token}")



# v1.23: five P0 regressions from the v1.22 self-audit.
video_evidence = ROOT / "src" / "ChaoxingLearningAssistant" / "Chaoxing" / "VideoTaskEvidence.cs"
media_evidence = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "PlayerMediaEvidence.cs"
end_detector = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "PlaybackEndDetector.cs"
for path, tokens in {
    video_evidence: ("PlaceholderMatchesReal", "MergeParentEvidence", "CompletionKnown", "ChapterId"),
    media_evidence: ("StableIdentity", "StableTaskIdentity", "IsSameMedia", "Source", "MediaId", "DomIndex"),
    end_detector: ("pollingBudget", "lastPlayingRate", "resetOrRewound", "0.35"),
}.items():
    if not path.exists():
        errors.append(f"v1.23 regression: missing {path.relative_to(ROOT)}")
        continue
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in text:
            errors.append(f"v1.23 regression: {path.name} missing {token}")

if media_evidence.exists():
    evidence_text = media_evidence.read_text(encoding="utf-8-sig", errors="ignore")
    stable_match = re.search(r"public static string StableIdentity[\s\S]*?public static bool IsSameMedia", evidence_text)
    if not stable_match:
        errors.append("v1.23 regression: StableIdentity block missing")
    elif "Duration" in stable_match.group(0) or "VideoTitle" in stable_match.group(0):
        errors.append("v1.23 regression: StableIdentity must not depend on mutable duration/title metadata")

if adapter_cs.exists():
    a123 = adapter_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "playerFrames = Array.from(document.querySelectorAll('iframe[src]'))",
        "VideoTaskEvidence.MergeParentEvidence(real, parent)",
        "const target = blocks.find(x => completion(x) === false) || blocks.find(x => completion(x) === null);",
        "if (completion(block) === true) continue;",
        "const x = tail.find(x => x.completion === false) || tail.find(x => x.completion === null);",
    ):
        if token not in a123:
            errors.append(f"v1.23 regression: adapter missing P0 guard {token}")

if main_cs.exists():
    m123 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "PlayerMediaEvidence.IsSameMedia",
        "PlayerMediaEvidence.StableIdentity",
        "PlaybackEndDetector.IsNaturalEnd",
        "_lastObservedPlayingRate",
        "_pendingOriginPlayerSnapshot",
    ):
        if token not in m123:
            errors.append(f"v1.23 regression: MainWindow missing P0 guard {token}")
    if "title:{snapshot.VideoTitle}" in m123 or "duration:{snapshot.Duration" in m123:
        errors.append("v1.23 regression: mutable title/duration returned to PlayerIdentity")
    strict_next = re.search(r"private ChapterItem\? FindNextNavigationCandidate[\s\S]*?\n\s*private ChapterItem\? FindNextSequentialNavigationCandidate", m123)
    if not strict_next or "ChapterHasExplicitUnfinishedVideo" not in strict_next.group(0):
        errors.append("v1.23 regression: explicit unfinished locator must still require CompletionKnown")
    sequential_next = re.search(r"private ChapterItem\? FindNextSequentialNavigationCandidate[\s\S]*?\n\s*private ChapterItem\? FindFirstUnfinishedNavigationCandidate", m123)
    if not sequential_next or "CoursePlaybackPlan.BuildPendingChapters" not in sequential_next.group(0):
        errors.append("v1.27 regression: natural sequential advance must accept unknown status while skipping explicit completed chapters")
    for token in (
        "StartPlaybackWithRetryAsync(candidate, sourceLabel)",
        "StartPlaybackWithRetryAsync(snapshot, userInitiated ?",
        "CompleteManualChapterSwitchAsync(chapter, switched)",
        "_manualPlaybackAfterNavigationTarget = chapter",
        "for (var attempt = 0; attempt < 4; attempt++)",
        "CX-MANUAL-CHAPTER-AUTOPLAY",
    ):
        if token not in m123:
            errors.append(f"v1.27 regression: automatic/manual chapter playback flow missing {token}")
    for token in (
        "--autoplay-policy=no-user-gesture-required",
        "ChapterHasPendingVideo",
        "ChapterHasExplicitUnfinishedVideo",
        "第 {position + 1}/{videos.Length} 个视频",
        "value.Equals(\"播放视频\"",
        "VerifyCourseCompletionOrContinueAsync",
        "ConfirmChapterHasNoVideoAsync",
        "_courseVerifiedChapters",
        "_courseUnresolvedChapters",
    ):
        if token not in m123:
            errors.append(f"v1.30 regression: unattended playback/video ordinal flow missing {token}")
    ended_flow = re.search(r"private async Task HandleVideoEndedAsync[\s\S]*?\n\s*private bool IsNaturalPlaybackEnd", m123)
    if not ended_flow or ended_flow.group(0).find("TryAdvanceWithinCurrentChapterAsync") > ended_flow.group(0).find("TryAdvanceUsingPlatformNextControlAsync"):
        errors.append("v1.28 regression: same-chapter video lookup must run before any native next control")

if runtime_model_test.exists():
    mt123 = runtime_model_test.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "VideoTaskEvidence_MergesParentChapterAndCompletionIntoIframeVideo",
        "PlayerMediaEvidence_IgnoresLateTitleAndDurationButDetectsNewSource",
        "PlaybackEndDetector_CatchesHighRateResetWithoutTreatingNormalPauseAsEnded",
    ):
        if token not in mt123:
            errors.append(f"v1.23 regression: MSTest source missing {token}")

if adapter_regression.exists():
    js123 = adapter_regression.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "v1.23 parent page can click next control when the real video lives only in a child iframe",
        "unknown video task remains a pending playback candidate",
        "same-chapter sequential playback accepts an unknown-status next video without labeling it unfinished",
        "same-chapter block fallback skips a test task and opens the following video",
        "same-chapter cross-iframe target opens the next parent task block",
        "native player fallback refuses a generic next-chapter control",
        "video-task scanner excludes an explicit chapter quiz placeholder",
    ):
        if token not in js123:
            errors.append(f"v1.23 regression: adapter test missing {token}")


# v1.25: statistics, cache, settings recovery and diagnostic evidence.
watch_tracker = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "PlaybackWatchTracker.cs"
course_cache = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "CourseCacheService.cs"
json_file = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "JsonFile.cs"
for path, tokens in {
    watch_tracker: ("Elapsed", "_wasPlaying", "TimeSpan.FromSeconds(15)"),
    course_cache: ("CourseCacheSnapshot", "SavedAt", "GroupBy", "IsWebUrl"),
    settings_model: ("Normalize()", "Enum.IsDefined", "NormalizeWebUrl"),
    json_file: ('path + ".bak"', "IsValidJson"),
}.items():
    if not path.exists():
        errors.append(f"v1.25 regression: missing {path.relative_to(ROOT)}")
        continue
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.25 regression: {path.name} missing {token}")

if main_cs.exists():
    m124 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        'FinishActiveStat("切换视频"', "PlaybackWatchTracker", "CourseCacheService",
        "LoadCachedCourses", "_lastObservedPlayerSnapshot ?? new PlayerSnapshot",
        "PlayerMediaEvidence.IsSameMedia(previousPlayerSnapshot, snapshot)",
    ):
        if token not in m124:
            errors.append(f"v1.25 regression: MainWindow missing {token}")

runtime_regression_test = ROOT / "tests" / "ChaoxingLearningAssistant.Tests" / "RuntimeRegressionTests.cs"
if runtime_model_test.exists():
    mt124 = runtime_model_test.read_text(encoding="utf-8-sig", errors="ignore")
    for token in (
        "PlayerMediaEvidence_ToleratesRotatingCdnSignatureForSameTask",
        "AppSettings_NormalizeClampsValuesAndRejectsUnsafeResumeUrl",
    ):
        if token not in mt124:
            errors.append(f"v1.25 regression: test source missing {token}")
if runtime_regression_test.exists():
    rt124 = runtime_regression_test.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("PlaybackWatchTracker_ExcludesPausedAndSleepIntervals", "PlaybackWatchTracker_ResetsWhenMediaChanges"):
        if token not in rt124:
            errors.append(f"v1.25 regression: runtime test source missing {token}")


# v1.26: guard the two Windows failures found only after running the real compiler/tests.
if main_cs.exists():
    main_code_v126 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    if re.search(r"}\s*_lastObservedPlayerSnapshot\s*=\s*snapshot;\s*else\s+if", main_code_v126):
        errors.append("ERR-BUILD-006 regression: statement must not split an if/else-if chain")

player_media_evidence = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "PlayerMediaEvidence.cs"
if player_media_evidence.exists():
    player_evidence_text = player_media_evidence.read_text(encoding="utf-8-sig", errors="ignore")
    if "SameSourcePath(a.Source, b.Source)" not in player_evidence_text:
        errors.append("ERR-MEDIA-001 regression: reused mediaId must compare the source path")

if runtime_model_test.exists():
    model_test_v126 = runtime_model_test.read_text(encoding="utf-8-sig", errors="ignore")
    if "PlayerMediaEvidence_ToleratesQueryRotationWithoutConfusingDifferentPaths" not in model_test_v126:
        errors.append("v1.26 regression: missing source-path media identity test")

persistence_test = ROOT / "tests" / "ChaoxingLearningAssistant.Tests" / "PersistenceRegressionTests.cs"
if persistence_test.exists() and "[DataTestMethod]" in persistence_test.read_text(encoding="utf-8-sig", errors="ignore"):
    errors.append("WARN-TEST-001 regression: DataTestMethod is obsolete in current MSTest")


# v1.33 inherits the v1.31 mist-green UI, tactile controls, feedback and tutorial.
app_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "App.xaml"
main_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "Views" / "MainWindow.xaml"
first_run_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "Views" / "FirstRunWindow.xaml"
settings_xaml = ROOT / "src" / "ChaoxingLearningAssistant" / "Views" / "SettingsWindow.xaml"
ui_guards = {
    app_xaml: ('Color="#EAF7F8"', 'ScaleTransform ScaleX="0.95" ScaleY="0.95"', 'TranslateTransform Y="2"'),
    main_xaml: ('Click="Tutorial_Click"', 'x:Name="InAppNotice"', 'x:Name="NoticeMessageText"'),
    first_run_xaml: ('Grid.Column="6"', 'Text="04"', 'Click="Close_Click"'),
    settings_xaml: ('Checked="Option_Changed"', 'Unchecked="Option_Changed"', 'x:Name="SettingsFeedback"'),
}
for path, tokens in ui_guards.items():
    if not path.exists():
        errors.append(f"v1.33 regression: missing {path.relative_to(ROOT)}")
        continue
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.33 regression: {path.name} missing {token}")

if main_cs.exists():
    main_code_v131 = main_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ('ShowInAppNotice(', 'Tutorial_Click(', 'new FirstRunWindow', 'nextTask?.DocumentUrl'):
        if token not in main_code_v131:
            errors.append(f"v1.33 regression: MainWindow code missing {token}")

restore_master = ROOT / "scripts" / "restore_master.py"
if not restore_master.exists():
    errors.append("v1.33 regression: restore_master.py is missing")
else:
    restore_code = restore_master.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("SOURCE ZIP BASE64 BEGIN", "SOURCE ZIP BASE64 END"):
        if token not in restore_code:
            errors.append(f"v1.33 regression: restore script missing {token}")

for public_file in ("LICENSE", "CONTRIBUTING.md", "SECURITY.md", ".gitignore", "使用教学.md", "使用教学.docx", "使用教学.pdf"):
    if not (ROOT / public_file).exists():
        errors.append(f"v1.33 open-source regression: missing {public_file}")

github_workflow = ROOT / ".github" / "workflows" / "build.yml"
release_packager = ROOT / "scripts" / "package_github_release.ps1"
if not github_workflow.exists():
    errors.append("v1.33 open-source regression: GitHub Actions workflow is missing")
if not release_packager.exists():
    errors.append("v1.33 open-source regression: GitHub release packager is missing")

if adapter_cs.exists():
    adapter_v132 = adapter_cs.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("targetNextDocumentUrl", "targetBlockMediaId", "nextDocumentUrl"):
        if token not in adapter_v132:
            errors.append(f"v1.33 same-chapter iframe regression: adapter missing {token}")
    for token in ('"返回课程"', 'IsCourseNavigationLabel'):
        if token not in adapter_v132:
            errors.append(f"v1.33 course-list regression: adapter missing {token}")

# v1.34 fixes false course cards, adds live next-video previews and restores the approved visual structure.
next_preview = ROOT / "src/ChaoxingLearningAssistant/Services/NextVideoPreviewResolver.cs"
for path, tokens in {
    adapter_cs: ("directLabel", "返回课程", ".course-title"),
    main_cs: ("SyncCurrentCourseCard", "NextVideoPreviewResolver.Resolve", "等待视频载入"),
    next_preview: ("TaskType.Quiz", "FormatTask", "NoNextText"),
    main_xaml: ("DarkPanelStyle", "StatusRotor", "学习通课程视频播放助手", "按课程顺序识别"),
}.items():
    if not path.exists():
        errors.append(f"v1.34 regression: missing {path.relative_to(ROOT)}")
        continue
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.34 regression: {path.name} missing {token}")

# v1.35 keeps course discovery useful on asynchronously rendered pages and exposes a visible fullscreen exit.
for path, tokens in {
    adapter_cs: (".course-card", ".ktmc", ".title", "input[name=\"courseId\"]"),
    main_cs: ("ScanCoursesWithRetryAsync", "ExitFullscreen_Click", "WindowState = _compactViewingMode ? WindowState.Maximized"),
    main_xaml: ('x:Name="FullscreenExitBar"', 'Content="退出全屏  Esc"', 'Content="刷新课程"'),
}.items():
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.35 regression: {path.name} missing {token}")

# v1.36 recognizes lazy video iframes whose visible src is only index.html,
# keeps assessment frames excluded, and gives unfinished-navigation clicks visible feedback.
for path, tokens in {
    adapter_cs: ("frameEvidence", "insertvideo", "attr(iframe,'_src')", "worktype"),
    main_cs: ('JumpToUnfinishedButton.Content = "正在查找…"', "FindFirstPendingNavigationCandidate"),
    main_xaml: ('x:Name="JumpToUnfinishedButton"',),
    adapter_regression: ("lazy learning-page iframe metadata identifies a video and excludes a chapter quiz",),
}.items():
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.36 regression: {path.name} missing {token}")

# v1.37 re-checks real videos after entering a chapter. A chapter left incomplete by
# a quiz must be skipped by both manual unfinished navigation and automatic traversal.
model_tests = ROOT / "tests" / "ChaoxingLearningAssistant.Tests" / "ModelTests.cs"
playback_plan = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "CoursePlaybackPlan.cs"
for path, tokens in {
    playback_plan: ("AllKnownVideosCompleted",),
    main_cs: ("SkipChapterWithCompletedVideosAsync", "CX-COURSE-VIDEOS-COMPLETE", "await QueueAndOpenNextChapterAsync(chapter);"),
    model_tests: ("CoursePlaybackPlan_AllKnownVideosCompleted_SeparatesQuizIncompleteChapterFromVideoWork",),
}.items():
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.37 regression: {path.name} missing {token}")

# v1.38 opens a course on one click, switches to the chapter tab, and keeps probing
# course landing pages whose catalog arrives after the main navigation completes.
url_classifier = ROOT / "src" / "ChaoxingLearningAssistant" / "Chaoxing" / "ChaoxingUrlClassifier.cs"
for path, tokens in {
    url_classifier: ("MayContainChapterCatalogUri", "studentcourse", '"/visit/courses"'),
    main_xaml: ('PreviewMouseLeftButtonDown="CourseList_PreviewMouseLeftButtonDown"',),
    main_cs: ("CourseList_PreviewMouseLeftButtonDown", "LibraryTabs.SelectedIndex = 1", "MayContainChapterCatalogUri", "章节目录仍在加载"),
}.items():
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.38 regression: {path.name} missing {token}")

# v1.39 treats only explicitly unfinished unknown chapters as inspection targets,
# then relies on the existing post-navigation video scan to choose the real task.
next_video_resolver = ROOT / "src" / "ChaoxingLearningAssistant" / "Services" / "NextVideoPreviewResolver.cs"
for path, tokens in {
    playback_plan: ("chapter.TaskType == TaskType.Unknown", "chapter.CompletionKnown && !chapter.IsCompleted"),
    next_video_resolver: ("needsChapterInspection", "进入后定位未完成视频"),
    main_cs: ("chapter.TaskType == TaskType.Video || chapter.TaskType == TaskType.Unknown",),
    model_tests: ("CoursePlaybackPlan_InspectsExplicitlyUnfinishedUnknownChapter", "NextVideoPreview_PreviewsExplicitlyUnfinishedUnknownChapterForInspection"),
}.items():
    source = path.read_text(encoding="utf-8-sig", errors="ignore")
    for token in tokens:
        if token not in source:
            errors.append(f"v1.39 regression: {path.name} missing {token}")


if errors:
    print("STATIC CHECK: FAIL")
    for item in errors:
        print(" -", item)
    sys.exit(1)

print("STATIC CHECK: PASS")
