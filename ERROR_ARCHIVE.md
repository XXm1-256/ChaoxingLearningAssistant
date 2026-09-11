# ERROR_ARCHIVE - 当前版本 v1.36

## v1.36 下一视频为空与未完成章节按钮无反馈 2026 09 11

- 用户现象：播放时仍显示“未发现待播放的下一视频”；点击“打开未完成章节”没有反应。
- 原因：部分学习通页面把未激活视频保存在 iframe 中，iframe 的 `src` 只是通用 `index.html`，视频类型与名称实际位于 `ans-insertvideo-online`、`data`、`_src`、`objectid` 等字段。原识别器只检查 `src` 和外层 class，因此漏掉任务；按钮同时缺少执行中状态，等待扫描和切换期间像是没有响应。
- 修复：读取 iframe 完整任务元数据并解析 JSON 标题；在扫描、聚焦与同章切换三条路径统一识别；继续排除 work/quiz 等答题任务。按钮点击后立刻进入忙碌状态并显示阶段提示。
- 防复发：新增同一父页面同时包含懒加载视频与章节测验的 DOM 回归，要求只识别并点击视频，且读出真实标题和媒体 ID。

## v1.35 课程列表空白与全屏退出入口缺失 2026 09 11

- 用户反馈：左侧课程列表完全空白；全屏只能凭经验按 Esc 退出，没有可见出口。
- 原因：课程页卡片可能在页面完成导航后继续异步加载，原逻辑只立即扫描一次；上版为避免“提示”伪课程收紧了标题节点，但没有兼容新版课程卡的 `.title`、`.name`、`.ktmc` 等专用节点。全屏实现同时隐藏了包含全屏按钮的工具栏。
- 修复：空列表自动短暂重试课程扫描，扩充限定在课程卡内的标题节点，识别结果写入缓存；空状态提供“刷新课程”。全屏保留 46 像素退出栏及按钮，并保持 Esc/F11。
- 防复发：Node 直接执行真实内嵌扫描脚本，验证新版课程卡标题可读取且“返回课程/提示”仍不会成为课程；WPF 编译、MSTest 与静态检查覆盖事件和布局资源。

## v1.34 课程伪卡、下一视频空白与视觉偏差 2026 09 11

- 现象：课程栏出现“提示 / 进入课程后同步视频进度”。原因：返回链接本身被清洗为空后，扫描器从过宽父容器借用了“提示”标题；旧结果随后进入课程缓存。
- 修复：导航链接在读取父容器前直接排除；缩窄课程标题节点；加载时清理旧伪卡；学习页识别到真实课程后补齐课程卡。
- 现象：播放期间下一视频显示横线。原因：旧逻辑只在结束处理或跳转阶段赋值，轮询播放状态时没有计算预告。
- 修复：每次播放器轮询根据当前任务点、章节内顺序和后续章节任务点重新计算，并明确排除非视频任务和已完成视频。
- 现象：成品只用了浅绿配色，结构与确认设计图差距明显。原因：上一版只做了定向配色与局部卡片，没有完整还原三栏层次和中央/右侧深色外壳。
- 修复：保留功能结构，重做顶部品牌区、中央播放区、右侧状态仪表、当前/下一视频卡、光轨和动效；未更换语言或框架。
- 现象：深色按钮和安全说明的文字接近背景色，右栏又重复出现顶部已有按钮。原因：隐式 TextBlock 样式覆盖了按钮继承的前景色；早期还原参考图时保留了两套操作入口。
- 修复：按钮内容使用显式前景色绑定，安全说明指定青白文字；删除右栏重复操作，只保留顶部唯一入口。
- 现象：章节总状态未完成、但章节内视频已经全部完成时，点击“开始”仍可能跳入该章。原因：没有未完成视频任务时，开始流程会退回使用章节总完成状态，而该状态也包含测验等非视频任务。
- 修复：课程播放计划只接受真实视频任务证据或明确的视频章节；只因测验未完成的章节不进入待播队列。新增“两条视频均完成、章节仍未完成”回归测试。
- 现象：三栏界面中的学习通网页可视面积过小。原因：旧 F11 只收起右栏且仍保留目录、顶部和底部，网页仍不是完整浏览器尺寸。
- 修复：网页默认缩放为 80%；F11 隐藏所有外围栏和播放信息条并最大化窗口，Esc 或 F11 完整恢复。

## v1.33 课程入口误识别与文档渲染记录 2026 09 11

### ERR-COURSE-RETURN-001 返回课程被当成课程名

- 用户现象：课程区域只显示一个“返回课程”。
- 根因：扫描器把含 courseId 的返回链接视为课程；同一课程合并时又按标题长度选最短项，因此四字导航文字可能覆盖真实课程名。
- 修复：网页脚本排除明确导航标签，C# 合并前再做一次相同语义过滤。
- Debug：新增同时包含“返回课程”和真实课程卡片的最小 DOM 测试，要求只返回真实课程。
- 为什么这样修：只过滤明确的导航文字，不触碰课程地址、章节识别和视频续播，改动范围最小。

### ERR-DOC-RENDER-001 内置渲染器缺少 LibreOffice

- 现象：标准 DOCX 渲染脚本报告找不到 soffice.exe。
- 处理：确认当前电脑安装 Microsoft Word 后，使用隐藏的 Word 实例导出 PDF，再用捆绑的 Poppler 生成逐页 PNG。
- 首轮检查：发现按钮表格左侧裁切、问题步骤跨页不完整、标题样式自带蓝线。
- 修复：缩小表格总宽度并明确各列宽度；在问题步骤前分页；删除 Title 样式和标题段落的边框。
- 复核：三页文档无裁切、重叠或缺字。

## v1.32 发布结构状态（2026-09-11）

### ERR-AUTO-NEXT-010：同章节多个独立 iframe 视频结束后不跳下一条

- 宝贝为何反馈：上午连续播放十几节总体成功，但某个含多个视频的章节在上一视频结束后停住；再次点击“开始”却能刷新到下一视频。
- 现象解释：“开始”会重新扫描全部任务并聚焦待播项；自然结束旧路径则用已结束视频的媒体地址，在父页面反推当前任务块。
- 根因：学习通部分模板把每个视频放在独立子 iframe，父页面只有任务块顺序。子页面媒体地址无法稳定定位父页面中的当前位置；多个子 iframe 内的 `DomIndex` 又都可能是 0。
- 修复：自然结束时先扫描真实视频任务，确定同章节下一条未完成或状态未知视频，把目标 mediaId、iframe 文档地址和 source 交给父页面精确点击；定位证据不足时才使用原相对顺序回退。父子证据合并时继承父任务块序号。
- Debug 原理：点击成功不等于播放器已切换。流程仍采用两阶段确认：先提交明确目标任务点击，再等待媒体身份变化，并核对新播放器真实开始播放。
- 防回归：新增“当前与下一视频分别位于不同 iframe、按钮只在父页面”的 Node DOM 测试；确认只点击下一视频任务块，不回放当前视频，也不跳入测验。
- 状态：源码修复与自动化回归 PASS；真实问题课程复测仍需宝贝实机确认。

- 主要风险：公开仓库误带登录数据或日志；下载包最外层仍堆放运行文件；Release 资产与验证过的版本不一致。
- 防护：公开文件白名单与 `.gitignore` 双重排除；下载包解压后检查顶层条目；Release 只上传本轮重新构建并核对 SHA256 的资产。
- Debug 原理：先证明“公开内容安全”，再证明“源码可构建”，最后证明“下载包可用”，三者不能互相替代。

### ERR-PACKAGE-001：图文下载包脚本在 Windows PowerShell 5.1 解析失败

