# CHANGELOG

## v1.36 - 2026-09-11

- 修复懒加载 iframe 的 `src` 仅为 `index.html` 时漏掉同章后续视频的问题。
- 从 iframe 的 class、data、_src、objectid、mid 与 module 中读取视频类型、标题和身份。
- 继续排除章节测验、作业、考试、签到及 `module: work` 任务，防止误跳题目。
- “打开未完成章节”增加查找中、目标章节和失败恢复提示，并防止连续重复点击。
- 未发现明确未完成状态时，只回退到具有真实视频证据的待核验章节。
- 版本、构建入口、用户包、开发者包和累计 MASTER 升级到 v1.36 / 1.36.0。

## v1.35 - 2026-09-11

- 修复课程页面异步加载或新版课程卡标题节点导致左侧课程列表空白的问题。
- 兼容只有课程 ID、班级 ID 和脚本按钮的课程卡，生成普通课程学习链接。
- 课程空状态新增说明与“刷新课程”入口，成功识别后立即缓存。
- 全屏观看新增始终可见的独立退出栏，Esc/F11 保持有效。
- 修复全屏期间从任务栏或托盘恢复时窗口状态不一致。
- 新增新版课程卡扫描回归，并保留“返回课程/提示”防误识别验证。

## v1.34 - 2026-09-11

- 修复课程列表把“返回课程”附近“提示”标题误识别为课程的问题，并清除旧缓存中的伪课程卡。
- 进入真实学习页后，以识别出的课程名和 courseId 自动补齐当前课程卡及进度。
- 播放期间持续显示下一视频；优先同章节后续视频，并跳过测验、作业、考试、签到、资料与已完成视频。
- 界面按确认参考图重做为浅色目录、深色播放区、深色运行状态区、青绿光轨与动态仪表。
- 修复深色按钮和安全说明文字对比度不足，删除右栏与顶部重复的快捷按钮。
- 点击“开始”只选择确有待播视频的章节；章节仅因测验未完成而视频已全部完成时不再误跳。
- 学习通网页普通模式默认 80% 缩放；F11 进入真正铺满窗口的网页观看模式，Esc 或 F11 恢复。
- 程序版本、构建入口、用户包、开发者包和累计 MASTER 升级到 v1.34 / 1.34.0。

## v1.33 - 2026-09-11

- 修复课程列表把“返回课程”当成课程名称并因标题更短而优先保留的问题。
- 网页扫描与 C# 合并层双重排除课程导航文字，并新增真实课程卡片回归测试。
- 界面采用浅绿外层、深绿播放框、单一翠绿色光效、轻量粒子、状态呼吸光与当前到下一视频流向动效。
- 学生界面将“开发者诊断”“诊断包”改为“详细日志”“反馈包”，保留完整日志和反馈导出能力。
- 删除公开 HTML 教学，新增 DOCX、PDF 与 GitHub 可直接阅读的 Markdown 教学。
- 版本、构建入口、安装器、用户包、开发者包与累计 MASTER 升级到 v1.33 / 1.33.0。

## v1.32 - 2026-09-11

- 修复同章节多视频在独立 iframe 中时，自然结束可能无法定位父页面下一任务块的问题；结束路径现在先扫描并明确指定同章下一视频，再保留相对顺序回退。
- 父任务块与子 iframe 视频合并时继承父页面任务序号，避免多个子视频都以局部 `DomIndex=0` 排错顺序。
- 新增跨 iframe 同章续播回归，Node 适配器测试增至 47 项。
- 新增下载包外层自包含图文教学和纯文字备用教学，程序文件统一放入次级文件夹。
- 新增 GitHub 开源发布结构、MIT 许可证、贡献规则、安全说明、隐私排除规则和 Windows 自动验证工作流。
- 生成 GitHub Release 可直接下载使用的自包含 win-x64 包，无需安装 .NET SDK。
- 公开源码排除日志、诊断包、WebView2 用户数据、账号资料、构建产物、旧版压缩包和 MASTER 大文件。
- 版本、程序标题、构建入口、安装器和归档统一升级到 v1.32 / 1.32.0。

## v1.31 - 2026-09-11

- 默认界面升级为浅绿“雾森”护眼主题，保留渐变、环境光、发光阴影和玻璃卡片效果。
- 全局按钮增加 95% 缩放、2 像素下沉、底色和描边变化，让按压反馈更明显。
- 主窗口增加右上角结果提示；开始、暂停、停止、辅助开关、宽屏切换和设置保存均给出明确反馈。
- 设置开关增加即时“已开启/已关闭（保存后生效）”提示。
- 首次教学扩展为四步，并增加主界面 `?` 入口，说明真实播放器确认、同章视频顺序、测验跳过和认证暂停。
- v1.25-v1.30 Release 归档补充需求背景、要求、实现方法与 Debug 原理；v1.31 MASTER 继续累计并自包含完整源码。
- Debug 修复 WPF 模板名称作用域问题、WPF/WinForms `CheckBox` 类型歧义和主题默认值变化后的旧测试断言。

## v1.30 - 2026-09-11

- 新增本次运行的整门课程待播清单：记录起始章节、已核验章节和暂时无法确认的章节。
- 当前章节全部可达视频结束后才标记该章已核验；明确完成章节继续直接跳过，状态未知章节继续进入核验候选。
- 已确认没有真实视频的章节可连续越过；打不开或证据不足的章节可以先继续后续章节，但最终会阻止过早显示课程完成。
- 最后一条可达视频结束后重新扫描完整章节目录；发现遗漏候选会返回补播，目录为空或仍有无法确认章节时保持等待状态。
- “开始”在没有明确未完成标记时可选择状态未知的首个待核验章节，不再只能人工进入。
- 新增 3 项课程待播清单 MSTest，总数增加到 32 项；Node 适配器回归保持 46 项。

