# TEST_REPORT - 当前版本 v1.36

## v1.36 懒加载视频与未完成章节操作回归 2026 09 11

- Adapter JavaScript：52/52 通过；新增 `src=index.html`、`ans-insertvideo-online`、JSON `data` 视频元数据与章节测验并存用例。
- 新用例确认：只返回真实视频；读取 `objectid` 与视频标题；“打开未完成”只点击视频 iframe，测验 iframe 不被点击。
- MSTest：36/36 通过。
- WPF Release 编译：通过，0 警告、0 错误。
- 静态检查：通过；覆盖懒加载 iframe 元数据、测验排除、按钮执行中状态与 v1.36 标识。
- 完整构建：Restore、Release Build、MSTest、quick-run、self-contained win-x64 publish 全部通过；本机未安装 Inno Setup，因此安装器按既定规则跳过。
- 本机界面截图：通过；v1.36 标题、课程空状态、刷新入口与三栏布局显示正常。
- 使用教学 DOCX/PDF：通过；换入 v1.36 实拍截图并逐页检查 4 页，无裁切、重叠、乱码或缺字。
- 用户版 ZIP：通过；顶层仅 DOCX、PDF、Markdown 教学与程序次级文件夹，解压后隐藏启动 6 秒保持运行。
- 开发者版 ZIP：通过；顶层为单一 `ChaoxingLearningAssistant_v1.36` 目录，包含源码、累计档案和 MASTER。
- MASTER：UTF-8 BOM 存在，无替换字符；内嵌载荷可恢复 149 个清单文件，逐文件哈希差异 0。
- 真实学习通账号页面：尚待使用 v1.36 成品复测；自动测试不冒充真实平台验收。

## v1.35 课程列表与全屏退出回归 2026 09 11

- Adapter JavaScript：51/51 通过；新增新版课程卡标题与仅含课程/班级 ID 的脚本按钮卡片用例，原有“返回课程/提示”排除继续通过。
- MSTest：36/36 通过。
- WPF Release 编译：通过，0 警告、0 错误。
- XAML/XML 与静态回归：通过；覆盖课程空状态、刷新入口、全屏退出栏、事件处理器和 v1.35 版本标识。
- 真实学习通课程列表：未运行，需要登录账号与对应学校课程页。
- 本机界面截图：通过；左侧空状态与“刷新课程”入口正常显示。
- 全屏按钮实机点击：通过；F11 后按钮可见，通过无障碍名称实际调用按钮，完整三栏界面成功恢复。
- 使用教学 DOCX/PDF：通过；使用 v1.35 实拍截图，4 页逐页渲染检查无裁切、重叠、乱码或缺字。
- 用户版 ZIP：通过；顶层仅 DOCX、PDF、Markdown 教学与程序次级文件夹，禁入项 0，解压后隐藏启动 6 秒保持运行。
- 开发者 MASTER：通过；UTF-8 BOM、内嵌载荷和 145 个清单文件校验，哈希差异 0。
- GitHub 公开源码：通过 GitHub 插件同步到 main，线上提交 `77893c3b93494f1e0a13fd6a594c29151752c6fe`；抽查 README、主界面和课程扫描源码均为 v1.35。
- GitHub Release 附件：BLOCKED；当前 GitHub 插件没有创建 Release 或上传附件接口，未改用浏览器或命令行绕过。用户版 ZIP 已在本地生成并完成结构与启动验证。

## v1.34 验证 2026 09 11

- MSTest：36/36 通过，含三项下一视频预告测试及“章节未完成但全部视频已完成”防误跳测试。
- 内嵌网页适配脚本：49/49 通过，含“返回课程附近提示标题不得成为课程”测试。
- WPF Release 编译：通过，0 警告、0 错误。
- 静态检查：通过，覆盖全屏网页模式、按钮反馈、视频任务筛选与 v1.34 版本标识。
- 界面截图：通过本机窗口渲染复核；深色按钮文字已恢复青白色，右栏重复操作已删除。
- 使用教学 DOCX/PDF：Word 后台导出后用 Poppler 渲染 4 页逐页检查，无裁切、重叠、乱码或缺字。
- 自包含 win-x64 构建：通过；完整构建再次确认 0 警告、0 错误及 36/36 测试通过。
- 用户版 ZIP：通过；最外层只有 DOCX、PDF、Markdown 教学和“学习通课程视频播放助手”次级文件夹，不含 MASTER 或源码。
- 开发者版 ZIP：通过；最外层只有单一 v1.34 开发目录，包含完整源码、累计档案和 MASTER，不再夹带旧版参考 ZIP。
- MASTER：UTF-8 BOM 存在，无替换字符或典型乱码；内嵌载荷 SHA256 通过，干净归档实际还原 139 个文件且逐文件哈希 0 差异。
- GitHub 公开源码：通过 GitHub 插件同步到 main，线上提交 `ac071454fad97944713d56bbec8bce9eb4721bff`；公开范围只含程序源码、测试、构建入口、功能介绍和原有公开教学，不上传开发者档案。
- GitHub Release 附件：BLOCKED；当前 GitHub 插件没有创建 Release 或上传附件接口，因此没有改用浏览器或命令行绕过。用户版 ZIP 已在本地生成并核对 SHA256，等待插件能力可用后上传。

## v1.33 验证 2026 09 11

- ChaoxingAdapter 网页脚本回归：PASS，48 项全部通过；新增“返回课程”过滤用例。
- .NET 单元测试：PASS，32 项全部通过。
- WPF Debug 编译：PASS，0 警告、0 错误。
- 使用教学 DOCX：PASS；Word 后台导出 PDF 后逐页检查 3 页，第二轮清除标题蓝线并确认表格与分页正常。
- 应用窗口自动截图：BLOCKED；进程成功启动，但自动化桌面没有暴露可截取的主窗口句柄，因此不宣称截图验收通过。
- 真实学习通课程：仍需账号环境实机验证课程列表、同章多视频和跨章节续播。
- 用户版 ZIP 结构：PASS；最外层为 `使用教学.docx`、`使用教学.pdf`、`使用教学.md` 和 `学习通课程播放辅助` 次级文件夹，不含 HTML、MASTER、错误档案或源码。
- 开发者版 ZIP 结构：PASS；包含完整源码、v1.33 Release、错误档案、测试报告和累计 MASTER。
- MASTER 编码与还原：PASS；UTF-8 BOM 存在，无替换字符或已知乱码标记；最终内嵌源码校验通过并还原 135 个文件。