- 现象：打包脚本报告字符串缺少结束引号，中文文件名在错误输出中变成乱码。
- 根因：Windows PowerShell 5.1 将无 BOM 的 UTF-8 脚本按旧代码页读取，中文字节破坏了解析结果。
- 修复：脚本源码改为 ASCII 安全形式；教学文件通过扩展名定位，中文次级文件夹用 Unicode 码点生成。
- Debug 原理：批处理与 PowerShell 5.1 入口不依赖系统代码页，中文只出现在内容文件和运行时生成结果中。
- 状态：RESOLVED；脚本在 Windows PowerShell 5.1 成功生成下载包，顶层结构核对通过。

## v1.31 界面与反馈 Debug（2026-09-11）

### ERR-ARCHIVE-004：v1.31 MASTER 首次反向还原失败

- 现象：MASTER 本身可读且有 BOM，但还原脚本找不到源码载荷起始标记。
- 根因：打包器已经输出 `V1.31` 标记，复制来的还原脚本仍查找 `V1.30` 标记。
- 修复：还原脚本同步升级为 `V1.31`，静态检查增加起止标记一致性守卫，重新生成 MASTER 后再做真实反向还原。
- Debug 原理：归档不能只检查“文件存在”和“没有乱码”，还必须执行恢复路径；只有载荷哈希匹配并能安全解压，才算自包含。
- 状态：RESOLVED；新 MASTER 实际还原 116 个文件，内嵌载荷 SHA256 校验通过。

### ERR-UI-TEMPLATE-001：按钮按压动画首次编译失败

- 现象：XAML 编译器报告 Trigger 无法定位 `TransformGroup` 内部命名对象。
- 根因：模板名称作用域不能稳定地从 Trigger 直接寻址嵌套 Transform 子项。
- 修复：按压时整体替换按钮边框的 `RenderTransform`，同时包含 95% 缩放和 2 像素下沉。
- Debug 原理：让状态触发器只修改模板中可直接定位的元素属性，减少名称作用域依赖。
- 状态：RESOLVED；Release 编译通过。

### ERR-BUILD-007：设置开关处理器出现 CheckBox 类型歧义

- 现象：编译器报 CS0104，WPF 与 WinForms 都提供 `CheckBox`。
- 根因：项目同时启用了两套 UI 命名空间，未限定类型名。
- 修复：显式使用 `System.Windows.Controls.CheckBox`。
- Debug 原理：在双 UI 技术项目中对冲突类型写完整命名空间，避免编译器猜测。
- 状态：RESOLVED；Release 编译通过。

### TEST-EXPECTATION-001：默认主题变更后旧单元测试失败

- 现象：设置归一化测试仍期待无效主题回退 Dark。
- 根因：产品默认主题已按护眼要求改为 Light，测试仍表达旧需求。
- 修复：只更新对应断言为 Light，没有放宽其他设置校验。
- Debug 原理：先确认失败来自需求变化还是实现退化；本例属于前者，因此同步验收标准。
- 状态：RESOLVED；MSTest 32/32 PASS。

## v1.30 当前状态（2026-09-11）

### ERR-COURSE-COMPLETE-001：找不到下一章就过早显示完成

- 现象：目录漏扫、章节暂时打不开或存在无视频章节时，旧流程可能把“没有下一候选”直接当成课程完成。
- 根因：旧流程只有当前位置之后的一次查找，没有保存本次运行的课程级待播状态，也没有结束前全局复核。
- 修复：增加课程待播清单、已核验集合和无法确认集合；最后重新扫描目录，遗漏候选返回补播，无法确认项阻止完成提示。
- 防回归：MSTest 覆盖课程顺序、起始位置、明确完成、状态未知、测验排除和已核验排除。
- 状态：代码、编译与自动化回归 PASS；真实账号长时间运行待验。

## v1.29 当前状态（2026-09-11）

- 播放功能基线：继承 v1.28。
- MASTER：改为从可读档案重建，不再嵌套乱码正文。
- 真实课程全程无人值守验收：NOT RUN。

### ERR-ARCHIVE-003：修正编码后旧版本正文仍乱码

- 现象：最外层 v1.28 标题可读，但历史版本正文仍乱码。
- 根因：旧版正文已经有损转码；BOM 只能帮助识别当前文件编码，不能逆转丢失字符。
- 修复：从可读的累计变更、错误、测试、Release 与说明文档重建历史区，不再复制损坏的旧 MASTER。
- 防回归：检查 BOM、替换字符、典型乱码串和源码还原结果。
- 状态：v1.29 归档流程修复。

### ERR-AUTO-NEXT-009：当前视频结束后仍不主动切章

- 现象：左侧列表手动点击可以切章，自动流程却停留在原播放器。
- 修复：下一候选由章节视频任务汇总确定，自动流程使用真实目录 DOM 点击，并保留 `toOld` 与上下文导航回退；完成章节不会成为候选。
- 状态未知策略：未知视频视为待核验，宁可播放确认，也不把可能没看的章节跳过。
- 状态：代码与回归 PASS，真实课程待验。

### ERR-AUTO-PLAY-005：切章后仍需人工点击播放

- 根因：仅调用 `video.play()` 重试仍可能受 Chromium 用户手势策略阻止。
- 修复：创建 WebView2 环境时加入 `--autoplay-policy=no-user-gesture-required`，同时保留 4 轮真实状态确认。
- 状态：编译 PASS，真实平台媒体策略待验。

### ERR-VIDEO-LABEL-001：当前视频只显示“播放视频”

- 修复：将当前真实任务映射到章节视频清单，显示 `第 N/M 个视频 · 标题`；模糊标题被丢弃。
- 测验隔离：明确章节测验、测试题、作业、签到和考试占位不进入视频清单。
- 状态：代码与 Node 回归 PASS。

## v1.28 当前状态（2026-09-11）

- Windows Release Build / MSTest：PASS，29/29。
- Node 适配器回归：PASS，45/45。
- Python 静态检查：PASS。
- 真实学习通账号课程：NOT RUN。

### ERR-AUTO-ORDER-001：章节内仍有视频却切章或跳入测试题

- 用户可见现象：一个章节下有视频一、测试题、视频二；视频一结束后没有进入视频二，可能直接切下一章或打开测试题。
- 根因：旧顺序先调用网页原生“下一视频/下一节”，再扫描同章节视频；同时同章节扫描把状态未知的视频排除。
- 修复：同章节视频扫描改为第一优先；按真实 DOM/任务块顺序跳过非视频内容；只排除明确已完成的视频。
- 风险收紧：原生控件只允许明确的“下一视频”、`vjs-next` 或 `next-video` 控件；“下一节/下一任务/测试/作业/考试”全部拒绝。
- iframe 处理：父页面可用当前媒体 ID 找到任务块，再从后续块中挑真实视频，测试块即使带 `ans-job` 也不会入选。
- 防回归：增加“测试夹在两个视频中间”和“拒绝下一节按钮”用例；静态检查固定优先级。
- 状态：代码级 PASS，真实课程待验。

### ERR-ARCHIVE-002：MASTER 在部分 Windows 记事本中显示乱码

- 现象：MASTER 内容打开后中文乱码，Base64 英文区仍可能正常。
- 根因：归档脚本使用 UTF-8 无 BOM；部分 Windows 记事本按系统 ANSI 代码页识别。
- 修复：MASTER 单独改用 UTF-8 with BOM；源码文件和哈希清单继续使用原有 UTF-8 格式。
- 验证：文件头为 `EF BB BF`，还原脚本使用 `utf-8-sig` 可正常读取并校验载荷。

## v1.27 当前状态（2026-09-11）