## v1.29 - 2026-09-11

- MASTER 不再嵌套已经损坏的旧版 MASTER 字符串，改为从可读的累计档案与各版 Release 重建完整历史正文。
- MASTER 生成脚本和输出文件统一为 UTF-8 with BOM，兼容 Windows PowerShell 5.1 与旧版记事本。
- 新增 `PRODUCT_EXPECTATIONS.md`，正式记录“持续开启即可顺序完成整门课程全部真实视频”的最终验收目标。
- 明确当前尚未完成视频序号 UI、全课程待播清单、无视频章节跨越和真实账号长时间验收，避免把预期写成既成事实。
- WebView2 加入无需用户手势的自动播放策略，并继续用真实 `paused` 状态验证结果。
- 章节候选改用视频任务汇总，跳过所有视频均明确完成的章节；未知视频保留为待播放候选。
- 当前视频增加 `第 N/M 个视频` 显示，并过滤“播放视频”等无信息标题。
- 视频任务扫描排除明确章节测验/测试题占位；Node 回归增加到 46 项。

## v1.28 - 2026-09-11

- 自动续播优先检查当前章节内排在后面的真实视频，章节内视频未播完时不再提前切章。
- 同章节顺序续播允许状态未知的视频，同时跳过平台明确已完成的视频。
- 父页面任务块扫描支持跨 iframe 多视频，并跳过测试、测验、作业、签到和考试块。
- 播放器原生回退只接受明确“下一视频”的控件，拒绝“下一节”“下一任务”和通用 next 按钮。
- 新增 2 项定向回归，Node 测试增加到 45/45 PASS；MSTest 29/29 PASS。
- MASTER TXT 改用带 BOM 的 UTF-8，兼容会把无 BOM 文件误判为 ANSI 的 Windows 记事本。

## v1.27 - 2026-09-11

- 将“明确未完成定位”和“自然结束后的顺序续章”拆开；顺序续章允许选择状态未知、但不是明确已完成的下一章节。
- 网页原生目录候选优先明确未完成项，找不到时回退到状态未知的后续项。
- 新增统一的播放确认重试：新播放器就绪较慢时最多重试 4 轮，并重新核对真实播放状态。
- 手动左侧切章的目录点击、网页原生函数、上下文导航和 href 导航全部在确认切换后自动播放。
- 顶层导航期间保存手动播放目标和旧播放器证据，页面加载完成后继续确认与播放。
- Node 适配器回归增加到 43 项；Windows MSTest 29/29 PASS；静态检查 PASS。

## v1.26 - 2026-09-11

- 修复 v1.24/v1.25 继承的 `if / else if` 断裂语法错误；Windows Release 编译恢复通过。
- 修正媒体身份判断：同一 CDN 路径仅查询参数轮换时保持同一视频，实际路径变化时不再因复用 mediaId 而误判。
- 将过时的 MSTest `DataTestMethod` 改为 `TestMethod + DataRow`，清除警告。
- 新增媒体路径变化回归测试；Windows MSTest 29/29 PASS，Node 适配器测试 42/42 PASS。
- quick-run 发布成功，生成 v1.26 EXE。
- 补全 v1.24/v1.25 缺失的错误、测试和变更归档，并建立“真实 Windows 编译通过后才可正式归档”的发布门槛。

# v1.25 阶段交付（2026-09-10）

本轮基于用户提供的 v1.24 源码包进行有限范围修复，不宣称真实课程适配已经全部完成。

## 本轮修改
1. 课程列表刷新复用相同 URL 的课程对象，保留当前选择、已观察到的视频数量/完成数和最近章节，并去重。空扫描仍保留列表；非空扫描中已不存在的课程会移除。
2. 清除会话先删除 .bak，再删除主文件，避免下次启动从旧备份恢复已清除的会话。
3. JSON 主文件为 null 时尝试备份；保存时仅允许能按当前模型反序列化的非 null 主文件覆盖备份，防止 []/null 等内容毁掉有效备份。临时文件使用独立名称并在结束时清理。
4. 章节打开路径增加异常处理、记录日志、暂停辅助流程并提示重试，finally 释放点击锁。
5. 课程缓存跳过 null 条目，保留同文件中的有效课程。
6. 版本、启动器、安装器和构建标识统一到 v1.25 / 1.25.0。

## 验证事实
- Python 源码静态检查：PASS。
- Node 真实适配器脚本回归：42/42 PASS；本轮没有修改平台适配器。
- 新增 PersistenceRegressionTests.cs 共 8 个测试场景：清除备份、3 类坏主文件恢复与保存、正常备份、空缓存条目、刷新选择/进度/去重、移除旧课程。已编写，NOT RUN。
- 本环境无 dotnet、Windows、WebView2：C# 编译、MSTest、界面和真实课程验收均 NOT RUN。无新编译 EXE。
- MASTER 载荷逐文件 SHA256 与工作源码比对：PASS（打包时校验）。

## Windows 阶段验收
1. 完整解压 v1.25 文件夹，双击 BUILD_V1_25.bat。保留 BUILD_LOG.txt；成功后运行 START.bat。
2. 进入一门课程并观察章节进度，刷新课程列表，确认选择与进度不丢失。
3. 出现恢复提示时选择不恢复，退出重启，确认旧记录不再由备份复活。
4. 章节切换期间遇到页面失败时，确认程序保留窗口并允许稍后重试；正常课程点击、自然结束续播仍需真机验证。
5. 下一轮优先提供本版 BUILD_LOG.txt 和失败时诊断包，避免仅凭猜测继续堆平台模板。