## v1.32 GitHub 与下载包验证（2026-09-11）

- Release 编译：PASS，0 warning / 0 error。
- MSTest：32/32 PASS。
- Node 网页适配器回归：47/47 PASS；新增同章节跨 iframe 精确目标切换场景。
- 静态检查：PASS。
- 公开内容隐私扫描：PASS；没有发现真实账号、本机路径、学校身份参数、日志或密钥。命中项仅为虚构测试数据和安全提示文字。
- Windows 使用包结构：PASS；最外层只有 `使用教学.html`、`使用教学.md` 和 `学习通课程播放辅助` 次级文件夹；EXE 只有一份且位于次级文件夹。
- 运行数据排除：PASS；下载包不含 Data、Logs、Diagnostics 或 WebView2 用户目录。
- 用户版归档排除：PASS；未包含 RELEASE、CHANGELOG、ERROR_ARCHIVE、TEST_REPORT、MASTER、`.cs`、`.xaml` 或解决方案文件。
- 开发者版归档完整性：PASS；包含累计 MASTER、错误档案、测试报告、v1.25-v1.32 Release 和完整源码。
- 下载包解压启动：PASS；从实际 ZIP 解压后的 EXE 隐藏启动 5 秒保持运行，随后由测试流程关闭。
- 安装器：SKIPPED，本机未安装 Inno Setup 6；GitHub 自包含 ZIP 不受影响。
- GitHub 公开仓库与 Release：PASS；源码已推送到 `https://github.com/XXm1-256/ChaoxingLearningAssistant`，v1.32 Release 已发布到 `https://github.com/XXm1-256/ChaoxingLearningAssistant/releases/tag/v1.32`。发布页只手动上传用户版 `ChaoxingLearningAssistant_v1.32_Windows.zip`；开发者版未上传。仓库简介已改为纯功能说明，不含审美宣传。
- 真实课程全程连续播放：NOT RUN，不以发布成功代替平台实测。
- 同章多视频真实问题课程：AWAITING USER VALIDATION；自动化已覆盖失败结构，但仍需原课程自然结束复测。

## v1.31 界面、反馈与教学验证（2026-09-11）

- 验收目标：浅绿主题存在；按钮有可见按压位移；关键功能有结果提示；教学可首次显示并可重复打开。
- Release 编译：PASS，0 warning / 0 error。
- MSTest：32/32 PASS。
- 静态检查：增加 v1.31 防退化条件，覆盖主题色、按钮缩放/下沉、提示卡、教学入口、四步教学和设置即时反馈。
- Node 适配器回归：46/46 PASS；本版没有改动学习通页面适配逻辑。
- quick-run 发布：PASS；`artifacts\QUICK_RUN_READY.txt` 已写入 `Version: 1.31.0`。
- 自包含 win-x64 发布与便携 ZIP：PASS。
- 安装器：SKIPPED；本机未安装 Inno Setup 6，现有 quick-run 与便携包不受影响。
- MASTER：UTF-8 BOM 与乱码特征检查 PASS；内嵌源码载荷 SHA256 PASS；真实反向还原 PASS，共恢复 116 个文件。
- 成品启动冒烟：PASS；隐藏启动 5 秒未退出，WebView2 Runtime 152 初始化完成，正常进入登录等待状态，日志无启动异常。检查后由测试流程关闭。
- 真实界面目视与学习通账号长时间播放：NOT RUN；不能用编译或静态检查替代。

## v1.30 全课程待播清单验证（2026-09-11）

- Release 编译：PASS，0 warning / 0 error。
- MSTest：32/32 PASS；新增课程级候选顺序、起点与已核验排除、任务级未知状态覆盖。
- Node 内嵌适配器回归：46/46 PASS。
- 静态检查：PASS；固定全局复核、无视频确认和无法确认章节阻止完成。
- 真实课程：上一版已经实机确认能够自动切换下一视频；v1.30 的整门课程长时间连续运行仍需实机验收。

## v1.29 归档验证（2026-09-11）

- Release 编译：PASS，0 warning / 0 error。
- MSTest：29/29 PASS。
- Node 内嵌适配器回归：46/46 PASS。
- 静态检查：PASS。
- quick-run、自包含 win-x64 与便携 ZIP：PASS。
- 安装器：SKIPPED，未安装 Inno Setup 6。
- MASTER 必须通过 UTF-8 BOM、乱码特征扫描、内嵌载荷 SHA256 与安全恢复验证。
- 最终产品目标已记录，但整门课程长时间连续播放仍为 NOT RUN。

## v1.28 Windows 验证（2026-09-11）

| 检查 | 结果 | 说明 |
|---|---|---|
| Release Build / MSTest | PASS | 29/29 |
| Node adapter regression | PASS | 45/45 |
| Python static check | PASS | 固定同章节视频优先级及安全控件范围 |
| Release Build | PASS | 0 warning / 0 error |
| quick-run / self-contained win-x64 | PASS | 便携 ZIP 已生成 |
| 安装器 | SKIPPED | 未安装 Inno Setup 6 |
| 真实学习通课程 | NOT RUN | 需要实际账号与包含多视频、测试题的章节 |

新增回归场景：

1. 同章节后续视频没有完成状态标记时仍按顺序播放，但不会称作“未完成”。
2. 视频一与视频二之间存在测试题时，任务块扫描跳过测试题并点击视频二。
3. 页面只有“下一节”通用按钮时，播放器回退拒绝点击，随后交给安全的下一章节流程。

## v1.27 Windows 验证（2026-09-11）

环境：Windows 桌面环境；.NET SDK 8.0.424；VSTest 17.11.1；Node 22；Codex bundled Python 3。