- Windows Release Build / MSTest：PASS，29/29。
- Node 真实适配器脚本：PASS，43/43。
- Python 静态检查：PASS。
- 真实学习通账号/课程：NOT RUN；未取得另一台测试电脑生成的运行日志。

### ERR-AUTO-NEXT-008：自然结束后不切下一章节

- 用户可见现象：视频看完仍停留在当前章节，程序没有自动进入目录中的下一章。
- 代码根因：`FindNextNavigationCandidate` 与网页回退脚本都只接受 `CompletionKnown && !IsCompleted`。当目录只暴露章节顺序、没有完成状态时，后续项全部被排除。
- 修复范围：新增独立的 `FindNextSequentialNavigationCandidate`，只排除明确已完成项；网页原生回退改为“明确未完成优先、未知其次”。
- 保留边界：“打开未完成章节”仍只接受明确未完成证据，未知状态不会被伪装成未完成。
- 防回归：静态检查分别检查两种语义；Node 新增未知状态的顺序候选用例。
- 状态：代码级 PASS，真实课程待验。

### ERR-AUTO-PLAY-004：自动切换后没有开始播放

- 用户可见现象：新视频已经出现，但播放器保持暂停。
- 代码根因：旧流程只在刚检测到播放器时执行一次 `PlayVideoAsync`，播放器异步初始化稍慢就会失败。
- 修复范围：新增统一的 4 轮有界播放重试，每轮重新读取真实播放器状态，并在播放器可用后恢复倍速。
- 失败行为：不回滚已切换章节；显示一次“章节已打开但自动播放被阻止”的提示，允许直接点网页播放。
- 状态：编译和脚本回归 PASS，浏览器媒体策略分支需真实课程验证。

### ERR-MANUAL-PLAY-002：手动左侧切章后不自动播放

- 用户可见现象：左侧点击有效、章节与播放器都变化，但视频不会开始。
- 代码根因：手动切章成功分支只确认章节和刷新界面，没有播放收尾；整页导航也未跨页面保存播放意图。
- 修复范围：所有手动切章成功路径统一调用 `CompleteManualChapterSwitchAsync`；整页导航保存目标和旧播放器快照，加载后确认目标再播放。
- 状态：代码级 PASS，真实课程待验。

本档案累计保留已解决、待验证和新发现错误。v1.24/v1.25 的遗漏条目已在文件末尾补档；原阶段交付摘要保留，避免历史内容丢失。

## v1.26 当前状态（2026-09-11）

- Windows Release Build：PASS。
- MSTest：29/29 PASS。
- Node 适配器回归：42/42 PASS。
- quick-run 发布：PASS。
- WPF 主窗口与 WebView2 登录页启动：PASS。
- 真实学习通账号/课程：NOT RUN。

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

# ERROR_ARCHIVE

版本：v1.6
日期：2026-09-07
原则：该文件为累计式错误档案。已解决错误不会删除，只更新状态与验证结果。

## ERR-BOOT-001 — 构建窗口一闪而过

- 首次出现：v1.1 Windows 首次构建尝试
- 现象：双击原 BUILD_WINDOWS.bat 后窗口迅速关闭，普通用户无法看到错误信息。
- 根因：原入口没有可靠的停留与诊断输出机制。
- 修复版本：v1.2
- 修复：新增 STEP1_CHECK_AND_BUILD.bat；无论成功失败都停留；自动生成 build_diagnostic.txt。
- 状态：RESOLVED
- 验证：v1.2 Windows 实机已成功保留完整诊断输出。

## ERR-ENV-001 — 系统存在 dotnet host 但没有 .NET SDK

- 首次出现：v1.1/v1.2 Windows 实机诊断
- 现象：where dotnet.exe 成功，但 dotnet --version 返回 “No .NET SDKs were found”。
- 根因：电脑只有 .NET Runtime/Host，没有 .NET 8 SDK。
- 修复版本：环境侧已于 v1.2 后完成。
- 修复：安装 Microsoft .NET 8 SDK x64；诊断脚本改为使用 dotnet --list-sdks 判定 SDK，而不是仅检查 dotnet.exe。
- 状态：RESOLVED
- 实机验证：SDK 8.0.424 已被检测，Restore 成功。

## ERR-SCRIPT-001 — Windows PowerShell 5.1 中文编码导致 build.ps1 ParserError

- 首次出现：v1.1
- 现象：build.ps1 中中文字符串乱码，PowerShell 报 ParserError / InvalidLeftHandSide。
- 根因：Windows PowerShell 5.1 对无 BOM UTF-8 脚本兼容性不足。
- 修复版本：v1.2
- 修复：构建脚本改用 ASCII 安全提示文本，并以 UTF-8 BOM 保存。
- 状态：RESOLVED
- 验证：v1.2 实机已经成功执行 Restore 和 Build 命令，说明脚本解析阶段已通过。

## ERR-BUILD-001 — CS0104 Application 类型歧义

- 首次出现：v1.2 Windows 首次真实 Build
- 原始错误：App.xaml.cs(8,28): error CS0104: “Application”是“System.Windows.Forms.Application”和“System.Windows.Application”之间的不明确的引用。
- 根因：工程同时启用 WPF 与 Windows Forms（托盘 NotifyIcon 需要 WinForms），而 App.xaml.cs 使用未限定的 Application 类型名。
- 影响：Release Build 在 C# 编译阶段停止。
- 修复版本：v1.3
- 修复内容：
  1. App 基类改为 System.Windows.Application。
  2. 所有 WPF MessageBox.Show 改为 System.Windows.MessageBox.Show。
  3. ThemeService 中 Application.Current 改为 System.Windows.Application.Current。
  4. WPF Color 转换显式使用 System.Windows.Media.Color。
  5. static_check.py 新增 WPF + WinForms 类型歧义回归扫描，防止同类错误重新出现。
- 状态：RESOLVED
- Windows 验证：v1.4 真实 Build 日志中已不再出现 Application / MessageBox / Application.Current 的 CS0104，因此该错误正式关闭。

## 错误归档规则

1. 每个错误分配稳定 ID。
2. 已解决错误永久保留，不删除。
3. 必须记录：首次出现版本、现象、根因、影响、修复版本、验证状态。
4. 只有真实验证通过后才可标记 RESOLVED。
5. 仅完成源码修改但尚未实机验证时标记 FIXED_IN_SOURCE / AWAITING_VALIDATION。
6. 后续 Master 必须累计包含本错误档案摘要。


## ERR-DELIVERY-001 — Windows 侧中文文件名乱码 / 构建日志难以定位

- 首次出现：v1.3 Windows 二次真实 Build
- 现象：构建入口最终显示 BUILD_FAILED，但用户未能找到预期的 `build_diagnostic.txt`；解压目录中同时出现多个乱码文件名。
- 已确认事实：v1.3 构建入口可以检测工程目录和 .NET 8 SDK，并已实际启动 `scripts\build.ps1`；因此该现象与 SDK 缺失无关。
- 初步根因：交付包中存在中文文件名，Windows 解压/编码链路对部分非 ASCII 文件名显示异常；旧 BAT 还使用 UTF-8 BOM，可能导致部分 CMD 环境不能正确执行首行 `@echo off`，从而产生大量命令回显。
- 影响：非技术用户难以辨认日志文件，真实 Build 失败原因无法可靠回传。
- 修复版本：v1.4
- 修复内容：
  1. 新增纯 ASCII、无 BOM 的 `BUILD_V14.bat`。
  2. `STEP1_CHECK_AND_BUILD.bat` 同步替换为纯 ASCII、无 BOM 版本。
  3. 日志固定使用纯 ASCII 文件名 `BUILD_LOG.txt`。
  4. Build 失败时自动使用 Notepad 打开 `BUILD_LOG.txt`。
  5. Master 增加纯 ASCII 别名 `MASTER_v1.4.txt`。