## 仍未解决 / 范围边界
- 没有真实失败页面、登录账号或新诊断日志，本轮没有声称解决所有学校的章节识别或自动续播。
- 课程匹配沿用 URL（忽略大小写）；同一课程 URL 参数变化可能被视作新候选。
- JSON 备份可反序列化不等于业务内容完全正确；未增加多进程同时写入协调。
- 统计跨午夜拆分、账户级缓存隔离及平台特有播放器适配留待后续。
- 视频仍须真实播放；不伪造时长/完成、不自动答题、不绕过验证码或学校权限。


===== 以下为原有累计历史，旧结论仅代表旧版本 =====

# CHANGELOG

## v1.24 - 2026-09-10

- 新增只累计真实播放区间的 PlaybackWatchTracker；暂停、休眠和长阻塞不再制造虚高统计。
- 视频切换、页面切换、停止和退出时独立结算上一条统计，避免跨视频串片。
- CDN 临时签名变化可由 TaskKey 或 MediaId/document/dom 强证据确认仍为同一任务。
- ended 防重使用稳定任务身份与完整 PlayerSnapshot。
- 诊断包写入最近完整播放器快照。
- 接入 30 天课程缓存；空扫描不再清空最近有效列表。
- 设置加载/保存归一化，JSON 文件增加有效 `.bak` 恢复。
- 修复累计统计超过 24 小时后小时显示回卷。
- 增加 4 个 MSTest 源码用例及 v1.24 静态回归守卫。
- Linux 静态检查 PASS，Node 42/42 PASS；Windows Build/MSTest/WebView2 实机验收 NOT RUN。

## v1.1 - 2026-09-07

- 完成 WPF + WebView2 主工程。
- 增加三栏式主界面和顶部控制栏。
- 增加持久化 WebView2 Profile 与清除登录状态。
- 增加课程/章节 DOM 扫描适配层。
- 增加 iframe JavaScript 扫描。
- 增加播放器状态读取。
- 增加视频暂停、明确点击后的播放。
- 增加倍速 UI 继承与失败降级。
- 增加视频结束后的下一未完成视频定位与预加载。
- 预加载后强制等待“继续下一节”确认。
- 增加网络超时、3 次重试与安全暂停。
- 增加状态机。
- 增加 AppData / Portable 数据目录。
- 增加学习统计。
- 增加崩溃恢复。
- 增加系统托盘、通知与提示音。
- 增加浅色/深色/系统主题。
- 增加首次使用引导。
- 增加日志窗口和诊断 ZIP。
- 增加 MSTest 基础模型测试。
- 增加 PowerShell 构建/发布脚本。
- 增加 Inno Setup 安装脚本。
- 增加 BUILD、TEST_REPORT、KNOWN_LIMITATIONS。

## v1.2 - Windows build bootstrap fix
- Fixed Windows PowerShell 5.1 parsing failure caused by non-BOM UTF-8 Chinese text in `scripts/build.ps1`.
- Build script messages are now ASCII-safe and the PowerShell file is saved with UTF-8 BOM.
- SDK detection now checks `dotnet --list-sdks`, so an installed runtime/host is no longer mistaken for an SDK.
- Added `STEP1_CHECK_AND_BUILD.bat`, which always pauses and writes `build_diagnostic.txt`.

## v1.3 — Windows 首次编译错误修正版

### 修复
- 归档并修复 `ERR-BUILD-001`：WPF `System.Windows.Application` 与 WinForms `System.Windows.Forms.Application` 的 CS0104 类型歧义。
- 主 `App` 基类改为显式 `System.Windows.Application`。
- WPF `MessageBox`、`Application.Current`、`System.Windows.Media.Color` 均改为显式限定，提前消除同类潜在歧义。
- 静态检查器增加 WPF + WinForms 命名冲突回归规则。

### 验证
- 用户 Windows 环境已确认 `.NET 8 SDK 8.0.424`。
- `dotnet restore` 已真实成功。
- v1.2 Build 真实结果：0 warning / 1 error（ERR-BUILD-001）。
- v1.3 源码静态检查：PASS。
- v1.3 Windows Build：等待用户重新执行一键构建。

### 文档
- 新增累计式 `ERROR_ARCHIVE.md`。
- TEST_REPORT 与 MASTER 更新为 v1.3。



## v1.4 - 2026-09-07

### Added
- ASCII-only `BUILD_V14.bat`.
- Fixed build-log filename: `BUILD_LOG.txt`.
- Automatic Notepad opening on build failure.
- ASCII Master alias: `MASTER_v1.4.txt`.
- Cumulative error entry `ERR-DELIVERY-001`.

### Changed
- Replaced `STEP1_CHECK_AND_BUILD.bat` with an ASCII/no-BOM launcher to avoid CMD BOM/encoding problems.
- Build handoff no longer depends on Chinese filenames.

### Validation status
- Source-code fix from v1.3 remains unchanged and awaits Windows rebuild.
- The actual v1.3 second-build compiler error is still unknown because the detailed log was not recoverable.


## v1.5 - 2026-09-07

### Windows evidence received
- v1.4 Restore: PASS.
- v1.4 Release Build: FAIL with 49 errors / 2 warnings.
- 48 errors were CS0103 (`File` / `Path` / `Directory`).
- 1 error was CS0104 (`ColorConverter`).
- 1 CS8619 warning was a cascading effect of unresolved `Path` typing.
- 1 WFAC010 warning was a WPF/WinForms DPI analyzer warning.

### Fixed
- Added explicit global aliases for `System.IO.File`, `System.IO.Path`, and `System.IO.Directory`.
- Fully qualified WPF `System.Windows.Media.ColorConverter`.
- Added a regression check for both fixes.
- Preserved WPF PerMonitorV2 manifest behavior and suppressed only WFAC010 with documented rationale.
- Build script now forces English .NET CLI diagnostics and performs a Release clean before build.
- Project/installer version advanced to 1.5.0.