| 检查 | 结果 | 说明 |
|---|---|---|
| Release Restore/Build | PASS | 主工程与测试工程成功还原、编译 |
| MSTest | PASS | 29/29 |
| Node adapter regression | PASS | 43/43；新增状态未知的自然下一章候选 |
| Python static check | PASS | 区分严格未完成定位与自然顺序续章 |
| quick-run publish | PASS | 生成框架依赖 EXE 与 `Version: 1.27.0` 标记 |
| self-contained win-x64 | PASS | 生成无需另装 .NET 8 Desktop Runtime 的便携 ZIP |
| 安装器 | SKIPPED | 未检测到 Inno Setup 6；不影响便携包 |
| 真实学习通课程 | NOT RUN | 缺少账号课程环境和另一台电脑日志 |

### 本轮验证覆盖

1. C# 真实编译覆盖新增异步播放重试、手动切章收尾和跨导航状态字段。
2. 旧 29 项模型、持久化、媒体证据和 XAML 回归全部通过。
3. 43 项 Node 测试直接执行 `ChaoxingAdapter.cs` 内嵌 JavaScript；新增用例证明后续章节无完成标记时仍返回真实 chapterId，并保持 `completionKnown=false`。
4. 静态门槛继续要求“打开未完成”使用明确状态，同时单独要求自然续章接受未知状态，防止两种需求再次混在一起。

### 未覆盖边界

- 没有真实账号，不声称已覆盖所有学校定制目录、播放器 iframe 或浏览器自动播放策略。
- 当前没有用户测试机日志，因此无法针对具体 DOM 模板做定点修正；本轮依据可复现的控制流缺口修复。
- 实测失败时优先提交运行日志，不需要截图长篇技术信息。

原始验证摘要位于 `docs/validation/v1.27/`。

## v1.26 Windows 正式验证（2026-09-11）

环境：Windows 10/11 桌面环境；.NET SDK 8.0.424；MSBuild 17.11.48；VSTest 17.11.1；Node 可用。

| 检查 | 结果 | 说明 |
|---|---|---|
| `dotnet restore` | PASS | 主工程和测试工程成功还原 |
| Release Build | PASS | 0 error / 0 warning |
| MSTest | PASS | 29/29，通过全部模型、运行时、持久化和真实 XAML 模板测试 |
| Node adapter regression | PASS | 42/42 |
| quick-run publish | PASS | 生成框架依赖 EXE 与版本标记 |
| self-contained win-x64 publish | PASS | 生成无需另装 .NET 8 Desktop Runtime 的便携包 |
| WPF 主窗口启动与视觉检查 | PASS | v1.26 窗口正常打开，主要控件和三栏布局正常呈现 |
| WebView2 初始化与登录页 | PASS | 学习通登录页正常加载；未填写或提交账号信息 |
| WebView2 真实课程 | NOT RUN | 需要实际登录账号与课程页面 |
| 安装器 | SKIPPED | 未检测到 Inno Setup 6；quick-run 和便携包不受影响 |

### 首次 Windows 编译发现

1. 原 v1.25 在 `MainWindow.xaml.cs:762` 报 5 个语法错误，测试无法开始。
2. 修复语法后，旧有 28 项测试中 27 项通过、1 项失败；失败揭示 mediaId 复用时媒体路径变化判断错误。
3. 修复媒体判断并新增 1 项组合测试后，29/29 PASS。
4. `DataTestMethod` 过时警告已修复，最终构建无警告。

### 发布门槛

自 v1.26 起，Python/Node 静态或脚本检查不能代替 C# 编译。正式归档至少需要记录真实 Restore、Release Build、MSTest 与 publish 结果；无法执行的项目必须明确写 NOT RUN。

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

> 当前 v1.24（2026-09-10）：`python3 scripts/static_check.py` PASS；`node --test scripts/adapter_regression.mjs` 42/42 PASS；XML/XAML 解析与 C# 花括号平衡检查 PASS。当前环境没有 dotnet/Windows/WebView2，因此 Release Build、MSTest 运行、WPF 加载和真实课程验收均为 NOT RUN。以下历史结果只代表对应历史版本。

## v1.24 新增回归覆盖

- CDN 签名变化但 TaskKey 相同仍为同一媒体任务。
- 非法设置值归一化并拒绝非 http/https 恢复地址。
- 播放统计排除暂停和长休眠区间。
- 媒体变化后统计计时器重置。
- 静态守卫覆盖课程缓存、JSON backup、诊断完整快照和视频切换结算。

> 当前 v1.15：非 Windows 源码回归已执行；Windows Build/MSTest/WebView2 真实课程连续播放未执行。历史 PASS 只代表对应历史版本。

> 当前 v1.13：本轮源码检查通过，Windows Build/MSTest/真实播放均未执行。下面历史版本的 PASS 不代表 v1.13 已验收。

# TEST_REPORT

版本：v1.3
日期：2026-09-07
性质：累计式测试报告。不得把未执行项目写成 PASS。

## 1. 已获得的真实 Windows 测试环境

来自 v1.2 用户实机诊断：

- OS：Windows x64
- .NET SDK：8.0.424
- MSBuild：17.11.48
- Microsoft.WindowsDesktop.App 8.0.x 已安装
- Solution 路径识别成功
- PowerShell 构建脚本可正常解析与执行

## 2. v1.2 真实构建结果

### Restore
PASS

- 主项目还原成功。
- MSTest 项目还原成功。

### Build
FAIL

- 0 warning
- 1 error
- 错误 ID：ERR-BUILD-001
- 错误：CS0104 `Application` 在 `System.Windows.Forms.Application` 与 `System.Windows.Application` 之间存在歧义。
- 定位：`src/ChaoxingLearningAssistant/App.xaml.cs`。

### Test / Publish / Portable / Installer
NOT RUN

原因：Build 阶段失败，后续步骤按 fail-fast 规则停止。

## 3. v1.3 修复后静态检查