- 状态：RESOLVED
- Windows 验证：v1.4 构建失败后成功生成并回传 `BUILD_LOG.txt`，说明 ASCII/no-BOM 构建入口和固定日志名方案有效。



## ERR-BUILD-002 — System.IO 核心类型未显式可见，触发 48 个 CS0103

- 首次出现：v1.4 Windows 真实 Build
- 真实证据：`docs/build_logs/BUILD_LOG_v1.4_failed.txt`
- 现象：`File`、`Path`、`Directory` 在 AppPaths、FileLogger、JsonFile、SessionRecoveryService、LogWindow、DiagnosticService、MainWindow 等多个文件中均报 CS0103。
- 本轮数量：48 个 CS0103；另有 1 个 CS8619 为该类型解析失败引发的级联 nullable warning。
- 根因：工程虽然启用了 `<ImplicitUsings>enable</ImplicitUsings>`，但当前 Windows Desktop / WPF + WinForms 实际编译环境没有为这些源码提供可用的 `System.IO` 简写类型。源码错误地依赖了隐式 using 行为。
- 修复版本：v1.5
- 修复内容：
  1. 新增 `src/ChaoxingLearningAssistant/GlobalUsings.cs`。
  2. 显式定义 `File = System.IO.File`、`Path = System.IO.Path`、`Directory = System.IO.Directory` 三个全局别名。
  3. static_check.py 增加三个 System.IO 别名存在性回归检查。
  4. 构建前增加 `dotnet clean`，避免旧 obj/bin 影响验证。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION

## ERR-BUILD-003 — ColorConverter 在 System.Drawing 与 WPF Media 之间歧义

- 首次出现：v1.4 Windows 真实 Build
- 原始错误：ThemeService.cs(58,61) CS0104，`ColorConverter` 在 `System.Drawing.ColorConverter` 与 `System.Windows.Media.ColorConverter` 之间不明确。
- 根因：项目同时启用 WPF 与 Windows Forms；WinForms 间接引入 System.Drawing，而 ThemeService 使用了未完全限定的 `ColorConverter`。
- 修复版本：v1.5
- 修复内容：改为 `System.Windows.Media.ColorConverter.ConvertFromString(...)`，并加入静态回归扫描。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION

## WARN-BUILD-001 — WFAC010：混合 WPF/WinForms 项目的 DPI manifest 提示

- 首次出现：v1.4 Windows 真实 Build
- 现象：WinForms analyzer 建议从 app.manifest 移除高 DPI 声明，改用 WinForms 的 `ApplicationHighDpiMode`。
- 项目背景：主体 UI 为 WPF，WinForms 仅用于系统托盘 `NotifyIcon`；现有 manifest 的 PerMonitorV2 DPI 配置属于 WPF 主窗口的明确设计。
- v1.5 处理：保留 WPF manifest DPI 配置，仅针对该混合框架 analyzer 提示增加 `WFAC010` 定向抑制；不关闭其他 warning。
- 状态：HANDLED / AWAITING_WINDOWS_VALIDATION

## v1.5 归档证据文件

- `docs/build_logs/BUILD_LOG_v1.2_failed.txt`：首次真实 C# 编译错误证据（ERR-BUILD-001）。
- `docs/build_logs/BUILD_LOG_v1.4_failed.txt`：本轮 49 error / 2 warning 原始构建日志（ERR-BUILD-002、ERR-BUILD-003、WARN-BUILD-001）。



## ERR-SCRIPT-002 — `dotnet clean` 使用不支持的 `--no-restore` 参数

- 首次出现：v1.5 Windows 真实构建。
- 原始证据：`docs/build_logs/BUILD_LOG_v1.5_failed.txt`。
- 现场状态：项目目录、.NET 8 SDK 检测均 PASS；Restore 主项目与测试项目均 PASS。
- 原始错误：`MSBUILD : error MSB1001: Unknown switch.`，并明确指出 `Switch: --no-restore`。
- 根因：v1.5 为排除旧 `bin/obj` 干扰新增了 `dotnet clean ... --no-restore`，但 `dotnet clean` 不支持该参数；这是构建脚本错误，不是 C# 源码错误。
- 影响：构建流程在 Clean 阶段终止，尚未进入 v1.5 的 C# Build，因此本轮不能验证 ERR-BUILD-002 / ERR-BUILD-003 的源码修复。
- 修复版本：v1.6。
- 修复内容：
  1. 删除独立 Clean 阶段，恢复更稳定的 `Restore -> Build -> Test -> Publish` 流程。
  2. 保留 `Build --no-restore` 与 `Test --no-build`，两者属于对应命令支持的有效参数。
  3. `artifacts` 仍在 Publish 前由脚本显式删除并重建，不影响发布物洁净性。
  4. `static_check.py` 新增针对 `Clean + --no-restore` 组合的回归检查。
  5. 新增 `BUILD_V16.bat`，继续保持 ASCII/no-BOM、英文 CLI 输出、失败自动打开 `BUILD_LOG.txt`。
- 状态：FIXED_IN_SCRIPT / AWAITING_WINDOWS_VALIDATION。

## v1.6 错误状态说明

- ERR-BUILD-002：仍为 `FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION`。v1.5 因脚本在 Clean 阶段提前失败，未进入 C# Build，不能据此宣布通过。
- ERR-BUILD-003：仍为 `FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION`，原因同上。
- WARN-BUILD-001：仍为 `HANDLED / AWAITING_WINDOWS_VALIDATION`。
- ERR-SCRIPT-002：v1.6 已修，等待下一次 Windows 构建验证。


## v1.6 Windows real-build closure

Evidence from the v1.6 Windows build:
- Release Build: PASS
- Warnings: 0
- Errors: 0
- Automated tests: 4/4 PASS
- Self-contained publish: INTERRUPTED by Ctrl+C before completion

Status updates:
- ERR-BUILD-002 (System.IO File/Path/Directory resolution): RESOLVED
- ERR-BUILD-003 (ColorConverter ambiguity): RESOLVED
- WARN-BUILD-001 (build warnings): RESOLVED
- ERR-SCRIPT-002 (invalid Clean switch): RESOLVED

## EVT-PUBLISH-001 — Self-contained publish interrupted

- Type: execution event, not a source-code defect
- First observed: v1.6 Windows run
- Evidence: Build and Test completed successfully; log reached `== Publish self-contained win-x64 ==` and then ended with `^C`.
- Root cause: publish process was manually interrupted before completion.
- Consequence: no conclusion can be made about the self-contained packaging result from v1.6.
- v1.7 mitigation:
  1. Create a quick-run framework-dependent EXE before self-contained packaging.
  2. Explicitly restore `win-x64` runtime packs before self-contained publish.
  3. Publish self-contained with `--no-restore` after runtime-pack restore.
  4. Build launcher warns not to press Ctrl+C during the first runtime-pack download.
  5. If later packaging fails, the launcher automatically opens `artifacts\quick-run` when the quick-run EXE exists.
- Status: MITIGATED / AWAITING_WINDOWS_VALIDATION


## ERR-BUILD-004 — `Color` 在 System.Drawing 与 System.Windows.Media 之间歧义

- 首次出现：v1.8 Windows 真实 Build。
- 原始证据：`docs/build_logs/BUILD_LOG_v1.8_failed_ERR-BUILD-004.txt`。
- 环境状态：
  - Project folder：PASS
  - .NET 8 SDK：PASS（8.0.424）
  - Restore：PASS
  - Build：FAIL