### Archive
- `ERR-BUILD-001` moved to RESOLVED based on v1.4 real Windows evidence.
- `ERR-DELIVERY-001` moved to RESOLVED because `BUILD_LOG.txt` was successfully returned.
- Added `ERR-BUILD-002`, `ERR-BUILD-003`, and `WARN-BUILD-001`.
- Added raw historical build logs under `docs/build_logs/`.


## v1.6 - 2026-09-07

### Fixed
- `ERR-SCRIPT-002`: removed the unsupported `--no-restore` option from the v1.5 Clean stage by removing the unnecessary Clean stage entirely.
- Build pipeline is again `Restore -> Build -> Test -> Publish`.
- Added a static regression guard for `dotnet clean --no-restore`.

### Added
- `BUILD_V16.bat`.
- Archived the real v1.5 failed Windows log as `docs/build_logs/BUILD_LOG_v1.5_failed.txt`.
- Cumulative Master v1.6.

### Status
- v1.5 never reached C# compilation because the Clean command failed first.
- Therefore ERR-BUILD-002 / ERR-BUILD-003 remain awaiting real Windows validation.


## v1.7 - 2026-09-07

### Verified on real Windows build
- Release Build: PASS, 0 warnings, 0 errors.
- MSTest: PASS, 4/4 tests.
- Previous System.IO and ColorConverter compile defects are now closed.
- Previous Clean-script defect is now closed.

### Added
- `artifacts\quick-run` framework-dependent publish generated before self-contained packaging.
- `QUICK_RUN_READY.txt` marker.
- Explicit `win-x64` runtime-pack restore stage.
- Clear warning not to cancel the first self-contained runtime-pack download.
- Automatic opening of quick-run folder if later packaging fails.

### Event archive
- Added `EVT-PUBLISH-001`: self-contained publish interrupted with Ctrl+C in v1.6.


## v1.9 - 2026-09-07 — UI compile regression fix

### Windows evidence
- v1.8 Restore: PASS.
- v1.8 Build: FAIL with 0 warnings / 1 error.
- Error isolated to `ThemeService.cs(79,34)`.

### Fixed
- Added explicit `System.Windows.Media.Color` qualification in ThemeService.
- Added static regression guard against unqualified `(Color)` casts in mixed WPF/WinForms project.
- Preserved all v1.8 High-Tech UI redesign work unchanged.

### Archive
- Added `ERR-BUILD-004`.
- Archived raw v1.8 Windows build log.
- Project and installer version advanced to 1.9.0.


## v2.0 - 2026-09-07 — Browser focus & viewport usability

### Runtime feedback incorporated
- Added UI-REV-007: easier horizontal page movement.
- Added UI-REV-008: browser focus fullscreen.
- Added UI-REV-009: playback-time clipping fix.

### Added
- Browser toolbar horizontal-left / horizontal-right buttons.
- Shift + mouse-wheel horizontal movement.
- F11 browser focus fullscreen.
- Esc exits browser focus fullscreen.
- Fullscreen mode preserves and restores previous WPF column widths/window state.

### Changed
- Playback time now occupies a full-width metric card.
- Playback rate is displayed as a compact badge.
- Long time strings use Consolas + DownOnly scaling instead of clipping.
- Version advanced to 2.0.0.


## v1.10 - 2026-09-07 — Version-number correction + browser usability upgrade

### Version management correction
- The immediately preceding development package was temporarily labeled `v2.0`.
- That label was premature because the project has not crossed a major-version boundary.
- The exact same functional changes are now formally incorporated into `v1.10`.
- `v2.0` is not treated as an official released baseline.

### Functional content retained
- Browser horizontal movement controls.
- Shift + mouse-wheel horizontal movement.
- Browser focus fullscreen.
- F11 / Esc fullscreen interaction.
- Playback-time clipping fix.

### Official current version
- Application version: 1.10.0
- Master baseline: MASTER v1.10


## v1.11 - 2026-09-07 — Input-event compile regression fix

### Windows evidence
- v1.10 Restore: PASS.
- v1.10 Build: FAIL with 0 warnings / 1 error.
- Error isolated to `MainWindow.xaml.cs(1014,59)`.

### Fixed
- Explicitly qualified `System.Windows.Input.KeyEventArgs`.
- Added static regression checks for unqualified `KeyEventArgs` / `MouseEventArgs` in the mixed WPF/WinForms project.
- Preserved all v1.10 fullscreen, horizontal-scroll and playback-time UI changes.

### Archive
- Added `ERR-BUILD-005`.
- Archived raw v1.10 Windows build log.
- Project and installer version advanced to 1.11.0.


## v1.12 - 2026-09-07 — Runtime course-list binding fix

### Runtime evidence
- WebView2: PASS.
- Browser focus fullscreen entry: observed.
- Real Chaoxing course navigation: observed.
- Course candidate scan: 1 candidate observed.
- Runtime crash: repeated XamlParseException from CourseItem.ProgressPercent binding.

### Fixed
- Course-item ProgressBar is explicitly OneWay.
- Telemetry ProgressBar is explicitly OneWay.
- Added regression check for ProgressPercent -> ProgressBar binding mode.
- Corrected stale BUILD_LOG version header from v1.8 to v1.12.

### Archived
- Full supplied runtime log.
- Added ERR-RUNTIME-001.
- Added VER-MGMT-002.
- Added OBS-ADAPTER-001 for chapter/video recognition retest.


## v1.13 — 2026-09-08 — 点击视频报错与重复提示修复