| 项目 | 状态 | 结果 |
|---|---|---|
| App WPF Application 显式限定 | PASS | 已使用 `System.Windows.Application` |
| WPF MessageBox 显式限定 | PASS | 已消除 WinForms 同名类型风险 |
| ThemeService Application.Current 显式限定 | PASS | 已修复 |
| WPF Color 显式限定 | PASS | 已修复 |
| WPF/WinForms 类型歧义回归扫描 | PASS | static_check.py 已新增规则 |
| `.csproj` / XAML XML 解析 | PASS | 可解析 |
| XAML 事件处理器存在性 | PASS | 未发现缺失 |
| TODO / FIXME / “自行实现”扫描 | PASS | 未发现占位实现 |

## 4. 当前验收矩阵

| 功能/阶段 | 状态 | 说明 |
|---|---|---|
| 项目目录检测 | PASS | Windows 实机通过 |
| .NET 8 SDK 检测 | PASS | Windows 实机 8.0.424 |
| PowerShell 构建脚本解析 | PASS | 已进入 Restore / Build |
| NuGet Restore | PASS | 主工程和测试工程均真实成功 |
| v1.2 Release Build | FAIL | ERR-BUILD-001，已在 v1.3 修源码 |
| v1.3 Release Build | NOT TESTED | 等待 Windows 重跑 |
| MSTest | NOT TESTED | Build 成功后执行 |
| win-x64 Publish | NOT TESTED | Build/Test 成功后执行 |
| Portable ZIP | NOT TESTED | Publish 成功后生成 |
| Installer | NOT TESTED | 需要 Inno Setup 6；缺失时构建脚本会明确跳过 |
| WebView2 真实启动 | NOT TESTED | 等待生成 EXE 后实机验证 |
| 学习通真实账号联调 | NOT TESTED | 等待程序成功启动后执行 |

## 5. 下一次 Windows 验证标准

解压 v1.3 后双击：

`STEP1_CHECK_AND_BUILD.bat`

期待顺序：

1. Project folder detected — PASS。
2. .NET 8 SDK detected — PASS。
3. Restore — PASS。
4. Build — 期待 PASS。
5. Test — 期待 PASS。
6. Publish self-contained win-x64 — 期待 PASS。
7. Portable ZIP — 期待生成。
8. Installer — 安装 Inno Setup 6 时期待生成；未安装时允许 SKIPPED。

如果出现新错误：
- 不由用户自行修复。
- 保存 `build_diagnostic.txt`。
- 分配新的 ERR-* ID。
- 累计写入 ERROR_ARCHIVE、TEST_REPORT、CHANGELOG、MASTER。

## 6. 当前结论

当前 v1.3 是“已根据真实 Windows 编译日志修复首个源码编译错误、静态回归检查通过、等待第二次 Windows Build”的版本。

不得将 v1.3 Build、Test、Publish 或 EXE 运行状态提前标为 PASS。


## v1.4 delivery/diagnostic validation

| Item | Status | Evidence |
|---|---|---|
| v1.3 project folder detection on Windows | PASS | User console output |
| v1.3 .NET 8 SDK detection on Windows | PASS | SDK 8.0.424 detected |
| v1.3 build script invocation | PASS | `scripts\build.ps1` was launched |
| v1.3 second Release Build | FAIL / ROOT CAUSE PENDING | Launcher returned exit code 1, detailed log unavailable |
| ASCII/no-BOM build launcher | PASS (static) | `BUILD_V14.bat` contains ASCII bytes only and no BOM |
| ASCII log filename | PASS (static) | `BUILD_LOG.txt` hard-coded in launcher |
| Auto-open failed log | PASS (static) | launcher calls Notepad on failure |
| Windows validation of v1.4 launcher | NOT TESTED | Requires next user run |

Do not classify the v1.3 second-build failure as a new compiler error until `BUILD_LOG.txt` is retrieved.


## v1.5 validation update

### Real Windows evidence from v1.4

| Item | Status | Evidence |
|---|---|---|
| Project root detection | PASS | BUILD_LOG v1.4 |
| .NET 8 SDK detection | PASS | SDK 8.0.424 |
| Restore main project | PASS | restored successfully |
| Restore test project | PASS | restored successfully |
| ERR-BUILD-001 regression | PASS | previous Application CS0104 absent |
| ASCII BUILD_LOG handoff | PASS | BUILD_LOG.txt successfully generated and returned |
| Release Build v1.4 | FAIL | 49 errors / 2 warnings |
| System.IO symbols | FAIL -> FIXED_IN_v1.5 | 48 CS0103 |
| WPF ColorConverter | FAIL -> FIXED_IN_v1.5 | 1 CS0104 |
| DPI analyzer WFAC010 | WARN -> HANDLED_IN_v1.5 | targeted suppression with WPF rationale |

### v1.5 static validation

| Item | Status |
|---|---|
| XML/XAML parsing | PASS |
| XAML event handler binding scan | PASS |
| TODO/FIXME/placeholder scan | PASS |
| WPF/WinForms ambiguity regression scan | PASS |
| System.IO global alias regression scan | PASS |
| ColorConverter qualification regression scan | PASS |
| BUILD_V15.bat ASCII/no-BOM | PASS |

### Still requires Windows execution

- v1.5 Release Build.
- MSTest.
- self-contained win-x64 Publish.
- Portable ZIP creation.
- optional Inno Setup installer build.
- EXE first launch and WebView2 runtime initialization.


## v1.5 Windows run / v1.6 regression status

| Item | Status | Evidence |
|---|---|---|
| Project folder detection | PASS | v1.5 Windows `BUILD_LOG.txt` |
| .NET 8 SDK detection | PASS | SDK 8.0.424 |
| Main project Restore | PASS | v1.5 log |
| Test project Restore | PASS | v1.5 log |
| v1.5 Clean stage | FAIL | MSB1001, unsupported `--no-restore` |
| v1.5 C# Build | NOT REACHED | Clean failed before Build |
| ERR-BUILD-002 fix | NOT TESTED ON WINDOWS | Build not reached |
| ERR-BUILD-003 fix | NOT TESTED ON WINDOWS | Build not reached |
| v1.6 build-script static regression | PASS | unsupported Clean combination removed and guarded |
| BUILD_V16.bat ASCII/no-BOM | PASS (static) | byte validation |
| v1.6 Windows Build | NOT TESTED | requires next user run |