- 原始错误：
  - `ThemeService.cs(79,34): error CS0104`
  - `'Color' is an ambiguous reference between 'System.Drawing.Color' and 'System.Windows.Media.Color'`
- 编译统计：
  - 0 Warning
  - 1 Error
- 根因：
  v1.8 UI 重构修改 ThemeService 时，虽然 `ColorConverter` 已经显式限定为
  `System.Windows.Media.ColorConverter`，但转换结果的强制类型转换仍写成裸 `(Color)`。
  由于项目同时启用 `<UseWPF>true</UseWPF>` 与 `<UseWindowsForms>true</UseWindowsForms>`，
  `Color` 同时可指向 `System.Drawing.Color` 与 `System.Windows.Media.Color`，触发 CS0104。
- v1.9 修复：
  1. 将 `(Color)` 改为 `(System.Windows.Media.Color)`。
  2. 保留显式 `System.Windows.Media.ColorConverter`。
  3. `static_check.py` 增加裸 `(Color)` 强制转换回归扫描。
  4. 同类源码扫描确认当前工程没有其他裸 `Color` 强制转换。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION。

## v1.9 错误档案累计状态更新

- ERR-BOOT-001：RESOLVED
- ERR-ENV-001：RESOLVED
- ERR-SCRIPT-001：RESOLVED
- ERR-BUILD-001：RESOLVED
- ERR-DELIVERY-001：RESOLVED
- ERR-BUILD-002：RESOLVED
- ERR-BUILD-003：RESOLVED
- WARN-BUILD-001：RESOLVED
- ERR-SCRIPT-002：RESOLVED
- EVT-PUBLISH-001：MITIGATED
- UI-REV-001：FIXED_IN_SOURCE / AWAITING_WINDOWS_VISUAL_VALIDATION
- UI-REV-002：FIXED_IN_SOURCE / AWAITING_WINDOWS_VISUAL_VALIDATION
- ERR-BUILD-004：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION


## v1.9 Windows runtime acceptance evidence

The v1.9 executable was launched successfully on the user's Windows machine.
Runtime screenshot evidence confirmed:
- WPF main window rendered.
- v1.8/v1.9 high-tech dark theme rendered.
- WebView2 initialized.
- Chaoxing login page loaded inside WebView2.
- Login-page recognition triggered the safe paused state.

This is runtime acceptance evidence. It does not replace a raw build log, but it confirms the built executable can launch and reach the real Chaoxing login page.

## UI-REV-007 — Embedded course page horizontal navigation is inconvenient

- First observed: v1.9 Windows real runtime after login.
- Symptom: the embedded Chaoxing page can exceed the available center viewport width; horizontal content is difficult to inspect because the native horizontal scrollbar is thin and usually located at the bottom of the page.
- v2.0 change:
  1. Browser toolbar adds explicit horizontal-left and horizontal-right controls.
  2. Controls execute smooth horizontal movement against the largest horizontally scrollable document element.
  3. Same-origin iframe documents are also inspected.
  4. Shift + mouse wheel is mapped to horizontal movement without changing ordinary wheel behavior.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_RUNTIME_VALIDATION.

## UI-REV-008 — Missing browser focus fullscreen

- First observed: v1.9 Windows real runtime.
- Symptom: three-column layout is useful for monitoring, but complex course pages sometimes need substantially more width.
- v2.0 change:
  1. Add browser focus fullscreen button.
  2. F11 enters/exits the mode.
  3. Esc exits the mode.
  4. In focus fullscreen, top command deck, left navigation, right telemetry and footer are hidden.
  5. Browser workspace expands to the full application window and the window becomes borderless/maximized.
  6. Browser toolbar remains visible so back/reload/horizontal movement/exit-fullscreen remain available.
  7. Original column sizes and window state are restored on exit.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_RUNTIME_VALIDATION.

## UI-REV-009 — Playback-time metric is clipped

- First observed: v1.9 Windows real runtime screenshot.
- Symptom: the playback-time metric shares a narrow two-column row with playback rate, causing the end of the time string to be clipped.
- v2.0 change:
  1. Playback time receives a full-width metric card.
  2. Playback rate moves to a compact header badge.
  3. Time uses Consolas for stable numeric width.
  4. A DownOnly Viewbox can shrink long time strings instead of clipping them.
  5. Full time text is also exposed as a tooltip.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_VISUAL_VALIDATION.


## VER-MGMT-001 — Premature major-version jump

- First observed: after v1.9.
- Symptom: the browser usability update was labeled `v2.0`.
- Root cause: version number was advanced based on feature significance rather than the project's established release semantics.
- Decision:
  - current development line remains 1.x;
  - the correct successor to v1.9 is v1.10;
  - the temporary v2.0 label is not an official release baseline.
- Functional impact: none.
- Source impact: none.
- Corrected in: v1.10.
- Status: RESOLVED.


## ERR-BUILD-005 — `KeyEventArgs` 在 WinForms 与 WPF 之间歧义

- 首次出现：v1.10 Windows 真实 Build。
- 原始证据：`docs/build_logs/BUILD_LOG_v1.10_failed_ERR-BUILD-005.txt`。
- 环境状态：
  - Project folder：PASS
  - .NET 8 SDK：PASS（8.0.424）
  - Restore：PASS
  - Build：FAIL
- 原始错误：
  - `MainWindow.xaml.cs(1014,59): error CS0104`
  - `'KeyEventArgs' is an ambiguous reference between 'System.Windows.Forms.KeyEventArgs' and 'System.Windows.Input.KeyEventArgs'`
- 编译统计：
  - 0 Warning
  - 1 Error
- 根因：
  v1.10 新增 F11 / Esc 全屏快捷键时，事件签名使用了裸 `KeyEventArgs`。
  项目同时启用了 WPF 与 WinForms（托盘 NotifyIcon），因此编译器无法判定应使用哪一个 KeyEventArgs。
- v1.11 修复：
  1. 将事件签名显式改为 `System.Windows.Input.KeyEventArgs`。
  2. 新增同类输入事件类型回归扫描。
  3. 扫描当前 C# 源码，禁止裸 `KeyEventArgs` 与裸 `MouseEventArgs`。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION。


## ERR-RUNTIME-001 — Course progress ProgressBar binds TwoWay to a read-only property

- First observed: real Windows runtime log dated 2026-09-07.
- Raw evidence: `docs/runtime_logs/2026-09-07_v1.11_runtime_ERR-RUNTIME-001.log`.
- Trigger sequence:
  1. WebView2 initialized successfully.
  2. User reached a real Chaoxing course page.
  3. Course scanner found 1 course candidate.
  4. The WPF course list attempted to render.
  5. `XamlParseException` was raised repeatedly during layout/virtualization.
- Exception:
  - `CourseItem.ProgressPercent` is read-only.
  - `ProgressBar.Value="{Binding ProgressPercent}"` used the dependency property's default binding mode.
  - WPF attempted a TwoWay / OneWayToSource path and rejected the read-only source property.
- Error count in the supplied runtime log:
  - 44 `APP-UNHANDLED` entries.
  - All 44 are the same binding root cause, not 44 independent defects.
- v1.12 fix:
  1. Course progress bar changed to `Value="{Binding ProgressPercent, Mode=OneWay}"`.
  2. Telemetry progress bar is also explicitly `Mode=OneWay` for consistency.
  3. Static regression guard added: ProgressPercent bindings on ProgressBar must explicitly be OneWay.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_RUNTIME_VALIDATION.

## VER-MGMT-002 — BUILD_LOG header remained at v1.8