- ERR-RUNTIME-002：补齐课程卡片 Run.Text 的 OneWay 绑定，保留 ProgressBar 修复；增加实际 WPF 模板布局回归测试和 XML 属性级防退化检查。
- ERR-RUNTIME-003：普通通知改为底部提示，相同提示限频；全局致命 UI 异常仅通知一次后退出，保留恢复快照。
- ERR-RUNTIME-004：轮询防重入，导航 ID/页面代次隔离过期回调和读取；取消导航不重试，页面切换/关闭取消重试和计时。
- ERR-PLAYER-001：结束处理去重，仅信任真实 ended；排除正在播放的当前页面作为下一视频；无法识别后续任务时不宣称课程完成。
- ERR-PLAYER-002：播放脚本返回同步数值，C# 核对实际播放状态，避免将 Promise JSON 当整数解析；已找到视频而播放未确认时不误跳课程。
- ERR-ADAPTER-001：未完成、未看完、not completed、unfinished、完成度 0% 均不作为已完成。
- BUILD-UX-001：新增 START/BUILD_V1_13/BUILD_FULL；默认只生成本机运行版，完整发布可选；旧入口指向当前版本，构建不再整体删除 artifacts。
- UI：课程进度通知更新、窗口标题和日志标明 v1.13；正文未重做视觉设计。
- 验证：14 项嵌入 JS 回归通过，静态检查及绑定故障注入通过；Windows 9 项测试已加入构建，但本轮未执行 Windows 编译、运行或安装测试。

## v1.14 — 2026-09-08 — 章节目录与默认连续播放

### Fixed / Changed
- 修复左侧章节列表在新目录结构中扫描为 0：新增 `.chapter_item[onclick*=toOld]`、`[id^=cur][onclick*=toOld]`、`.ncells h4 > a`、studentstudy 等识别路径。
- 章节模型增加 ChapterId，当前章节定位和自动切章优先使用稳定的章节 ID。
- 新增页面原生 `OpenChapterAsync`，自动切换优先点击真实目录节点；只有网页本身提供的可信直链才允许 URL 回退。
- 修复原生目录点击会立即触发导航时被误判为“点击失败”的竞态：即使旧文档 JS 返回值因导航失效，只要文档版本已变化就视为网页已接受点击，避免错误进入 URL 回退。
- 由 `toOld(...)` 推导出的 `studentstudy` 地址标记为 synthetic，仅用于目录识别，不再由自动流程或章节双击强制导航。
- 新增登录页 URL 识别：平台确实进入登录页时停止播放器轮询并保留最近学习页；登录后若平台回到通用首页则自动回到最近学习页，其他学校认证/绑定中间页保持原页，避免被强行跳过。
- 视频自然结束后默认自动切换下一视频、继续播放，并继承最近播放倍速。
- 未知章节在没有播放器时自动跳过继续查找后续候选；明确资料/测验/作业等不进入视频候选。
- 浏览器阻止 autoplay 时降级为“继续下一节”，不再把下一页强制暂停。
- 左侧页签改为“章节 / 视频”，识别到章节后自动切换显示。
- 设置新增 AutoPlayNextVideo / PreservePlaybackRate，默认均为 true。
- 版本与构建入口升级为 v1.14 / 1.14.0 / BUILD_V1_14.bat。

### Validation
- Node 中直接运行 ChaoxingAdapter 内嵌 JavaScript 回归，当前 16/16 PASS。
- XAML/XML、事件处理器、版本一致性和 v1.14 行为 token 由 static_check.py 检查。
- 当前 Linux 环境无 .NET SDK；Windows 编译、MSTest 与真实学习通续播标记为 NOT RUN。



## v1.15 — 2026-09-08 — 状态语义、任务栏、目录与下一节兜底

### Fixed / Changed
- 播放器真实状态与辅助流程状态拆分：右侧主状态显示视频实际播放/暂停/结束，辅助启用状态独立显示，避免“视频在播却显示暂停辅助”造成误解。
- 最小化不再调用 `Hide()`；主窗口保持 `ShowInTaskbar=true`，减少 EXE 从任务栏消失、退到后台后难找的问题。
- 章节扫描增加 `.posCatalog_select`、`.menulist-menu-title`、`.catalog_title`、`.catalog_item`、广义 `[id^=cur]` 等结构，并从自身/子节点/onclick/URL 提取章节 ID。
- 章节首次扫描为 0 时短延时重试；播放器运行时左侧仍为空则限频补扫，兼容目录异步渲染。
- 新增 `FindNextChapterCandidateAsync`：视频结束后左侧候选为空时，按当前 ChapterId 在网页原生目录顺序中找下一章节。
- Unknown/Document/Other 无播放器时可继续用列表或网页原生顺序寻找后续候选；明确的测验/作业/签到/考试不进入该自动处理路径。
- “未识别到后续视频”改为更准确的“暂未定位到下一章节”，只有左侧与网页原生目录兜底均失败后才显示。
- 深色主题 Secondary/Tertiary 文本提高亮度；设置窗口增加明确的动态前景/背景，补充 TextBlock/Label/RadioButton 默认前景。
- 版本、安装器、START、构建入口升级到 v1.15 / 1.15.0 / BUILD_V1_15.bat。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：20/20 PASS。
- Windows Release Build / MSTest / WebView2 真实课程：NOT RUN（当前环境无 .NET/Windows）。

## v1.16 — 2026-09-08 — 非全屏观看、未完成定位与防重播