The v1.5 failure is classified as a build-script regression, not as a new application-source compile failure.


## v1.6 Windows real-build evidence imported into v1.7

| Item | Status | Evidence |
|---|---|---|
| .NET 8 SDK detection | PASS | SDK 8.0.424 |
| Restore solution | PASS | Both projects restored |
| Release Build | PASS | 0 warnings, 0 errors |
| Automated tests | PASS | 4 passed, 0 failed, 0 skipped |
| Self-contained publish | INTERRUPTED | Process reached publish stage and ended with Ctrl+C |
| Installer | NOT TESTED | Publish did not finish |
| Quick-run publish | NOT TESTED | Added in v1.7 |

The v1.6 log is archived under `docs/build_logs/BUILD_LOG_v1.6_windows_build_test_pass_publish_interrupted.txt`.


## v1.8 Windows Build evidence imported into v1.9

| Item | Status | Evidence |
|---|---|---|
| Project folder detection | PASS | v1.8 `BUILD_LOG.txt` |
| .NET 8 SDK detection | PASS | SDK 8.0.424 |
| Restore main project | PASS | Restored successfully |
| Restore test project | PASS | Restored successfully |
| v1.8 Release Build | FAIL | ERR-BUILD-004 |
| Warning count | PASS | 0 warnings |
| Error count | FAIL | 1 error |
| ERR-BUILD-004 source fix | PASS (static) | `System.Windows.Media.Color` fully qualified |
| Bare `(Color)` regression scan | PASS (static) | no unqualified casts found |
| v1.9 Windows Release Build | NOT TESTED | requires next Windows run |
| v1.9 EXE visual validation | NOT TESTED | requires successful Windows build + launch |

The raw v1.8 log is archived as:
`docs/build_logs/BUILD_LOG_v1.8_failed_ERR-BUILD-004.txt`.


## v1.9 real Windows runtime acceptance imported into v1.10

| Item | Status | Evidence |
|---|---|---|
| EXE launch | PASS | real Windows screenshot |
| WPF main UI rendering | PASS | real Windows screenshot |
| High-tech dark theme | PASS | real Windows screenshot |
| WebView2 initialization | PASS | Chaoxing page rendered |
| Chaoxing login page load | PASS | real page visible in WebView2 |
| Login-page safety pause | PASS | UI status shows automation paused |
| Horizontal movement buttons | NOT TESTED | added in v1.10 |
| Shift+wheel horizontal movement | NOT TESTED | added in v1.10 |
| Browser focus fullscreen F11/Esc | NOT TESTED | added in v1.10 |
| Playback-time full display | NOT TESTED | added in v1.10 |

v1.10 must be validated on Windows with a post-login course page that is wider than the center viewport.


## Version-number correction

The package previously labeled v2.0 was not a formal major release.
Its functional content has been re-baselined as v1.10 with no behavioral rollback.


## v1.10 Windows Build evidence imported into v1.11

| Item | Status | Evidence |
|---|---|---|
| Project folder detection | PASS | v1.10 BUILD_LOG |
| .NET 8 SDK detection | PASS | SDK 8.0.424 |
| Restore | PASS | all projects up to date |
| v1.10 Release Build | FAIL | ERR-BUILD-005 |
| Warning count | PASS | 0 |
| Error count | FAIL | 1 |
| ERR-BUILD-005 source fix | PASS (static) | WPF KeyEventArgs fully qualified |
| v1.11 Windows Build | NOT TESTED | requires next Windows run |


## v1.11 Windows runtime evidence imported into v1.12

| Item | Status | Evidence |
|---|---|---|
| WebView2 initialization | PASS | Runtime 152.0.4191.66 |
| Login/manual-intervention safety state | PASS | Login/CAPTCHA path paused automation |
| Browser focus fullscreen entry | PASS | `UI-FULLSCREEN` runtime event |
| Real course page navigation | PASS | mooc1/mooc2 student-course URLs loaded |
| Course scanner | PARTIAL PASS | 1 course candidate found on student-study page |
| Chapter/video scanner | OBSERVED 0 | pending retest after UI crash fix |
| Course-list UI render | FAIL | ERR-RUNTIME-001 |
| ERR-RUNTIME-001 source fix | PASS (static) | ProgressBar binding is Mode=OneWay |
| v1.12 runtime retest | NOT TESTED | requires Windows run |

The supplied log contained 44 `APP-UNHANDLED` records; all matched the same ProgressPercent read-only binding failure.


## v1.13 — 本轮验证记录（2026-09-08）

环境：Linux，Python 3.12.13，v24.19.0；没有 dotnet SDK / Windows / WebView2。

| 检查 | 结果 | 实际覆盖 |
| --- | --- | --- |
| scripts/static_check.py | PASS | XAML/XML、事件引用、历史 WPF/WinForms 歧义防护、新 Run/ProgressBar OneWay、版本与启动器 |
| 绑定故障注入 | PASS | 临时还原 v1.12 漏掉的 Run 模式，检查器返回非零并报告 ERR-RUNTIME-002；原字节随后恢复 |
| node --test scripts/adapter_regression.mjs | 14/14 PASS | 实际嵌入脚本；完成/未完成文案、空播放器、非有限媒体数值、可见视频优先、单次同步播放请求、Promise 拒绝和同步失败 |
| BAT/PowerShell 编码 | PASS | BAT ASCII 无 BOM、CRLF；PowerShell UTF-8 BOM |
| Windows Release Build | NOT RUN | 当前无 Windows/.NET；由 BUILD_V1_13.bat 执行 |
| Windows MSTest | NOT RUN | 原 4 项 + 新 5 项，共 9 项；其中 1 项触发实际课程模板布局 |
| 真实学习通点击视频 | NOT RUN | 需要用户已登录课程；不伪造测试通过 |
| 便携发布 / 安装卸载 | NOT RUN | BUILD_FULL.bat 可选 |