- Evidence: later build logs could show the correct project folder/version while line 2 still said `Build Log v1.8`.
- Root cause: the launcher banner was version-bumped, but the separate log-header string was not.
- v1.12 fix:
  - `BUILD_V1_12.bat` now writes `Chaoxing Learning Assistant - Build Log v1.12`.
  - Regression guard added.
- Functional impact: none; diagnostic labeling only.
- Status: RESOLVED_IN_SOURCE / AWAITING_NEXT_LOG_CONFIRMATION.

## OBS-ADAPTER-001 — Current Chaoxing page produced 0 chapter/video candidates

- Runtime evidence:
  - Course scanner later found 1 course candidate.
  - Chapter scanner still reported 0 chapter candidates and 0 explicit videos on the observed pages.
- Interpretation:
  - This is not the cause of ERR-RUNTIME-001.
  - It is retained as an adapter observation for the next functional-validation stage.
  - Do not classify as a confirmed selector defect until the UI binding crash is removed and the same course page is re-tested.
- Status: OBSERVED / PENDING_RETEST.


## v1.13 — 本轮新增记录（2026-09-08）

本轮输入仅有 v1.12 源码 ZIP 和“点击视频后连续报错”的描述，没有新的 v1.12 运行日志。
源码 ZIP 内的 v1.11 历史日志含同一 ProgressPercent 只读绑定异常 44 次，旧日志不能证明 v1.13 已通过。

### ERR-RUNTIME-002 — v1.12 遗漏 Run.Text 的只读进度绑定

证据：MainWindow.xaml 中 Run.Text 绑定 CourseItem.ProgressPercent 没有 Mode；属性只有 getter。
v1.12 的回归检查只覆盖 ProgressBar，漏掉了同一属性的 Run.Text。
修复：课程卡片全部绑定的 Run.Text 显式 Mode=OneWay；保留进度条的 OneWay。
更正旧归因：历史日志定位到 ProgressPercent，不能据此把全部问题只归给 ProgressBar。v1.12 只修改进度条不足以排除同一异常。
验证：XML 属性级检查通过；临时还原旧绑定会被明确报 ERR-RUNTIME-002，已恢复正确源码。
新增真实 WPF 模板布局测试，等待 Windows 执行。
状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_RUNTIME_VALIDATION。

### ERR-RUNTIME-003 — 错误弹窗放大

旧全局处理器每次都 Show MessageBox，再标记 Handled 继续运行，损坏的布局会再次报同样错误。
NotifyUser 也会从网页处理路径创建模态提示。微软文档指出 WebView2 回调里不支持嵌套模态消息循环。
修复：普通错误用底部提示；20 秒内同内容只通知一次。全局致命错误先置一次性标记、停止后台任务，再延后只提示一次并退出，保留恢复快照。
状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_RUNTIME_VALIDATION。

### ERR-RUNTIME-004 — 页面切换时的异步竞争

DispatcherTimer 的异步轮询未防重入；旧导航完成/重试能干扰已切换的新页面。
修复：轮询闩锁、NavigationId 校验、页面代次校验、重试与页面/窗口生命周期绑定；OperationCanceled 不进入网络重试；导航回调异常就地记录。
状态：SOURCE_REVIEWED / AWAITING_WINDOWS_NAVIGATION_VALIDATION。

### ERR-PLAYER-001 / ERR-PLAYER-002

修复 ended 状态每次轮询重复处理；去掉接近结尾 0.4 秒即视为结束的判断。
修复 PlayVideoAsync 内返回 Promise 却按 int 解析的协议错配。JS 同步回传提交结果，C# 通过真实播放器状态确认成功，重复点击命令受保护。
新增预加载期间轮询暂停，补充对延迟 iframe 播放器的处理；这一机制仍有轮询延迟边界。
状态：FIXED_IN_SOURCE；JS 场景 PASS；真实 WebView2 待验证。

### ERR-ADAPTER-001 — “未完成”包含“完成”

旧正则包含裸“完成”，导致未完成也匹配。
新规则先排除否定/待完成状态，再识别明确完成词；完成度 0% 不认为已完成。
状态：SOURCE_JS_REGRESSION_PASS；真实平台 DOM 待验证。

### BUILD-UX-001

默认快速构建；完整便携包/安装包放到 BUILD_FULL.bat。START 用当前版本成功标记启动。
根目录旧启动文件作为兼容别名转到 v1.13。当前构建不删除整个 artifacts；新打包临时目录排除个人数据目录。
状态：SOURCE_AND_ENCODING_CHECK_PASS；Windows BAT/PowerShell 实际执行待验证。

参考：
- [WPF Run.Text 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.run.text?view=windowsdesktop-10.0)
- [WebView2 线程模型与重入限制](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model)
- [WebView2 JavaScript 返回值说明](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/javascript)


## v1.15 runtime-feedback archive

### ERR-UI-STATUS-001 — 播放状态与辅助状态混用
- 现象：网页视频正在播放，但未开启/暂停辅助时主状态显示“暂停”，容易理解为视频暂停。
- 修复：新增 PlaybackStatusText / AutomationStatusText，主卡显示真实播放器，流程状态降级为辅助信息。

### ERR-UI-MINIMIZE-001 — 最小化后任务栏入口消失
- 根因：MainWindow.OnStateChanged 在 WindowState.Minimized 时主动 `Hide()`。
- 修复：取消 Hide，保持 ShowInTaskbar=true；关闭窗口恢复正常关闭语义。

### ERR-ADAPTER-002 — 左侧章节/视频目录仍为空
- 修复：扩大目录 selector，增强 ChapterId/标题提取，首次为 0 自动重试，运行中限频补扫。

### ERR-NEXT-001 — 视频结束提示未识别到后续视频
- 根因：v1.14 下一节完全依赖 `_vm.Chapters`；目录扫描为 0 时直接结束。
- 修复：新增网页原生目录顺序 fallback；只有列表与原生 DOM 都失败后才提示暂未定位下一章节。

### UI-THEME-002 — 深色设置页部分文字对比度不足
- 修复：提高辅助文字亮度并显式设置设置窗口/基础文本控件前景色。

## v1.16 runtime-feedback archive

### UX-WATCH-001 — 观看模式需要真正全屏，操作成本高
- 反馈：观看操作“反人类”，希望不必进入全屏。
- 修复：F11 改为非全屏宽屏观看；保留 Windows 标题栏、任务栏和章节列表，仅隐藏右侧次要遥测。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_VALIDATION。

### UX-NAV-001 — 点击章节只选中，不直接打开/定位未完成
- 根因：v1.15 章节打开绑定在 `MouseDoubleClick`；单击只更新 SelectedChapter 文本。
- 修复：改为 `PreviewMouseLeftButtonUp` 单击打开；新增“定位未完成”；章节打开后尝试聚焦页内明确未完成视频任务点。
- 状态：FIXED_IN_SOURCE / AWAITING_REAL_DOM_VALIDATION。

### ERR-ADAPTER-002 — 章节标题混入状态/任务点文字，完成状态置信度不足
- 根因：旧扫描可能使用整个容器文本作为标题/状态证据，标题容易带上“待完成任务点/视频/已完成”等噪声；未知状态又与未完成共用 `IsCompleted=false`。
- 修复：标题单独清洗；新增 `CompletionKnown`；UI 显示“状态待同步”；自动候选优先明确未完成。
- 状态：FIXED_IN_SOURCE / NODE_REGRESSION_PASS。

### ERR-COURSE-001 — 课程名称/0-0 进度显示容易误导
- 修复：课程名称优先卡片/课程名节点并过滤通用操作文案；当前页面识别 `CourseTitle`；未同步总数时显示“进入课程后同步视频进度”。
- 状态：FIXED_IN_SOURCE / AWAITING_REAL_PAGE_VALIDATION。