### Fixed / Changed
- F11 从真正网页全屏改为非全屏宽屏观看：保留 Windows 标题栏、任务栏和左侧章节列表，仅隐藏右侧次要遥测并压缩边距。
- 左侧章节由双击打开改为单击打开；新增“定位未完成”按钮。
- `ChapterItem` 增加 `CompletionKnown` 与 `DisplayTitle`；状态未知不再直接伪装成“未完成”。
- 自动候选优先 `CompletionKnown && !IsCompleted`，明确已完成始终跳过，未知状态仅兜底。
- 章节标题增加状态/任务类型噪声清洗，避免把“待完成任务点/已完成/视频”等附加文字当成章节名。
- 课程扫描改进标题提取，过滤“进入课程/继续学习”等通用操作文案；学习页识别增加 `CourseTitle`。
- 课程尚未同步视频总数时显示“进入课程后同步视频进度”，不再用 0/0 暗示真实进度。
- 新增 `FocusFirstUnfinishedVideoTaskAsync`，打开章节后保守尝试定位平台标记的未完成视频任务点。
- `FindNextChapterCandidateAsync` 明确排除当前 ChapterId；主流程再做一次同章节防重播检查。
- ended 后记录刚结束媒体源；切换后若仍命中同一 `currentSrc/src`，触发 `CX-AUTO-NEXT-REPLAY-GUARD`，禁止重新播放当前视频。
- `Start` 不再无条件重播当前 ended/已完成播放器，先尝试定位未完成候选。
- 版本、安装器、START、构建入口升级至 v1.16 / 1.16.0 / BUILD_V1_16.bat；旧构建入口转发到当前版本。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：25/25 PASS。
- Windows Release Build / MSTest / WebView2 真实课程：NOT RUN（当前环境没有 .NET/Windows）。


## v1.17 — 2026-09-08 — Windows 回归测试修复

### Evidence
- v1.16 Windows：.NET SDK 8.0.424，Restore PASS，Release Build PASS（0 Error / 2 Warning）。
- MSTest：10 PASS / 1 FAIL；唯一失败 `RealCourseTemplate_RendersReadOnlyProgress_AndUpdatesCounts`。
- 根因：测试仍按旧模板寻找绑定 `ProgressPercent` 的 `Run`，发布 XAML 已改成 `TextBlock.Text -> ProgressSummary` + `ProgressBar.Value -> ProgressPercent`。

### Fixed / Changed
- 更新 RuntimeRegressionTests，直接验证当前真实发布模板的 ProgressSummary 和 ProgressPercent 两个绑定及计数变化。
- CS4014：显式丢弃 `Dispatcher.BeginInvoke` 返回操作。
- CS8602：异步下一章节 fallback 使用局部 `adapter` 非空引用。
- 版本、安装器、START、构建入口升级至 v1.17 / 1.17.0 / BUILD_V1_17.bat；旧构建入口转发到当前版本。
- 未改变 v1.16 的章节/观看/自动下一节业务逻辑。

### Validation
- Linux static_check / adapter_regression 见 `docs/validation/v1.17/`。
- v1.17 Windows Build/MSTest：AWAITING WINDOWS REBUILD。


## v1.18 — 2026-09-08 — 章节/播放器真实状态同步

### Runtime feedback
- 左侧显示“定位未完成”但点击章节无效。
- 左侧章节仍混入任务点/状态等非章节内容。
- 右侧章节与实际播放器不一致。
- 左侧章节会自行跳动，而内嵌网页视频实际上没有变化。

### Root cause
- 目录刷新在无法由 URL 对上当前章节时，会回退选中第一条未完成候选，导致 UI 自行跳动。
- 手动章节点击在真正网页切换之前就写入 `SelectedChapter` / 右侧视频文字；网页 click 失败时 UI 已经提前提交。
- 章节扫描把目录容器与任务点证据混用，导致标题和辅助状态进入左侧。
- 旧切换成功条件主要等价于“DOM click 已提交”，没有验证播放器媒体身份是否真的改变。

### Fixed / Changed
- 引入播放器真实身份：`MediaId / Source / DocumentUrl / ChapterId / VideoTitle`；切换确认以媒体身份变化为核心证据。
- `SelectedChapter` 改为 OneWay 展示，章节鼠标按下先拦截 ListBox 默认选择；验证播放器改变后才调用 `ConfirmCurrentChapter`。
- 目录刷新删除“无法匹配就选第一未完成”的回退，未确认时显示“章节待确认”。
- 右侧拆成“当前章节（已确认）”与“播放器视频”，不再用左侧选中项冒充实际播放器状态。
- 章节扫描优先真实目录节点，明确排除任务点/附件块；左侧模板删除 TaskTypeText / StatusText，只保留清洗后的 DisplayTitle。
- “定位未完成”改为“打开未完成”，查找函数收紧到 `CompletionKnown && !IsCompleted`。
- 手动与自动章节切换均增加等待播放器身份改变的确认流程；旧媒体未变化时记录 `CX-CHAPTER-SWITCH-NOOP` / `CX-MANUAL-CHAPTER-TIMEOUT`，不推进左/右 UI。
- 自动下一节继续保留 ended 媒体身份防重播，并与新的身份确认门槛合并。
- 版本、安装器、START、构建入口升级至 v1.18 / 1.18.0 / BUILD_V1_18.bat；旧构建入口转发到当前版本。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node scripts/adapter_regression.mjs`：27/27 PASS；新增媒体身份、active 目录与任务点排除回归。
- v1.18 Windows Release Build / MSTest / WebView2 真实课程：NOT RUN（当前 Linux 环境无 .NET/Windows/WebView2）。

## v1.19 — 2026-09-08 — 界面语言自然化

### Feedback
- 使用反馈认为部分界面语言过于游戏化/控制台化，“建筑群”被作为不希望出现的典型例子。
- 希望整体更接近普通学习软件，而不是游戏地图、控制台或科技面板。

### Changed
- 主窗口移除 `CX Learning Console / LIBRARY / TELEMETRY / VIDEO STATE / NEXT VIDEO / DEVELOPER DIAGNOSTICS / LOCAL-FIRST` 等可见标签。
- 统一替换为“课程目录 / 播放信息 / 视频状态 / 下一视频 / 开发者诊断 / 数据仅保存在本机”等直接中文。
- 首次使用中的“学习工作区 / 工作台”改成“首次设置 / 主界面”。
- 设置页“暗色科技主题”改成“深色主题”。
- “当前章节（已确认）/ 播放器视频”改为“当前章节 / 当前视频”；确认状态继续由真实值表达。
- “打开未完成”改为“打开未完成章节”，tooltip 明确只处理平台明确未完成状态。
- 设置、日志、统计、首次使用窗口移除 `CX Learning Console` 标题。
- 普通章节识别提示不再用“任务点”描述左侧污染项；开发者面板“任务”改成“页面类型”。
- 新增 v1.19 可见文案防退化规则，禁止“建筑群”及本轮移除的游戏化/控制台式词语重新进入 UI。
- 版本、安装器、START、构建入口升级至 v1.19 / 1.19.0 / BUILD_V1_19.bat；旧构建入口转发到当前版本。

### Unchanged
- v1.18 章节/播放器真实身份同步逻辑。
- 明确未完成判定和防重播逻辑。
- 真实播放与人工验证安全边界。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node scripts/adapter_regression.mjs`：27/27 PASS。
- v1.19 Windows Release Build / MSTest / WebView2 真实界面：NOT RUN（当前 Linux 环境无 .NET/Windows/WebView2）。