原始本轮检查输出：docs/validation/v1.13/adapter_regression.txt、static_check.txt。
MSTest 只有通过后才进入 quick-run 发布；静态检查不等于 C# 编译通过。

Windows 首次验收顺序：退出旧版 → 新目录解压 → START.bat → 确认标题 v1.13 → 进入原课程点击视频 → 核对是否仍弹错与时间是否前进。
进一步确认：快速切换两页不会被旧重试拉回；视频最后 0.4 秒不会提前跳转；结束后通知不重复；未完成章节不被错误排除；关闭时无后续弹窗。
若失败，只提供 BUILD_LOG.txt（构建）或最新 .log（运行），无需先重装环境或清除个人数据。

## v1.14 — 本轮验证记录（2026-09-08）

| 项目 | 状态 | 说明 |
|---|---|---|
| Adapter JavaScript 回归 | PASS (16/16) | 运行 `node --test scripts/adapter_regression.mjs`，含目录状态、播放器、onclick-only 章节、synthetic/direct URL 标记 |
| XAML/XML / 静态回归 | PASS | 运行 `python scripts/static_check.py` |
| 登录跳转保护静态回归 | PASS | 检查登录 URL 分类、synthetic URL 禁止自动回退、原生点击导航竞态保护、最近学习页恢复逻辑 token |
| Windows Release Build | NOT RUN | 当前环境无 .NET SDK / Windows Desktop |
| MSTest | NOT RUN | 需 Windows + .NET 8 SDK |
| WebView2 真实课程目录 | NOT RUN | 需登录目标学习通课程 |
| 自动切下一视频并续播 | NOT RUN | 需真实视频自然结束测试 |
| 倍速跨章节继承 | NOT RUN | 需真实播放器倍速 UI 验证 |

原始检查输出：`docs/validation/v1.14/adapter_regression.txt`、`docs/validation/v1.14/static_check.txt`。
Windows 首次验收建议：标题确认 v1.14 → 进入曾经左侧为空的课程 → 核对“章节 / 视频”列表 → 设 1.5x/2x → 让视频自然结束 → 核对是否自动切下一视频、持续播放且倍速保持。再连续切 3–5 个章节，确认不会因程序合成 URL 回退而突然进入登录页；若平台真实要求登录，完成登录后确认会回到最近学习页。


## v1.15 — 本轮验证记录（2026-09-08）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj XML 解析 | PASS | `scripts/static_check.py` |
| XAML 事件处理器存在性 | PASS | 静态扫描 |
| 版本一致性 1.15.0 | PASS | csproj / START / BUILD / installer 静态检查 |
| 最小化不 Hide | PASS (source) | OnStateChanged 中无 Hide，存在 ShowInTaskbar 保持逻辑 |
| 深色设置页前景色 | PASS (source) | SettingsWindow + ThemeService + App.xaml 防退化 token |
| 目录扫描 JS | PASS | Node 回归含 posCatalog / catalog_title |
| 适配器 JS 回归 | PASS | 20/20 |
| 原生下一章节 fallback | PASS (source) | `FindNextChapterCandidateAsync` + `CX-AUTO-NEXT-FALLBACK` 静态检查 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet |
| MSTest | NOT RUN | 依赖 Windows/.NET |
| WebView2 真实课程目录 | NOT RUN | 需目标 Windows 实机 |
| 视频结束自动切下一节 | NOT RUN | 需真实学习通课程 |
| 最小化任务栏实机显示 | NOT RUN | 需 Windows shell 验收 |

本轮不把源码静态检查等同于 Windows 真机通过。

## v1.16 — 本轮验证记录（2026-09-08）

环境：Linux；Python/Node 可用；无 dotnet SDK、Windows Desktop、WebView2 目标运行环境。

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj XML 解析 | PASS | `scripts/static_check.py` |
| XAML 新事件处理器存在性 | PASS | 含 `PreviewMouseLeftButtonUp` / `JumpToUnfinished_Click` |
| 版本一致性 1.16.0 | PASS | csproj / START / BUILD / installer 静态检查 |
| 非全屏宽屏观看源码防退化 | PASS | `EnterCompactViewingMode` 不设置无边框或强制最大化 |
| 单击章节打开 | PASS (source) | XAML + handler + 原生章节点击路径静态检查 |
| 完成状态置信度 | PASS | Node 回归验证 explicit unfinished 与 unknown 分离 |
| 章节标题清洗 | PASS | Node 回归确认 `第三节 视频 待完成任务点` 显示为 `第三节` |
| 原生下一章节排除当前 ID | PASS | Node/静态检查 |
| ended 同媒体源防重播 | PASS (source) | `CX-AUTO-NEXT-REPLAY-GUARD` + source equality guard |
| 未完成视频任务点聚焦 JS | PASS (parse/source) | embedded JS 语法回归 |
| 适配器 JS 回归 | PASS | 25/25 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet |
| MSTest | NOT RUN | 依赖 Windows/.NET |
| WebView2 真实章节单击 | NOT RUN | 需目标 Windows 实机 |
| 真实“定位未完成” | NOT RUN | 需目标课程完成状态 DOM |
| 视频 ended → 下一未完成 → 非重播 | NOT RUN | 需真实课程自然结束验证 |
| 非全屏宽屏观看 Windows 体验 | NOT RUN | 需 Windows shell 验收 |

原始输出：`docs/validation/v1.16/static_check.txt`、`adapter_regression.txt`、`environment.txt`。

禁止把 PASS (source) 或 JS parse 回归写成 Windows 真机 PASS。


## v1.17 — Windows v1.16 证据驱动修复（2026-09-08）

### v1.16 Windows 实机证据

| 项目 | 状态 | 结果 |
|---|---|---|
| .NET 8 SDK | PASS | 8.0.424 |
| Restore | PASS | 主项目与测试项目均恢复成功 |
| Release Build | PASS | 0 Error / 2 Warning |
| MSTest | FAIL (10/11) | 唯一失败为 `RealCourseTemplate_RendersReadOnlyProgress_AndUpdatesCounts` |
| quick-run Publish | NOT REACHED | build.ps1 在 Test exit code 1 后按设计停止 |