### ERR-PLAYER-003 — ended 后可能重新播放刚看完的视频
- 风险路径：下一候选仍解析成当前 ChapterId，或章节点击后播放器仍指向刚结束的同一媒体源，随后 `PlayVideoAsync` 会把当前视频重新从头播放。
- 修复：网页下一候选排除当前 ChapterId；主流程二次同章节校验；保存 ended `currentSrc/src`，切换后同源触发 `CX-AUTO-NEXT-REPLAY-GUARD` 并继续查找后续候选。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_WEBVIEW2_VALIDATION。


## v1.17 Windows build/test evidence archive

### ERR-TEST-001 — Course UI runtime regression test stale after v1.16 UI change
- Evidence: `docs/build_logs/BUILD_LOG_v1.16_windows_test_failure.txt`.
- Windows result: Release Build succeeded; 0 Error / 2 Warning. MSTest 10/11 PASS.
- Failure: `RealCourseTemplate_RendersReadOnlyProgress_AndUpdatesCounts` threw `InvalidOperationException: Sequence contains no matching element`.
- Root cause: test searched a `Run.Text` binding whose path was `ProgressPercent`; v1.16 shipped template uses `TextBlock.Text -> ProgressSummary` and `ProgressBar.Value -> ProgressPercent`.
- Fix: keep testing the embedded real MainWindow.xaml template, but find and validate the current two bindings; verify 3/8 -> 38% and 4/8 -> 50%.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_MSTEST.

### WARN-BUILD-002 — CS4014 Dispatcher.BeginInvoke
- Fix: assign the awaitable DispatcherOperation to discard (`_ = ...`). Runtime scheduling behavior unchanged.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_BUILD.

### WARN-BUILD-003 — CS8602 nullable adapter dereference
- Fix: capture `_adapter` into a local non-null reference before async fallback and use that local for `FindNextChapterCandidateAsync`. Business flow unchanged.
- Status: FIXED_IN_SOURCE / AWAITING_WINDOWS_BUILD.


## v1.18 runtime-feedback archive

### ERR-SYNC-001 — 左侧章节推进但播放器实际未切换
- 现象：左侧章节自行跳动或点击后立即切到目标，右侧章节文字也随之变化，但内嵌网页视频仍是原视频。
- 根因 1：刷新目录无法从 URL 映射当前章节时，旧代码会把第一条未完成候选直接写成 `SelectedChapter`。
- 根因 2：手动章节点击在验证网页播放器切换之前就更新左侧选中与右侧文字。
- 根因 3：`OpenChapterAsync` 的成功只表示 DOM click 被提交，不能证明媒体已经改变。
- 修复：引入播放器真实身份并实施“两阶段提交”——先请求网页切换，再等待 `MediaId/currentSrc/documentUrl` 身份变化；只有确认变化后才更新 `SelectedChapter` 和 `CurrentChapterText`。
- 失败行为：播放器没变时记录 NOOP/TIMEOUT，左/右 UI 保持原状态。
- 状态：FIXED_IN_SOURCE / STATIC_PASS / AWAITING_WINDOWS_REAL_DOM_VALIDATION。

### ERR-ADAPTER-004 — 任务点/状态噪声仍进入章节目录
- 现象：左侧章节中继续出现视频、资料、状态待同步、未完成等并非课程章节标题的内容。
- 根因：章节扫描候选范围与任务点块存在重叠，UI 又额外渲染 TaskTypeText / StatusText。
- 修复：目录扫描改为 catalog-first；只有真实任务点 class/data 标记时才排除，避免误杀正常章节；左侧模板只显示清洗后的 `DisplayTitle`。
- Node 回归：新增“task-point blocks are not promoted into chapter library”，27/27 PASS。
- 状态：FIXED_IN_SOURCE / NODE_REGRESSION_PASS / AWAITING_REAL_DOM_VALIDATION。

### ERR-SYNC-002 — 右侧章节名与实际播放器不一致
- 根因：旧 `CurrentVideoText` 来源于 `SelectedChapter.DisplayTitle`，属于目录 UI 状态而非播放器证据。
- 修复：右侧拆成 `CurrentChapterText`（已确认章节）与 `CurrentVideoText`（播放器实际标题）；没有硬证据时显示待确认/无标题，不再猜测。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_REAL_DOM_VALIDATION。

## v1.21 runtime-feedback archive

### ERR-NEXT-002 — 自动续播停在“播放器没变化”的提示，而不是继续真实跳转
- 现象：v1.20 能正确识别目录高亮变化不等于播放器变化，但在部分模板里最终只提示“播放器身份始终没有变化”，学习通实际视频仍停在原条目。
- 根因 1：原生 `toOld(...)` 重试只转发前三个参数，而学习通常见 onclick 还包含第 4 参数；少参调用可能无法完成真实章节导航。
- 根因 2：旧 synthetic studentstudy URL 为避免登录回退被禁止强跳，但缺少一个“保留当前已登录页面全部 query 参数、只换 chapterId”的安全真实导航路径。
- 根因 3：播放器原生 next 控件搜索范围偏窄，只看 video 祖先，可能漏掉 iframe 外层/页面底部的真实下一节按钮。
- 修复：v1.21 完整转发 toOld 参数；新增上下文保持型 studentstudy 导航；全局 next 搜索；失败后继续其他真实导航候选。
- 状态：FIXED_IN_SOURCE / STATIC_PASS / NODE_36_OF_36_PASS / AWAITING_WINDOWS_REAL_COURSE_VALIDATION。


## v1.22 runtime-feedback archive

### ERR-SYNC-003 — 进入视频后课程/章节/视频三层信息无法自动对齐
- 现象：右侧可出现“学生学习页面 / 章节待确认 / 播放器已检测”，与真实课程页面不一致。
- 根因：课程通用 title/旧课程兜底过宽；章节与视频任务未分层；播放器缺少稳定任务级映射。
- 修复：新增 VideoTaskItem 与播放器任务身份；按 courseId 约束课程标题；播放器 -> 视频任务 -> 章节反向绑定。
- 状态：FIXED_IN_SOURCE / STATIC_PASS / NODE_39_OF_39_PASS / AWAITING_WINDOWS_REAL_COURSE_VALIDATION。

### ERR-SYNC-004 — 目录 active 与播放器变化可能组合成错误章节确认
- 现象：页面目录先高亮时，若其它播放器证据恰好变化，旧逻辑可能把目标章节确认到错误视频。
- 根因：`PlayerMatchesTargetChapterAsync` 允许目录 active 作为最终弱证据。
- 修复：active 只用于重新扫描；最终确认必须由 chapterId/document URL 或重新扫描后的真实视频任务映射闭环。
- 状态：FIXED_IN_SOURCE / AWAITING_WINDOWS_WEBVIEW2_VALIDATION。

### ERR-ADAPTER-005 — FindNextChapterCandidate 内嵌 JS 重复 const 声明
- 现象：下一章节候选脚本存在重复 `const id = getId(el)`，真实执行会 SyntaxError。
- 修复：删除重复声明；Node 直接解析该方法脚本继续通过。
- 状态：RESOLVED_IN_SOURCE / NODE_REGRESSION_PASS。


## v1.23 self-audit fix archive

### ERR-NEXT-003 — 父页面没有 `<video>` 时漏掉真正的“下一节”按钮
- 现象：真实视频在子 iframe，下一节在父页面；旧脚本因父页面 `videos.length == 0` 直接返回。
- 修复：识别明确播放器 iframe；父页面即使没有 `<video>` 也继续搜索播放器附近与明确 next 控件；文本型模糊 next 限制在播放器附近。
- 状态：FIXED_IN_SOURCE / NODE_REGRESSION_PASS / AWAITING_WINDOWS_REAL_COURSE_VALIDATION。