## v1.20 — 2026-09-08 — 视频结束后自动续播修复

### Runtime feedback
- 真实使用中，当前视频自然播放结束后仍不会自动跳到下一视频。

### Root cause / risk points
- v1.18/v1.19 的播放器身份优先只使用 `MediaId`；部分学习通播放器会复用同一个容器/data-objectid 装载不同视频，导致新视频 source 已变化但程序仍判定为旧视频。
- ended 流程此前首先按“章节目录”找下一项，无法覆盖“同一个章节页面中连续存在多个视频任务点”的课程结构。
- 当左侧当前章节因为严格同步规则尚未确认时，ended 流程可能缺少 previousIndex/chapterId，后续候选容易从目录头部重新计算。
- 当前页面同时保留旧 ended video 和新 paused video 时，播放器选择器可能继续选中旧 ended 元素。

### Fixed / Changed
- `PlayerSnapshot` 新增 `DomIndex`；`PlayerIdentity` 改成 MediaId + currentSrc + documentUrl + chapterId + DOM index + title/duration 的组合证据。
- `HandleVideoEndedAsync` 优先调用 `AdvanceToNextVideoTaskAsync`，先处理同章节内当前 ended video 后面的下一真实 video/task，再回退到下一章节。
- 新的同章节推进只对真实 `video`/视频任务容器滚动、点击或调用 `play()`，不修改完成状态；调用后必须由新的 `PlayerSnapshot` 再确认。
- 同一个 `MediaId` 但不同 `currentSrc` 允许被识别为新视频；同 source 仍严格拒绝，防止重播当前 ended 视频。
- ended 当前章节证据优先使用 `snapshot.ChapterId` / `snapshot.DocumentUrl`，再回退顶层 URL。
- 播放器快照评分新增“未结束优先”，新打开但暂停的下一视频不会被旧 ended 视频压过。
- 版本、安装器、START、构建入口升级至 v1.20 / 1.20.0 / `BUILD_V1_20.bat`；旧构建入口转发到当前版本。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：30/30 PASS。
- 新增回归：同一章节下一视频脚本解析、复用 mediaId 时按 source 推进且不重播旧视频、新 paused 视频优先于旧 ended 视频。
- v1.20 Windows Release Build / MSTest / WebView2 真实自动续播：NOT RUN（当前 Linux 环境无 .NET/Windows/WebView2）。

### v1.20 clarification — 学习通网页本身必须真实切换
- 用户进一步明确：“没有自动跳”指学习通网页播放器本身没有切到下一视频，不只是 App 左右栏没有同步。
- ended 检测新增“刚刚仍在播放 + 已到视频尾部”的自然结束补判，避免平台瞬间把 `ended=true` 重置为 paused 后被 1 秒轮询漏掉。
- 新增 `ClickNativeNextVideoControlAsync()`：优先点击真实播放器附近的“下一视频 / 下一节”控件；提交后仍必须验证 `currentSrc / mediaId / documentUrl / DOM video` 真正变化。
- 同章节任务点推进和跨章节推进统一使用“学习通真实播放器变化”作为成功标准。
- 普通章节 DOM click 只改高亮但播放器不变时，新增 `InvokeNativeChapterActionAsync()`，直接调用页面自身 `toOld(courseId, chapterId, clazzid)` 再核验真实播放器。
- 自动切换动作优先级：平台播放器原生下一控件 → 同章节下一视频任务点 → 下一章节目录 → 原生 `toOld(...)` 重试。
- Node 内嵌 JavaScript 回归更新为 33/33 PASS。

## v1.21 — 2026-09-08 — 自动续播失败后继续真实导航

### Runtime feedback
- v1.20 已能识别“目录变了但播放器没变”，但真实使用仍出现：程序停在“播放器身份始终没有变化”的提示，而学习通网页本身没有进入下一视频。
- 目标进一步明确：失败提示不能代替跳转；某条路径失败后应继续尝试真实导航，直到学习通实际视频变化或所有可确认路径都耗尽。

### Root cause / fixes
- `InvokeNativeChapterActionAsync()` 旧实现只提取并调用 `toOld(courseId, chapterId, clazzid)`；学习通常见目录 onclick 实际还带第 4 参数。v1.21 改为解析并原样传递页面 `toOld(...)` 的全部简单字面量参数。
- 新增 `BuildContextPreservingChapterUrlAsync()`：从当前已登录学习页生成目标章节 URL，原样保留 `courseId / clazzid / cpi / enc / openc / mooc2` 等现有参数，只替换章节 ID；不再依赖缺参数的旧 synthetic URL。
- `QueueAndOpenNextChapterAsync()` / `PreparePendingVideoAfterNavigationAsync()` / 手动章节打开都接入上述真实导航升级路径。
- `ClickNativeNextVideoControlAsync()` 由播放器祖先局部搜索扩展为当前文档全局 next 控件搜索。
- `WaitForRealAutoAdvanceAsync()` 等待真实播放器变化窗口延长到约 10 秒。
- 一个候选的所有真实导航路径仍失败时，自动续播继续尝试后续目录候选，不把第一条错误提示当作流程终点。