### v1.17 源码修复

| 项目 | 状态 | 说明 |
|---|---|---|
| 真实 CourseList 模板回归测试 | FIXED_IN_SOURCE | 改为验证 `ProgressSummary` TextBlock + `ProgressPercent` ProgressBar |
| CS4014 | FIXED_IN_SOURCE | `_ = Dispatcher.BeginInvoke(...)` |
| CS8602 | FIXED_IN_SOURCE | async fallback 使用局部 `adapter` |
| v1.16 观看/章节/防重播行为 | UNCHANGED | 本轮不修改核心业务路径 |
| Linux static_check | PASS | `docs/validation/v1.17/static_check.txt` |
| Linux adapter JS regression | PASS (25/25) | `docs/validation/v1.17/adapter_regression.txt` |
| v1.17 Windows Release Build | NOT RUN | 等待宝贝 Windows `BUILD_V1_17.bat` |
| v1.17 MSTest | NOT RUN | 目标 11/11 PASS |

禁止把 v1.16 的 Build PASS 直接写成 v1.17 Build PASS；v1.17 必须获得新的 Windows 日志。


## v1.18 — 章节/播放器同步回归（2026-09-08）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj XML 与事件处理器 | PASS | `scripts/static_check.py` |
| 版本一致性 1.18.0 | PASS | csproj / App / START / BUILD / installer 静态检查 |
| 左侧不得预先跳转 | PASS (source) | `SelectedItem` OneWay + `PreviewMouseLeftButtonDown` 拦截默认选择 |
| 刷新不得自动选第一未完成 | PASS (source) | 已删除 `current ??= FindFirstUnfinishedNavigationCandidate(...)` 旧回退 |
| 明确未完成判定 | PASS (source) | 仅 `CompletionKnown && !IsCompleted` |
| 右侧章节/播放器分离 | PASS (source) | `CurrentChapterText` + `CurrentVideoText` |
| 播放器身份切换确认 | PASS (source) | `PlayerIdentity` + `WaitForManualPlayerSwitchAsync` + `WaitForPendingPlayerSwitchAsync` |
| 旧媒体防重播 | PASS (source) | `IsStaleEndedPlayer` 与 ended identity gate |
| 真实章节目录去噪 | PASS (JS regression) | task-point blocks 不进入 ChapterItem；UI 不呈现 TaskTypeText/StatusText |
| Adapter JavaScript regression | PASS | 27/27 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WebView2 真实章节点击 | NOT RUN | 需真实学习通课程 DOM |
| 真实右侧章节与播放器一致性 | NOT RUN | 需目标学校课程实机 |
| ended → 下一真实媒体 → 自动播放 | NOT RUN | 需真实课程自然结束验证 |

本轮 PASS (source) 只证明源码防退化规则和内嵌 JS 回归，不等同于 Windows/WebView2 真机通过。

## v1.19 — 界面语言回归（2026-09-08）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj XML 与事件处理器 | PASS | `scripts/static_check.py` |
| 版本一致性 1.19.0 | PASS | csproj / App / START / BUILD / installer 静态检查 |
| 控制台式英文标签移除 | PASS (source) | Main/Settings/FirstRun/Log/Stats 可见 XAML 扫描 |
| “建筑群”等游戏化词语防退化 | PASS (source) | v1.19 banned_ui_terms 静态规则 |
| 主界面中文标签 | PASS (source) | 课程目录 / 播放信息 / 视频状态 / 下一视频 / 开发者诊断 |
| 首次使用文案 | PASS (source) | 首次设置 / 主界面，不再使用学习工作区 / 工作台 |
| 设置页文案 | PASS (source) | 深色主题，不再使用“科技主题” |
| v1.18 章节同步防退化 | PASS (source) | 原 v1.18 静态规则继续执行 |
| Adapter JavaScript regression | PASS | 27/27 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WPF 实际视觉验收 | NOT RUN | 需 Windows 实机 |
| WebView2 真实课程 | NOT RUN | 需目标学校课程实机 |

本轮只把 Linux 静态/Node 回归标为 PASS；Windows 编译和真实界面仍需新日志确认。


## v1.20 — ended → 下一真实视频自动续播回归（2026-09-08）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj XML 与事件处理器 | PASS | `scripts/static_check.py` |
| 版本一致性 1.20.0 | PASS | csproj / App / START / BUILD / installer 静态检查 |
| 同章节下一真实视频脚本 | PASS (JS) | `AdvanceToNextVideoTaskAsync` |
| 复用 MediaId + 不同 currentSrc | PASS (JS) | 只播放 next source，不重播 ended source |
| 新 paused 视频优先于旧 ended 视频 | PASS (JS) | GetPlayerSnapshot 评分回归 |
| ended 当前章节证据 | PASS (source) | `snapshot.ChapterId` + `snapshot.DocumentUrl` |
| 组合播放器身份 | PASS (source) | MediaId/source/document/chapter/DomIndex/title/duration |
| Adapter JavaScript regression | PASS | 30/30 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WebView2 真实视频自然结束自动跳转 | NOT RUN | 需目标课程实机验证 |
| 同章节多视频真实续播 | NOT RUN | 需目标课程实机验证 |

本轮 source/JS PASS 证明新的自动续播路径和防重播条件已进入真实源码并通过夹具回归；不能替代 Windows/WebView2 实机课程验证。


## v1.20 clarification regression
- 平台原生下一视频控件脚本：PASS（Node 解析 + 模拟真实 next 按钮点击）。
- 页面原生 `toOld(...)` 章节动作重试脚本：PASS（Node 解析）。
- 自然结束补判、真实播放器核验与自动续播调用链：STATIC PASS。
- Windows/WebView2 真实学习通网页自动切换：NOT RUN，必须以 Windows 实机课程验证为准。