### ERR-SYNC-005 — 去重真实 iframe video 时丢失父任务章节/完成状态
- 现象：父任务块有 chapterId/标题/完成状态，子 iframe 只有真实 media；旧去重保留真实 video 但把父信息一起丢弃。
- 修复：新增 `VideoTaskEvidence`，先合并父任务证据，再去重。
- 状态：FIXED_IN_SOURCE / MSTEST_SOURCE_ADDED / AWAITING_WINDOWS_MSTEST_AND_WEBVIEW2。

### ERR-SYNC-006 — 标题/时长延迟加载制造“播放器已切换”假象
- 现象：同一视频 duration 从 0 变正常值、VideoTitle 晚到时，旧 `PlayerIdentity` 字符串变化。
- 修复：新增 `PlayerMediaEvidence.StableIdentity/IsSameMedia`；稳定身份只使用 source/mediaId/document/dom/task fallback，不依赖可变标题/时长。
- 状态：FIXED_IN_SOURCE / MSTEST_SOURCE_ADDED / AWAITING_WINDOWS_MSTEST。

### ERR-END-002 — 2x 等高倍速下 1.5 秒轮询可能错过 ended
- 现象：上一轮询距离结尾超过 1.25 秒，平台又快速清掉 ended 并重置 currentTime，旧补判漏掉自然结束。
- 修复：新增 `PlaybackEndDetector`，按轮询间隔×倍速动态计算容错；同时要求末端或回卷证据，避免普通靠近结尾暂停误报。
- 状态：FIXED_IN_SOURCE / MSTEST_SOURCE_ADDED / AWAITING_WINDOWS_REAL_PLAYER_VALIDATION。

### ERR-PROGRESS-002 — CompletionKnown=false 被部分自动路径当成未完成
- 现象：“打开未完成”、同章节推进、下一章节候选可能把状态未知任务当成未完成，存在跳到已看视频风险。
- 修复：相关自动未完成路径只接受明确 `CompletionKnown && !IsCompleted` / `completion === false`。未知状态保留未知，不自动冒充。
- 状态：FIXED_IN_SOURCE / NODE_REGRESSION_PASS。


## v1.24 遗漏错误补档（2026-09-10）

### ERR-STATS-001 — 暂停、休眠和阻塞时间被计入播放统计

- 现象：统计使用开始到结束的墙钟差，未真实播放的时间也可能累计。
- 根因：旧统计没有按播放器观察区间计时。
- 修复：引入 `PlaybackWatchTracker`，只累计连续观察到的真实播放区间；超过 15 秒的采样间隔不累计。
- v1.26 验证：对应 Windows 单元测试 PASS。
- 状态：RESOLVED_BY_V1.26_TEST；真实长时间课程观察仍待执行。

### ERR-STATS-002 — 切换视频后统计记录串片

- 现象：上一视频尚未结算时切换页面或媒体，多条视频可能合并为一条记录。
- 根因：媒体变化、导航、停止和退出路径没有统一结束活动统计。
- 修复：各生命周期路径先调用 `FinishActiveStat`，再建立新媒体统计。
- v1.26 验证：相关源码和测试构建通过。
- 状态：FIXED_AND_BUILD_VERIFIED；真实课程切换待观察。

### ERR-DIAG-001 — 诊断包播放器证据不足

- 现象：旧诊断仅保留部分状态，无法判断真实播放器是否变化。
- 修复：保存完整 `PlayerSnapshot`，包含 source、mediaId、taskKey、chapterId、时间和时长等字段。
- 状态：FIXED_IN_SOURCE / BUILD_VERIFIED。

### ERR-CACHE-001 — 短暂扫描失败清空课程列表

- 现象：网页短暂未返回课程时，最近课程会从界面消失。
- 修复：增加 30 天课程缓存；空扫描保留最近有效列表。
- 状态：FIXED_IN_SOURCE / WINDOWS_TEST_PASS。

### ERR-JSON-001 — 设置或会话主文件损坏时恢复能力不足

- 修复：有效主文件保存前生成 `.bak`，主文件损坏或缺失时尝试备份。
- 状态：FIXED_IN_SOURCE；更严格的 null/类型校验由 v1.25 补齐。


## v1.25 遗漏错误补档（2026-09-10）

### ERR-CACHE-002 — 刷新课程后选择和进度丢失

- 根因：刷新结果直接替换课程对象。
- 修复：按 URL 复用已有对象，保留选择、视频数量、完成数量和最近章节，并去重。
- v1.26 验证：Windows 回归测试 PASS。
- 状态：RESOLVED_BY_V1.26_TEST。

### ERR-SESSION-001 — 清除会话后旧备份再次恢复

- 根因：只删除主文件，没有同时删除 `.bak`。
- 修复：先删除备份，再删除主文件。
- v1.26 验证：Windows 回归测试 PASS。
- 状态：RESOLVED_BY_V1.26_TEST。

### ERR-JSON-002 — null、数组或坏主文件可能破坏有效备份

- 根因：旧有效性检查只验证 JSON 语法，没有验证当前模型能否反序列化为非 null 对象。
- 修复：备份前按目标模型验证；坏主文件加载失败时使用备份；临时文件独立命名并清理。
- v1.26 验证：3 类坏主文件、正常备份和临时文件清理测试 PASS。
- 状态：RESOLVED_BY_V1.26_TEST。

### ERR-CACHE-003 — 单个 null 课程条目影响整个缓存

- 修复：加载时跳过 null，保留其余有效课程。
- v1.26 验证：Windows 回归测试 PASS。
- 状态：RESOLVED_BY_V1.26_TEST。

### ERR-CHAPTER-001 — 章节打开异常可能遗留点击锁

- 修复：章节打开路径增加异常记录、暂停辅助和 `finally` 解锁。
- 状态：FIXED_IN_SOURCE / BUILD_VERIFIED / REAL_COURSE_NOT_RUN。


## v1.26 Windows 实测新增错误（2026-09-11）

### ERR-BUILD-006 — 赋值语句断开 if/else-if，源码无法编译

- 首次确认：v1.25 Windows `dotnet test`。
- 原始证据：`MainWindow.xaml.cs(762,52)` 报 CS8641、CS1003、CS1525、CS1026、CS1002。
- 影响版本：静态对照确认 v1.24 与 v1.25 均含同一错误。
- 根因：`_lastObservedPlayerSnapshot = snapshot;` 被插在 `if` 结束与 `else if` 开始之间。
- 修复：移动赋值到完整分支链之后。
- 验证：v1.26 Windows Release Build PASS；MSTest 29/29 PASS。
- 状态：RESOLVED。

### ERR-MEDIA-001 — mediaId 复用掩盖真实媒体路径变化

- 首次确认：修复编译后运行 v1.25 已有测试，28 项中 1 项失败。
- 现象：`a.mp4` 切换为 `b.mp4` 时，因为 mediaId、文档和 DOM 位置相同而误判为同一媒体。
- 根因：mediaId 被作为过强证据，没有区分 CDN 查询参数变化与资源路径变化。
- 修复：仅在源地址路径一致时容忍查询参数轮换；路径变化时要求一致 TaskKey 才视为同一任务。
- 验证：新增组合测试后 MSTest 29/29 PASS。
- 状态：RESOLVED。

### WARN-TEST-001 — DataTestMethod 已过时

- 证据：MSTEST0044。
- 修复：改用 `TestMethod + DataRow`。
- 验证：重新构建 0 warning。
- 状态：RESOLVED。