### Validation
- `python scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：36/36 PASS。
- 新增回归：toOld 第 4 参数保留；studentstudy 当前鉴权参数保留并只替换 chapterId；全局 next 控件可点击。
- v1.21 Windows Release Build / MSTest / WebView2 真实自动续播：NOT RUN（当前 Linux 环境无 .NET/Windows/WebView2）。


## v1.22 — 2026-09-09 — 课程 / 章节 / 视频任务 / 播放器结构绑定修复

### Runtime feedback
- 进入真实视频后右侧仍可能显示“学生学习页面 / 章节待确认 / 播放器已检测”，不能自动对应实际课程、章节、视频。
- 左侧章节点击与学习通真实播放器存在错位风险；用户目标明确为“进入后自动读取章节，点章节后真实切换视频”。

### Root cause
- 旧模型把 ChapterItem 同时当作章节和视频任务，一个章节多个视频时结构天然丢失。
- 课程名识别会使用 document.title / 上次课程名兜底，可能把“学生学习页面”或旧课程名显示为当前课程。
- 章节确认的弱证据包含目录 active；目录可能先高亮而播放器仍未切换。
- 跨 iframe 聚合只在单文档内做好播放器评分；旧 ended 长视频仍可能在全局汇总中压过新 paused 视频。
- 未完成定位主要依赖章节级状态，缺少任务级明确状态。

### Fixed / Changed
- 新增 `VideoTaskItem`，`ChapterItem.VideoTasks` 与 `MainViewModel.VideoTasks`；章节和真实视频任务分开。
- 新增 `ScanVideoTasksAsync()`，扫描真实 `<video>` / 常见视频任务容器，保存 TaskKey、mediaId、currentSrc、documentUrl、chapterId/title、完成状态。
- `PlayerSnapshot` 新增 `IsVisible / TaskKey / ChapterTitleHint`；跨 iframe 汇总优先 playing -> not-ended -> visible -> readyState。
- `ResolveChapterFromEvidence` 增加播放器 -> 视频任务 -> 章节反向映射；进入学习页后自动刷新映射。
- `PlayerMatchesTargetChapterAsync` 不再让 active catalog 单独确认切换；弱证据不足时重新扫描章节+视频任务，只有结构闭环才成功。
- 当前章节点击路径移除 `allowCurrentPlayer=true` 的“旧播放器直接算切换成功”行为。
- `ResolveEffectiveCourseTitle` 按当前 `courseId` 约束选中/上次课程标题；过滤“学生学习页面”等通用标题。
- 未完成定位优先使用视频任务级明确 `CompletionKnown && !IsCompleted`。
- 播放器标题扩展任务块标题读取，播放器无标题时回退已绑定视频任务标题。
- 修复 `FindNextChapterCandidateAsync` 内嵌脚本重复 `const id` 的确定性语法错误。
- 版本、安装器、START、构建入口更新至 v1.22 / 1.22.0 / `BUILD_V1_22.bat`。

### Validation
- `python3 scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：39/39 PASS。
- 新增 Node 回归：视频任务扫描；“学生学习页面”不得成为课程名；PlayerSnapshot 任务/可见性/章节提示证据。
- 新增 MSTest 源码用例 2 项；当前 Linux 无 .NET，未执行。
- Windows Release Build / MSTest / WebView2 真实课程：NOT RUN。


## v1.23 — 2026-09-09 — v1.22 P0 自检集中修复

### 修复
- 跨 iframe 原生下一节：父页面无 `<video>` 但存在明确播放器 iframe 时，仍会搜索播放器附近/明确 next 控件；普通文本型“下一个”不再全页泛搜。
- 父任务证据合并：父页面任务块的 chapterId / chapterTitle / title / CompletionKnown 在去重前合并进子 iframe 真实 video，避免真实 video 优先时丢失章节信息。
- 稳定媒体身份：新增 `PlayerMediaEvidence`，`PlayerIdentity` 不再依赖 Duration / VideoTitle / ChapterTitle 等延迟变化元数据；同 MediaId 复用但 source 变化仍视为新媒体。
- 高倍速结束识别：新增 `PlaybackEndDetector`，按 1.5 秒轮询间隔和最近真实倍速计算末端容错，同时要求 currentTime 位于末端或发生明显回卷，降低手动暂停误报。
- 未知状态严格分离：`FocusFirstUnfinishedVideoTaskAsync`、同章节未完成任务推进、下一未完成章节候选均只接受明确 `completion === false / CompletionKnown && !IsCompleted`。

### 回归
- `python3 scripts/static_check.py`：PASS。
- `node --test scripts/adapter_regression.mjs`：42/42 PASS。
- 新增 Node：父页面无 video + 子 iframe 下一节、未知任务不作为未完成、同章节未知任务不自动播放。
- 新增 MSTest 源码：父任务证据合并、稳定媒体身份、2x ended 回卷补判；当前 Linux 无 .NET，未执行。
- Windows Release Build / MSTest / WebView2 真实课程：NOT RUN。

### 版本
- 程序、安装器、START 与默认构建入口更新至 v1.23 / 1.23.0 / `BUILD_V1_23.bat`。
# v1.35 - 2026-09-11

- 修复课程页面异步加载或新版课程卡标题节点导致左侧课程列表空白的问题。
- 课程空状态新增说明与“刷新课程”入口，成功识别后立即缓存。
- 全屏观看新增始终可见的独立退出栏，Esc/F11 保持有效。
- 修复全屏期间从任务栏或托盘恢复时窗口状态不一致。
- 新增新版课程卡扫描回归，并保留“返回课程/提示”防误识别验证。