## v1.21 — 自动续播真实导航升级回归（2026-09-08）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj / 版本一致性 1.21.0 | PASS | `scripts/static_check.py` |
| `toOld(...)` 完整参数转发 | PASS (JS) | 回归验证第 4 参数 `0` 未丢失 |
| 当前 studentstudy 上下文真实导航 | PASS (JS) | chapterId 被替换；courseId/clazzid/cpi/enc/openc 保持不变 |
| 播放器外层/全局 next 控件 | PASS (JS) | 不要求按钮位于 `<video>` 祖先树 |
| v1.20 同章节多视频 / MediaId 复用 / 防重播 | PASS (regression) | 原回归继续通过 |
| Adapter JavaScript regression | PASS | 36/36 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WebView2 真实自然结束→下一视频 | NOT RUN | 需目标学校真实课程 |

本轮 PASS 证明真实导航升级逻辑已经进入源码并通过本地脚本回归；不等同于 Windows/学习通实机已经成功自动跳转。


## v1.22 — 课程/章节/视频任务结构绑定回归（2026-09-09）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj / 版本一致性 1.22.0 | PASS | `scripts/static_check.py` |
| VideoTaskItem 独立模型 | PASS (source) | chapter/task/player 分层存在 |
| 通用“学生学习页面”课程名过滤 | PASS (JS) | 不再生成课程名；“材料力学 - 学生学习页面”提取为“材料力学” |
| 真实视频任务扫描 | PASS (JS) | chapterId/title/source/mediaId/明确未完成均被保留 |
| PlayerSnapshot 任务身份 | PASS (JS) | IsVisible / TaskKey / ChapterTitleHint |
| 旧 ended vs 新 paused 播放器 | PASS (regression) | v1.20 用例继续通过 |
| 下一章节内嵌脚本语法 | PASS (JS) | 重复 const 修复后 Node 解析 |
| Adapter JavaScript regression | PASS | 39/39 |
| 新增 MSTest 源码用例 | ADDED / NOT RUN | 当前环境无 .NET |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WebView2 进入视频自动读取章节 | NOT RUN | 需目标学校真实课程 |
| WebView2 点击章节真实切换视频 | NOT RUN | 需目标学校真实课程 |
| WebView2 自然结束真实自动续播 | NOT RUN | 需目标学校真实课程 |

结论：v1.22 的结构性修复和 39 项内嵌 JS 回归已在当前环境通过；Windows/WPF/WebView2/真实课程仍不得提前标记 PASS。


## v1.23 — v1.22 P0 自检修复回归（2026-09-09）

| 项目 | 状态 | 结果 |
|---|---|---|
| XAML / csproj / 版本一致性 1.23.0 | PASS | `scripts/static_check.py` |
| 父页面无 `<video>` + 子 iframe 播放器下一节 | PASS (JS) | 父页面仍能命中明确 next 控件 |
| 父任务证据合并到真实 iframe video | ADDED / MSTest NOT RUN | C# 纯逻辑用例已加入；当前无 dotnet |
| 稳定播放器身份 | ADDED / MSTest NOT RUN | title/duration 变化不改变 identity；source 变化判新媒体 |
| 2x 高倍速 ended 回卷补判 | ADDED / MSTest NOT RUN | 纯逻辑用例已加入；普通 97% 手动暂停不判结束 |
| 状态未知不作为“未完成” | PASS (JS) | Focus helper 返回 false |
| 同章节未知任务不自动推进 | PASS (JS) | 不调用未知视频 play |
| 下一章节候选未知状态 | PASS (source/static) | 只接受 `completion === false` |
| Adapter JavaScript regression | PASS | 42/42 |
| .NET 8 Release Build | NOT RUN | 当前环境无 dotnet/Windows Desktop |
| MSTest | NOT RUN | 需 Windows/.NET |
| WebView2 进入视频自动读取章节 | NOT RUN | 需目标学校真实课程 |
| WebView2 点击章节真实切换视频 | NOT RUN | 需目标学校真实课程 |
| WebView2 2x 自然结束真实自动续播 | NOT RUN | 需目标学校真实课程 |

结论：v1.23 的 5 个 P0 源码修复、静态守卫和 42 项内嵌 JavaScript 回归在当前环境通过；C# 新增纯逻辑测试已写入但无法在当前 Linux 容器执行。Windows/WPF/WebView2 真实课程仍不得提前标记 PASS。


## v1.24 / v1.25 结果更正与补档

### v1.24

- 当时记录的 Python 静态检查与 Node 42/42 结果继续有效。
- Windows Build/MSTest 当时为 NOT RUN。
- v1.26 静态对照发现 v1.24 原始源码已包含 ERR-BUILD-006，因此不能把该源码视为可构建版本。
- v1.24 新增的统计、缓存、设置和媒体测试均在 v1.26 修复后进入 Windows 测试集并通过。

### v1.25

- 原阶段记录的 Python 静态检查 PASS 与 Node 42/42 PASS 继续有效，但覆盖范围不足。
- v1.26 首次真实编译原 v1.25：FAIL，5 个编译错误，均来自 ERR-BUILD-006。
- 修复编译错误后首次运行旧测试集：27/28 PASS，ERR-MEDIA-001 失败。
- 修复媒体身份逻辑并增加新测试后的最终结果：29/29 PASS。
- v1.25 新增的 8 类持久化场景已在最终测试集中执行并通过，不再是 NOT RUN。
## v1.35 — 课程列表与全屏退出回归（2026-09-11）

| 项目 | 状态 | 说明 |
|---|---|---|
| Adapter JavaScript 回归 | PASS (50/50) | 新增新版 course-card `.title` 读取；原有“返回课程/提示”排除继续通过 |
| MSTest | PASS (36/36) | Release / no-restore |
| Windows Release Build | PASS | 0 Warning / 0 Error |
| XAML/XML / 静态回归 | PASS | 新增界面与版本结构可解析 |
| 真实学习通课程列表 | NOT RUN | 需要宝贝已登录账号与学校课程页 |
| 全屏按钮实机点击 | NOT RUN | 编译与事件连接通过，仍需成品窗口点击验收 |
