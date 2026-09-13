using System.Text.Json;
using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ChaoxingLearningAssistant.Chaoxing;

/// <summary>
/// 学习通页面适配层。所有 DOM 识别和播放器脚本集中在此处，
/// 页面改版时优先修改此类，而不是修改主窗口业务逻辑。
/// 任何识别结果不确定时都采用“停止猜测、交给人工”的安全降级策略。
/// </summary>
public sealed class ChaoxingAdapter : IDisposable
{
    private readonly WebView2 _webView;
    private readonly FileLogger _logger;
    private readonly List<CoreWebView2Frame> _frames = new();
    private bool _disposed;
    private bool _playRequestInProgress;
    private int _documentVersion;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChaoxingAdapter(WebView2 webView, FileLogger logger)
    {
        _webView = webView;
        _logger = logger;
    }

    public void Attach()
    {
        var core = _webView.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 尚未初始化。");

        core.FrameCreated += OnFrameCreated;
        core.NavigationStarting += OnNavigationStarting;
    }

    public async Task<IReadOnlyList<CourseItem>> ScanCoursesAsync()
    {
        const string script = """
(() => {
  const normalize = s => (s || '').replace(/\s+/g,' ').trim();
  const clean = raw => normalize(raw)
    .replace(/^(进入|打开|查看|开始|继续|返回)\s*(课程|学习)\s*[:：-]?\s*/i, '')
    .replace(/\s*(进入课程|开始学习|继续学习|查看课程|返回课程)\s*$/i, '')
    .trim();
  const generic = s => /^(课程|我的课程|课程首页|返回|返回课程|进入课程|打开课程|查看课程|学习|开始学习|继续学习|详情)$/i.test(normalize(s));
  const courseIdOf = href => {
    try {
      const u = new URL(href, location.href);
      for (const key of ['courseId','courseid','courseId_']) {
        const value = u.searchParams.get(key);
        if (value) return value;
      }
    } catch {}
    return '';
  };
  const out = [];
  const seen = new Set();
  const anchors = Array.from(document.querySelectorAll('a[href]'));
  for (const a of anchors) {
    const directLabel = normalize(a.getAttribute?.('title') || a.innerText || a.textContent);
    if (/^(?:返回|返回课程|课程首页|我的课程)$/i.test(directLabel)) continue;
    let href = '';
    try { href = new URL(a.href || a.getAttribute?.('href') || '', location.href).href; } catch { continue; }
    const low = href.toLowerCase();
    const looksCourse = low.includes('courseid=') || low.includes('/course/') ||
                        low.includes('studentcourse') || low.includes('visit/courses') ||
                        low.includes('mycourse');
    if (!looksCourse) continue;

    const card = a.closest?.('[data-courseid],[data-course-id],li,.course,.course-item,.courseItem,.course-card,.courseCard,.courseList,.course-list,.course-list-item,.courseInfo,.course-info,.Mconright') || a;
    const candidates = [
      a.getAttribute?.('title'),
      a.innerText,
      a.textContent,
      card?.querySelector?.('[data-course-name]')?.getAttribute?.('data-course-name'),
      card?.querySelector?.('.course-name,.courseName,.coursename,.course_name,.course-title,.courseTitle,.course-name-box,.ktmc,.name,.title')?.getAttribute?.('title'),
      card?.querySelector?.('.course-name,.courseName,.coursename,.course_name,.course-title,.courseTitle,.course-name-box,.ktmc,.name,.title')?.innerText
    ].map(clean).filter(x => x && x.length >= 2 && x.length <= 120 && !generic(x));
    const title = candidates[0] || '';
    if (!title) continue;

    const courseId = courseIdOf(href);
    const key = courseId ? `id:${courseId}` : `url:${href}`;
    if (seen.has(key)) continue;
    seen.add(key);
    out.push({ title, url: href, progressText: normalize(card?.innerText || card?.textContent) });
  }

  // Some course-home variants keep course/class ids in the card and use a
  // JavaScript-only button. Build the same ordinary study URL from those ids.
  const cards = Array.from(document.querySelectorAll('[data-courseid],[data-course-id],li'));
  for (const card of cards) {
    const courseInput = card.querySelector?.('input[name="courseId"],input[name="courseid"]');
    const courseId = normalize(card.getAttribute?.('data-courseid') || card.getAttribute?.('data-course-id') ||
                               courseInput?.value || courseInput?.getAttribute?.('value'));
    if (!courseId) continue;
    const classInput = card.querySelector?.('input[name="classId"],input[name="clazzid"],input[name="jclassId"]');
    const classId = normalize(card.getAttribute?.('data-classid') || card.getAttribute?.('data-clazzid') ||
                              classInput?.value || classInput?.getAttribute?.('value'));
    const titleNode = card.querySelector?.('[data-course-name],.course-name,.courseName,.coursename,.course_name,.course-title,.courseTitle,.course-name-box,.ktmc,.name,.title');
    const title = [
      card.getAttribute?.('data-course-name'),
      titleNode?.getAttribute?.('data-course-name'),
      titleNode?.getAttribute?.('title'),
      titleNode?.innerText,
      titleNode?.textContent
    ].map(clean).find(x => x && x.length >= 2 && x.length <= 120 && !generic(x)) || '';
    if (!title) continue;

    const key = `id:${courseId}`;
    if (seen.has(key)) continue;
    const host = /(^|\.)mooc[^.]*\.chaoxing\.com$/i.test(location.hostname || '')
      ? location.origin : 'https://mooc1.chaoxing.com';
    const query = new URLSearchParams({ courseId });
    if (classId) query.set('clazzid', classId);
    seen.add(key);
    out.push({ title, url: `${host}/mycourse/studentcourse?${query}`, progressText: normalize(card.innerText || card.textContent) });
  }
  return out.slice(0, 200);
})()
""";
        var dto = await ExecuteAcrossDocumentsAsync<List<CourseDto>>(script);
        var courses = dto
            .SelectMany(x => x)
            .Where(x => !string.IsNullOrWhiteSpace(x.Title) &&
                        !IsCourseNavigationLabel(x.Title) &&
                        Uri.TryCreate(x.Url, UriKind.Absolute, out _))
            .GroupBy(x => GetCourseIdentity(x.Url), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Title.Length).First())
            .Select(x => {
                var progress = TaskPointProgress.Parse(x.ProgressText);
                return new CourseItem { Title = x.Title.Trim(), Url = x.Url,
                    TaskCount = progress?.Total, CompletedTaskCount = progress?.Completed ?? 0 };
            })
            .ToArray();

        _logger.Info("CX-COURSE-SCAN", $"课程候选识别数量：{courses.Length}");
        return courses;
    }

    private static bool IsCourseNavigationLabel(string? title)
    {
        var text = title?.Trim();
        return text is "课程" or "我的课程" or "课程首页" or "返回" or "返回课程" or
            "进入课程" or "打开课程" or "查看课程" or "学习" or "开始学习" or
            "继续学习" or "详情";
    }

    public async Task<IReadOnlyList<ChapterItem>> ScanChaptersAsync()
    {
        const string script = """
(() => {
  const normalize = s => (s || '').replace(/\s+/g,' ').trim();
  const cleanTitle = raw => {
    let s = normalize(raw);
    s = s.replace(/\b(?:未完成|未看完|待完成任务点|待完成|未开始|进行中|已完成|已看完|全部完成)\b/gi, ' ');
    s = s.replace(/\bjobUnfinishCount\s*[:=]?\s*\d+\b/gi, ' ');
    s = s.replace(/(?:任务点|任务|完成度)\s*[:：]?\s*\d*%?/gi, ' ');
    s = normalize(s);
    s = s.replace(/\s+[·|｜-]?\s*(?:视频|资料|文档|测验|作业|签到|考试)\s*$/i, '').trim();
    return s.length <= 160 ? s : s.slice(0, 160).trim();
  };
  const getHref = el => {
    const raw = el?.getAttribute?.('href') || el?.href || '';
    if (!raw || /^javascript:/i.test(raw)) return '';
    try { return new URL(raw, location.href).href; } catch { return ''; }
  };
  const extractFromOne = (el, href) => {
    if (!el) return '';
    if (href) {
      try {
        const u = new URL(href, location.href);
        for (const key of ['chapterId','knowledgeId','knowledgeid']) {
          const value = u.searchParams.get(key);
          if (value) return value;
        }
      } catch {}
    }
    const action = el.getAttribute?.('onclick') || '';
    let m = action.match(/toOld\s*\(\s*['"][^'"]+['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (m?.[1]) return m[1];
    m = action.match(/(?:chapterId|knowledgeId|knowledgeid)\s*[:=,]\s*['"]?([\w-]+)/i);
    if (m?.[1]) return m[1];
    m = String(el.id || '').match(/^cur(.+)$/i);
    return m?.[1] || '';
  };
  const extractChapterId = (el, href) => {
    let id = extractFromOne(el, href);
    if (id) return id;
    const related = [
      el?.querySelector?.('[id^="cur"]'),
      el?.querySelector?.('[onclick*="toOld"]'),
      el?.querySelector?.('a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="]'),
      el?.closest?.('[id^="cur"]'),
      el?.closest?.('[onclick*="toOld"]')
    ].filter(Boolean);
    for (const item of related) {
      const itemHref = getHref(item);
      id = extractFromOne(item, itemHref);
      if (id) return id;
    }
    return '';
  };
  const isTaskPointContainer = el => {
    if (!el) return false;
    const cls = normalize(el.getAttribute?.('class') || el.className || '');
    if (/\b(?:ans-attach-ct|ans-job|ans-videoquiz|task-point|taskPoint)\b/i.test(cls)) return true;
    return !!(el.getAttribute?.('data-attachment') || el.getAttribute?.('data-objectid') || el.getAttribute?.('data-object-id'));
  };
  const isTaskPointNode = node => {
    if (!node) return false;
    if (node.matches?.('.chapter_item,.chapter_unit,.catalog_title,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells,[id^="cur"]')) return false;
    if (isTaskPointContainer(node)) return true;
    const task = node.closest?.('.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-attachment],[data-objectid]');
    return isTaskPointContainer(task);
  };
  const looksCatalogNode = el => {
    if (!el) return false;
    const cls = normalize(el.getAttribute?.('class') || el.className || '');
    if (/\b(?:chapter_item|chapter_unit|catalog_item|posCatalog_select|menulist-menu-title|menulist-menu|ncells)\b/i.test(cls)) return true;
    if (/^cur/i.test(String(el.id || ''))) return true;
    const action = el.getAttribute?.('onclick') || '';
    if (/toOld\s*\(/i.test(action)) return true;
    const href = getHref(el);
    return /(?:chapterId|knowledgeId|knowledgeid)=/i.test(href);
  };
  const resolveCatalogNode = node => {
    if (!node || isTaskPointNode(node)) return null;
    if (looksCatalogNode(node)) return node;
    const candidate = node.closest?.('.chapter_item,.chapter_unit,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells,li[id^="cur"],dd[id^="cur"],[id^="cur"]');
    return looksCatalogNode(candidate) ? candidate : node;
  };
  const resolveClickable = node => {
    if (!node) return null;
    if (node.matches?.('a[href],[onclick*="toOld"],[id^="cur"][onclick],.chapter_item[onclick],.catalog_title[onclick]')) return node;
    return node.querySelector?.('[onclick*="toOld"],a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="],a[href*="studentstudy"],a[href*="nodedetail"],[id^="cur"][onclick]') || node;
  };
  const titleFrom = (node, clickable) => {
    const direct = [
      node?.querySelector?.('.catalog_name span[title]'),
      node?.querySelector?.('.chapter_Thats_bnt span[title]'),
      node?.querySelector?.('.catalog_name'),
      node?.querySelector?.('.chapter_name'),
      node?.querySelector?.('.chapterText'),
      node?.querySelector?.('.articlename'),
      node?.querySelector?.('h4 > a'),
      clickable?.querySelector?.('[title]'),
      clickable,
      node
    ].filter(Boolean);
    for (const el of direct) {
      const value = cleanTitle(el.getAttribute?.('title') || el.innerText || el.textContent || '');
      if (value && value.length <= 160 && !/^(进入|打开|查看|学习|详情)$/i.test(value)) return value;
    }
    return '';
  };
  const completionState = node => {
    // innerText only contains the currently rendered state. textContent/outerHTML can include
    // hidden finished/unfinished icon templates, so they must never override visible evidence.
    const visible = normalize([
      node?.getAttribute?.('title'), node?.getAttribute?.('aria-label'), node?.innerText,
      node?.querySelector?.('.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a')?.innerText
    ].filter(Boolean).join(' '));
    if (/未完成|未看完|待完成|待完成任务点|未通过|未开始|进行中|not\s+(?:completed|finished)|incomplete|unfinished/i.test(visible)) return false;
    if (/已完成|已看完|全部完成|\b(?:finished|completed)\b/i.test(visible)) return true;
    const markup = (node?.outerHTML || '').slice(0, 2200);
    const count = markup.match(/jobUnfinishCount["']?\s*[:=]\s*["']?(\d+)/i);
    if (count) return Number(count[1]) === 0;
    const unfinished = node?.querySelector?.('.orange01,.ans-job-unfinished,.ans-job-unfinish,[class*="unfinished"],[class*="unfinish"],[class*="icon_not"]');
    const finished = node?.querySelector?.('.ans-job-finished,[class~="finished"],[class*="icon_finished"]');
    if (finished && !unfinished) return true;
    if (unfinished && !finished) return false;
    return null;
  };
  const detect = node => {
    const evidence = normalize([
      node?.getAttribute?.('class'), node?.getAttribute?.('title'), node?.getAttribute?.('aria-label'),
      node?.innerText, node?.textContent,
      node?.querySelector?.('.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a')?.innerText,
      (node?.outerHTML || '').slice(0, 1800)
    ].filter(Boolean).join(' ')).toLowerCase();
    if (evidence.includes('视频') || evidence.includes('video') || evidence.includes('shipin') ||
        node?.querySelector?.('video,iframe[src*="video" i],[class*="video"],[class*="shipin"],[title*="视频"],[data-type*="video"],[onclick*="video"]')) return 'Video';
    if (evidence.includes('测验') || evidence.includes('测试') || evidence.includes('quiz')) return 'Quiz';
    if (evidence.includes('作业') || evidence.includes('homework')) return 'Homework';
    if (evidence.includes('签到') || evidence.includes('sign')) return 'SignIn';
    if (evidence.includes('考试') || evidence.includes('exam')) return 'Exam';
    if (evidence.includes('文档') || evidence.includes('资料') || evidence.includes('阅读') || evidence.includes('document')) return 'Document';
    return 'Unknown';
  };
  const isActive = node => {
    if (!node) return false;
    const attrs = normalize([
      node.getAttribute?.('class'), node.className, node.getAttribute?.('aria-current'), node.getAttribute?.('aria-selected'),
      node.getAttribute?.('data-selected'), node.getAttribute?.('data-current')
    ].filter(Boolean).join(' '));
    if (/\b(?:active|current|selected|on)\b/i.test(attrs)) return true;
    if (/posCatalog_select|catalog_select|chapter[_-]?(?:selected|current)/i.test(attrs)) return true;
    const marker = node.querySelector?.('[aria-current="true"],[aria-selected="true"],.active,.current,.selected,.catalog_select,.posCatalog_select');
    return !!marker;
  };
  const fallbackUrl = (el, chapterId) => {
    if (!chapterId) return '';
    const action = el?.getAttribute?.('onclick') || '';
    const m = action.match(/toOld\s*\(\s*['"]([^'"]+)['"]\s*,\s*['"]([^'"]+)['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (!m) return '';
    const params = new URLSearchParams({ chapterId: m[2], courseId: m[1], clazzid: m[3], mooc2: '1', hidetype: '0' });
    try {
      const here = new URL(location.href);
      for (const key of ['cpi','enc','fid']) {
        const value = here.searchParams.get(key);
        if (value) params.set(key, value);
      }
    } catch {}
    return 'https://mooc1.chaoxing.com/mycourse/studentstudy?' + params.toString();
  };

  const primarySelectors = [
    '.chapter_item[onclick*="toOld"]','.chapter_item','.chapter_unit','.catalog_title[onclick*="toOld"]',
    '.catalog_item[id^="cur"]','.posCatalog_select[id^="cur"]','.menulist-menu-title[id^="cur"]',
    '.menulist-menu[id^="cur"]','[id^="cur"][onclick]','.ncells','.ncells h4 > a'
  ];
  const fallbackSelectors = [
    'a[href*="knowledgeId="]','a[href*="knowledgeid="]','a[href*="chapterId="]',
    'a[href*="nodedetail"]','a[href*="studentstudy"]','.chapter a[href]','.knowledge a[href]','.units a[href]','.catalog a[href]'
  ];
  let raw = Array.from(new Set(Array.from(document.querySelectorAll(primarySelectors.join(',')))));
  if (!raw.length) raw = Array.from(new Set(Array.from(document.querySelectorAll(fallbackSelectors.join(',')))));

  const out = [];
  const seen = new Set();
  let order = 0;
  for (const rawNode of raw) {
    const node = resolveCatalogNode(rawNode);
    if (!node || isTaskPointNode(node)) continue;
    const clickable = resolveClickable(node);
    let href = getHref(clickable) || getHref(node);
    const hasDirectUrl = !!href;
    const chapterId = extractChapterId(clickable, href) || extractChapterId(node, href);
    if (!href) href = fallbackUrl(clickable, chapterId) || fallbackUrl(node, chapterId);
    const low = (href || '').toLowerCase();
    const looksChapter = !!chapterId || low.includes('knowledgeid=') || low.includes('chapterid=') ||
                         low.includes('nodedetail') || low.includes('studentstudy') || low.includes('cards');
    if (!looksChapter) continue;
    const title = titleFrom(node, clickable) || (chapterId ? `章节 ${chapterId}` : '');
    if (!title) continue;
    const key = chapterId ? `id:${chapterId}` : `url:${href}`;
    if (seen.has(key)) continue;
    seen.add(key);
    const contextCandidate = rawNode?.closest?.('.ncells,.chapter_item,.chapter_unit,.catalog_item,.menulist-menu,.posCatalog_select,li,dd,.chapter,.knowledge,.unit,.catalog');
    const context = contextCandidate && !isTaskPointNode(contextCandidate) ? contextCandidate : node;
    const completion = completionState(context);
    out.push({
      title, url: href, chapterId, documentUrl: location.href,
      progressText: normalize(context?.innerText || context?.textContent),
      isSyntheticUrl: !hasDirectUrl && !!href, isActive: isActive(node) || isActive(context),
      taskType: detect(context), isCompleted: completion === true,
      completionKnown: completion !== null, order: order++
    });
  }
  return out.slice(0, 500);
})()
""";
        var dto = await ExecuteAcrossDocumentsAsync<List<ChapterDto>>(script);
        var merged = dto.SelectMany(x => x)
            .Where(x => !string.IsNullOrWhiteSpace(x.Title) &&
                        (!string.IsNullOrWhiteSpace(x.ChapterId) || Uri.TryCreate(x.Url, UriKind.Absolute, out _)))
            .GroupBy(x => !string.IsNullOrWhiteSpace(x.ChapterId) ? $"id:{x.ChapterId}" : $"url:{x.Url}", StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var best = g
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Url) && !x.IsSyntheticUrl)
                    .ThenBy(x => x.Title.Length)
                    .ThenBy(x => x.Order)
                    .First();
                if (g.Any(x => ParseTaskType(x.TaskType) == TaskType.Video))
                    best.TaskType = TaskType.Video.ToString();
                var known = g.Where(x => x.CompletionKnown).ToArray();
                best.CompletionKnown = known.Length > 0;
                best.IsCompleted = known.Length > 0 && known.All(x => x.IsCompleted);
                best.IsActive = g.Any(x => x.IsActive);
                best.Order = g.Min(x => x.Order);
                return best;
            })
            .OrderBy(x => x.Order)
            .ToArray();

        var result = new List<ChapterItem>();
        var index = 0;
        foreach (var x in merged)
        {
            var progress = TaskPointProgress.Parse(x.ProgressText);
            result.Add(new ChapterItem
            {
                TaskCount = progress?.Total,
                CompletedTaskCount = progress?.Completed ?? 0,
                Index = index++,
                Title = x.Title,
                Url = x.Url,
                ChapterId = x.ChapterId,
                DocumentUrl = x.DocumentUrl,
                IsSyntheticUrl = x.IsSyntheticUrl,
                IsActive = x.IsActive,
                TaskType = ParseTaskType(x.TaskType),
                IsCompleted = x.IsCompleted,
                CompletionKnown = x.CompletionKnown
            });
        }

        _logger.Info("CX-CHAPTER-SCAN",
            $"章节目录识别数量：{result.Count}；网页当前标记：{result.Count(x => x.IsActive)}；明确未完成：{result.Count(x => x.CompletionKnown && !x.IsCompleted)}");
        return result;
    }

    /// <summary>
    /// 扫描真实视频任务点。章节目录和视频任务分开建模，避免一个章节包含多个视频时
    /// 被压缩成一条 ChapterItem，导致“当前视频 / 未完成 / 下一视频”互相错位。
    /// </summary>
    public async Task<IReadOnlyList<VideoTaskItem>> ScanVideoTasksAsync()
    {
        const string script = """
(() => {
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const cleanTitle = raw => {
    let s = norm(raw)
      .replace(/\b(?:未完成|未看完|待完成任务点|待完成|未开始|进行中|已完成|已看完|全部完成)\b/gi, ' ')
      .replace(/(?:任务点|任务|完成度)\s*[:：]?\s*\d*%?/gi, ' ')
      .replace(/\s+/g,' ').trim();
    if (/^(?:视频|video|播放器|player|任务点)$/i.test(s)) return '';
    return s.length <= 180 ? s : s.slice(0,180).trim();
  };
  const attr = (el, name) => el?.getAttribute?.(name) || '';
  const chapterIdFromUrl = raw => {
    try {
      const u = new URL(raw || location.href, location.href);
      for (const key of ['chapterId','knowledgeId','knowledgeid']) {
        const id = u.searchParams.get(key);
        if (id) return id;
      }
    } catch {}
    return '';
  };
  const chapterIdFromNode = el => {
    if (!el) return '';
    const holder = el.closest?.('[data-knowledgeid],[data-knowledge-id],[data-chapterid],[data-chapter-id]');
    let id = attr(holder,'data-knowledgeid') || attr(holder,'data-knowledge-id') || attr(holder,'data-chapterid') || attr(holder,'data-chapter-id');
    if (id) return norm(id);
    const chapter = el.closest?.('.chapter_item,.chapter_unit,.catalog_item,.ncells,[id^="cur"],[onclick*="toOld"]');
    const action = attr(chapter,'onclick');
    let m = action.match(/toOld\s*\(\s*['"][^'"]+['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (m?.[1]) return m[1];
    m = String(chapter?.id || '').match(/^cur(.+)$/i);
    return m?.[1] || '';
  };
  const chapterTitleFromNode = el => {
    const chapter = el?.closest?.('.chapter_item,.chapter_unit,.catalog_item,.ncells,[data-chapter-title],[data-knowledge-title]');
    return cleanTitle(
      attr(chapter,'data-chapter-title') || attr(chapter,'data-knowledge-title') ||
      chapter?.querySelector?.('.catalog_name,.chapter_name,.chapterText,.articlename,h4')?.innerText || ''
    );
  };
  const taskBlock = v => v?.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]') || v?.parentElement || null;
  const mediaIdOf = (v, block) => [
    attr(v,'data-objectid'), attr(v,'data-object-id'), attr(v,'data-id'), attr(v,'data-mid'),
    attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
    attr(block,'data-attachment'), attr(block,'data-attach-id'),
    attr(block?.querySelector?.('iframe'),'objectid'), attr(block?.querySelector?.('iframe'),'data-objectid'),
    attr(block?.querySelector?.('iframe'),'mid')
  ].map(norm).find(Boolean) || '';
  const completion = el => {
    const visible = norm(el?.innerText || '');
    if (/未完成|未看完|待完成|待完成任务点|未开始|进行中|incomplete|unfinished/i.test(visible)) return false;
    if (/已完成|已看完|全部完成|\b(?:finished|completed)\b/i.test(visible)) return true;
    const markup = (el?.outerHTML || '').slice(0,4200);
    const count = markup.match(/jobUnfinishCount["']?\s*[:=]\s*["']?(\d+)/i);
    if (count) return Number(count[1]) === 0;
    const unfinished = el?.querySelector?.('.orange01,.ans-job-unfinished,.ans-job-unfinish,[class*="unfinished"],[class*="unfinish"],[class*="icon_not"]');
    const finished = el?.querySelector?.('.ans-job-finished,[class~="finished"],[class*="icon_finished"]');
    if (finished && !unfinished) return true;
    if (unfinished && !finished) return false;
    return null;
  };
  const titleOf = (v, block) => {
    const frame = block?.querySelector?.('iframe');
    const node = block?.querySelector?.('.video-name,.video-title,.task-title,.title,.ans-job-title,.ans-attach-title,[data-title],[data-name],[title]');
    const candidates = [
      attr(v,'title'), attr(v,'aria-label'), attr(v,'data-title'), attr(v,'data-name'),
      attr(block,'data-title'), attr(block,'data-name'), attr(block,'title'),
      attr(frame,'data-title'), attr(frame,'data-name'), attr(frame,'title'),
      node?.getAttribute?.('data-title'), node?.getAttribute?.('data-name'), node?.getAttribute?.('title'),
      node?.innerText
    ];
    const frameData = attr(frame,'data');
    if (frameData) {
      try {
        const metadata = JSON.parse(frameData);
        candidates.unshift(metadata?.property?.name, metadata?.property?.title, metadata?.name, metadata?.title);
      } catch {}
    }
    for (const candidate of candidates) {
      const title = cleanTitle(candidate || '');
      if (title && title.length >= 2) return title;
    }
    return '';
  };
  const out = [];
  const seen = new Set();
  // 真实 video 与尚未创建 video 的 iframe 必须共用同一套父任务 DOM 顺序。
  const blocks = Array.from(new Set(Array.from(document.querySelectorAll(
    '.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-type*="video"],[class*="ans-video"]'
  ))));
  const videos = Array.from(document.querySelectorAll('video'));
  for (let i = 0; i < videos.length; i++) {
    const v = videos[i];
    const block = taskBlock(v);
    const blockIndex = blocks.indexOf(block);
    const pageOrder = blockIndex >= 0 ? blockIndex : blocks.length + i;
    const mediaId = mediaIdOf(v, block);
    const source = norm(v.currentSrc || v.src || '');
    const chapterId = chapterIdFromNode(v) || chapterIdFromUrl(location.href);
    const rect = v.getBoundingClientRect?.() || { width:0, height:0 };
    const isVisible = rect.width > 10 && rect.height > 10;
    const taskKey = mediaId ? `media:${mediaId}|src:${source}|doc:${location.href}|dom:${i}` : source ? `src:${source}` : `doc:${location.href}|dom:${i}`;
    if (seen.has(taskKey)) continue;
    seen.add(taskKey);
    const done = completion(block);
    out.push({
      index:pageOrder, taskKey, title:titleOf(v,block), chapterId, chapterTitle:chapterTitleFromNode(v),
      documentUrl:location.href, source, mediaId, domIndex:i, isVisible,
      isPlaying:!v.paused && !v.ended, isCompleted:done === true, completionKnown:done !== null
    });
  }

  // 部分模板的视频任务先以 iframe/任务块存在，video 只有激活后才创建。
  for (const block of blocks) {
    if (block.querySelector?.('video')) continue;
    const iframe = block.querySelector?.('iframe');
    const frameEvidence = norm([
      iframe?.src, attr(iframe,'src'), attr(iframe,'_src'), attr(iframe,'class'), attr(iframe,'title'),
      attr(iframe,'data-type'), attr(iframe,'type'), attr(iframe,'module'), attr(iframe,'data')
    ].filter(Boolean).join(' '));
    const taskLabel = norm((block.innerText || block.textContent || '') + ' ' + attr(block,'title') + ' ' + frameEvidence);
    if (/章节测验|测试题|测验|作业|签到|考试|homework|exam|workid|worktype|module["']?\s*[:=]\s*["']?work/i.test(taskLabel)) continue;
    const looksVideo = !!iframe && /video|richvideo|insertvideo|shipin|module["']?\s*[:=]\s*["']?video/i.test(frameEvidence + ' ' + norm(block.className || '')) ||
                       /视频|video|shipin/i.test(norm(attr(block,'title')) + ' ' + norm(block.className || ''));
    if (!looksVideo) continue;
    const mediaId = mediaIdOf(null, block);
    const source = norm(iframe?.src || attr(iframe,'src') || attr(iframe,'_src'));
    const chapterId = chapterIdFromNode(block) || chapterIdFromUrl(location.href);
    const blockOrder = blocks.indexOf(block);
    const taskKey = mediaId ? `media:${mediaId}|src:${source}|doc:${location.href}|block:${blockOrder}` : source ? `frame:${source}` : `doc:${location.href}|block:${blockOrder}`;
    if (seen.has(taskKey)) continue;
    seen.add(taskKey);
    const done = completion(block);
    const rect = block.getBoundingClientRect?.() || { width:0, height:0 };
    out.push({
      index:blockOrder, taskKey, title:titleOf(null,block), chapterId, chapterTitle:chapterTitleFromNode(block),
      documentUrl:location.href, source, mediaId, domIndex:-1, isVisible:rect.width > 10 && rect.height > 10,
      isPlaying:false, isCompleted:done === true, completionKnown:done !== null
    });
  }
  return out.slice(0,800);
})()
""";
        var dto = await ExecuteAcrossDocumentsAsync<List<VideoTaskDto>>(script);
        var rawTasks = dto.SelectMany(x => x)
            .Where(x => !string.IsNullOrWhiteSpace(x.TaskKey))
            .Select(x => new VideoTaskItem
            {
                Index = x.Index,
                TaskKey = x.TaskKey,
                Title = x.Title,
                ChapterId = x.ChapterId,
                ChapterTitle = x.ChapterTitle,
                DocumentUrl = x.DocumentUrl,
                Source = x.Source,
                MediaId = x.MediaId,
                DomIndex = x.DomIndex,
                IsVisible = x.IsVisible,
                IsPlaying = x.IsPlaying,
                IsCompleted = x.IsCompleted,
                CompletionKnown = x.CompletionKnown
            })
            .ToArray();

        // 父文档任务块通常掌握 chapterId / 标题 / 完成状态，真正的 <video> 则位于子 iframe。
        // v1.22 直接丢弃父占位会把最关键的章节证据一起丢掉；v1.23 先把父任务证据合并进真实播放器任务，再去重。
        var realVideoTasks = rawTasks.Where(x => x.DomIndex >= 0).ToArray();
        var placeholders = rawTasks.Where(x => x.DomIndex < 0).ToArray();
        foreach (var real in realVideoTasks)
        {
            foreach (var parent in placeholders.Where(parent => VideoTaskEvidence.PlaceholderMatchesReal(parent, real)))
                VideoTaskEvidence.MergeParentEvidence(real, parent);
        }

        var flattened = rawTasks
            .Where(x => x.DomIndex >= 0 || !realVideoTasks.Any(real => VideoTaskEvidence.PlaceholderMatchesReal(x, real)))
            .GroupBy(x => x.TaskKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.DomIndex >= 0)
                .ThenByDescending(x => x.IsPlaying)
                .ThenByDescending(x => x.IsVisible)
                .ThenByDescending(x => x.CompletionKnown)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ChapterId))
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Title))
                .First())
            .OrderBy(x => x.Index)
            .ToArray();

        var result = flattened.Select((x, index) =>
        {
            x.Index = index;
            return x;
        }).ToArray();

        _logger.Info("CX-VIDEO-TASK-SCAN",
            $"真实视频任务识别数量：{result.Length}；正在播放：{result.Count(x => x.IsPlaying)}；明确未完成：{result.Count(x => x.CompletionKnown && !x.IsCompleted)}");
        return result;
    }

    public async Task<ChapterItem?> FindNextChapterCandidateAsync(string? currentChapterId)
    {
        var targetId = JsonSerializer.Serialize(currentChapterId ?? string.Empty);
        var script = $$"""
(() => {
  const targetId = {{targetId}};
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const cleanTitle = raw => norm(raw)
    .replace(/未完成|未看完|待完成任务点|待完成|未开始|进行中|已完成|已看完|全部完成/gi, ' ')
    .replace(/\s+[·|｜-]?\s*(?:视频|资料|文档|测验|作业|签到|考试)\s*$/i, '')
    .replace(/\s+/g,' ').trim();
  const getHref = el => {
    const raw = el?.getAttribute?.('href') || el?.href || '';
    if (!raw || /^javascript:/i.test(raw)) return '';
    try { return new URL(raw, location.href).href; } catch { return ''; }
  };
  const getIdOne = el => {
    if (!el) return '';
    const href = getHref(el);
    if (href) {
      try {
        const u = new URL(href, location.href);
        for (const key of ['chapterId','knowledgeId','knowledgeid']) {
          const value = u.searchParams.get(key);
          if (value) return value;
        }
      } catch {}
    }
    const action = el.getAttribute?.('onclick') || '';
    let m = action.match(/toOld\s*\(\s*['"][^'"]+['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (m?.[1]) return m[1];
    m = String(el.id || '').match(/^cur(.+)$/i);
    return m?.[1] || '';
  };
  const getId = el => getIdOne(el) || getIdOne(el?.querySelector?.('[id^="cur"]')) ||
                    getIdOne(el?.querySelector?.('[onclick*="toOld"]')) ||
                    getIdOne(el?.querySelector?.('a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="]'));
  const titleOf = el => {
    const t = el?.querySelector?.('.catalog_name span[title],.chapter_Thats_bnt span[title],.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a') || el;
    const value = cleanTitle(t?.getAttribute?.('title') || t?.innerText || t?.textContent || el?.getAttribute?.('title') || '');
    return value && value.length <= 180 ? value : '';
  };
  const completion = el => {
    const visible = norm(el?.innerText || '');
    if (/未完成|未看完|待完成|待完成任务点|未开始|进行中|incomplete|unfinished/i.test(visible)) return false;
    if (/已完成|已看完|全部完成|\b(?:finished|completed)\b/i.test(visible)) return true;
    const markup = (el?.outerHTML || '').slice(0,2600);
    const count = markup.match(/jobUnfinishCount["']?\s*[:=]\s*["']?(\d+)/i);
    if (count) return Number(count[1]) === 0;
    const unfinished = el?.querySelector?.('.orange01,.ans-job-unfinished,.ans-job-unfinish,[class*="unfinished"],[class*="unfinish"]');
    const finished = el?.querySelector?.('.ans-job-finished,[class~="finished"]');
    if (finished && !unfinished) return true;
    if (unfinished && !finished) return false;
    return null;
  };
  const detect = el => {
    const text = norm((el?.innerText || el?.textContent || '') + ' ' + (el?.outerHTML || '').slice(0,2600)).toLowerCase();
    if (text.includes('视频') || text.includes('video') || text.includes('shipin') || el?.querySelector?.('video,iframe[src*="video" i],[class*="video"],[title*="视频"],[data-type*="video"]')) return 'Video';
    if (text.includes('测验') || text.includes('测试') || text.includes('quiz')) return 'Quiz';
    if (text.includes('作业') || text.includes('homework')) return 'Homework';
    if (text.includes('签到')) return 'SignIn';
    if (text.includes('考试') || text.includes('exam')) return 'Exam';
    if (text.includes('文档') || text.includes('资料') || text.includes('阅读')) return 'Document';
    return 'Unknown';
  };
  const selectors = [
    '.chapter_item','.catalog_title','.catalog_item[id^="cur"]','.posCatalog_select[id^="cur"]',
    '.menulist-menu-title[id^="cur"]','.menulist-menu[id^="cur"]','[id^="cur"][onclick]',
    '.ncells','a[href*="chapterId="]','a[href*="knowledgeId="]','a[href*="knowledgeid="]','a[href*="studentstudy"]'
  ];
  const raw = Array.from(new Set(Array.from(document.querySelectorAll(selectors.join(',')))));
  const entries = [];
  const seen = new Set();
  for (const el of raw) {
    const id = getId(el);
    const href = getHref(el) || getHref(el?.querySelector?.('a[href]'));
    if (!id && !href) continue;
    const key = id ? `id:${id}` : `url:${href}`;
    if (seen.has(key)) continue;
    seen.add(key);
    entries.push({ el, id, href, title:titleOf(el), completion:completion(el), taskType:detect(el) });
  }
  let currentId = targetId;
  if (!currentId) {
    const selected = document.querySelector?.('.posCatalog_select[id^="cur"],.chapter_item.active[id^="cur"],.chapter_item.cur[id^="cur"],.menulist-menu-title.active[id^="cur"],[aria-current="true"][id^="cur"]');
    currentId = getId(selected);
  }
  if (!currentId) return null;
  const index = entries.findIndex(x => x.id === currentId);
  if (index < 0) return null;
  const allowed = x => x.id !== currentId && !['Quiz','Homework','SignIn','Exam'].includes(x.taskType);
  const tail = entries.slice(index + 1).filter(allowed);
  // 自然播放结束后的“下一章”按目录顺序推进：优先明确未完成，
  // 平台没有暴露完成状态时也允许选择未知项；只跳过明确已完成项。
  const x = tail.find(x => x.completion !== true);
  if (!x) return null;
  return {
    title: x.title || (x.id ? `章节 ${x.id}` : '下一章节'),
    url: x.href || '', chapterId: x.id || '', isSyntheticUrl: false,
    taskType: x.taskType, isCompleted: x.completion === true, completionKnown: x.completion !== null
  };
})()
""";
        var candidates = await ExecuteAcrossDocumentsAsync<ChapterDto>(script, x => !string.IsNullOrWhiteSpace(x.ChapterId) || !string.IsNullOrWhiteSpace(x.Url));
        var x = candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ChapterId) || !string.IsNullOrWhiteSpace(x.Url));
        if (x is null) return null;

        return new ChapterItem
        {
            Index = int.MaxValue,
            Title = string.IsNullOrWhiteSpace(x.Title) ? "下一章节" : x.Title,
            Url = x.Url,
            ChapterId = x.ChapterId,
            IsSyntheticUrl = x.IsSyntheticUrl,
            TaskType = ParseTaskType(x.TaskType),
            IsCompleted = x.IsCompleted,
            CompletionKnown = x.CompletionKnown
        };
    }

    public async Task<PageRecognitionResult> RecognizePageAsync()
    {
        const string script = """
(() => {
  const normalize = s => (s || '').replace(/\s+/g,' ').trim();
  const genericCourseTitle = value => /^(?:学习通|课程|我的课程|学生学习页面|学生学习|学习页面|课程学习|章节学习|任务学习|学生课程|课程页面)$/i.test(normalize(value));
  const text = normalize(document.body?.innerText || '').slice(0, 120000);
  let reason = '';
  const manualWords = ['验证码','人脸','安全验证','人工验证','滑块验证','身份验证','风险验证','学校认证','统一身份认证','登录失效','请重新登录'];
  for (const w of manualWords) {
    if (text.includes(w)) { reason = w; break; }
  }
  const hasVideo = !!document.querySelector('video') || /视频|video/i.test(document.body?.innerHTML?.slice(0,200000) || '');
  let taskType = 'Unknown';
  if (hasVideo) taskType = 'Video';
  else if (/考试|exam/i.test(text)) taskType = 'Exam';
  else if (/测验|测试|quiz/i.test(text)) taskType = 'Quiz';
  else if (/作业|homework/i.test(text)) taskType = 'Homework';
  else if (/签到|sign in/i.test(text)) taskType = 'SignIn';
  else if (/文档|资料|阅读|document/i.test(text)) taskType = 'Document';

  const courseNodes = [
    document.querySelector?.('[data-course-name]'),
    document.querySelector?.('#courseName,.courseName,.course-name,.course_name,.courseTitle,.course-title,.curriculum-name'),
    document.querySelector?.('.navshow .courseName,.course_info .name,.course-info .name')
  ].filter(Boolean);
  let courseTitle = '';
  for (const node of courseNodes) {
    const value = normalize(node.getAttribute?.('data-course-name') || node.getAttribute?.('title') || node.innerText || node.textContent || '');
    if (value.length >= 2 && value.length <= 120 && !genericCourseTitle(value)) { courseTitle = value; break; }
  }
  if (!courseTitle) {
    const docTitle = normalize(document.title || '')
      .replace(/\s*[-_|｜]\s*(?:学生)?学习页面.*$/i, '')
      .replace(/\s*[-_|｜]\s*(?:章节|课程)?学习.*$/i, '')
      .replace(/\s*[-_|｜]\s*(超星)?学习通.*$/i, '')
      .replace(/\s*[-_|｜]\s*课程.*$/i, '')
      .trim();
    if (docTitle.length >= 2 && docTitle.length <= 120 && !genericCourseTitle(docTitle))
      courseTitle = docTitle;
  }

  return {
    url: location.href,
    title: document.title || '',
    courseTitle,
    taskType,
    hasManualIntervention: !!reason,
    manualInterventionReason: reason,
    selectorSummary: 'course-structure-v1.23'
  };
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<PageDto>(script);
        var primary = results
            .FirstOrDefault(x => x.HasManualIntervention)
            ?? results.FirstOrDefault(x => ParseTaskType(x.TaskType) == TaskType.Video)
            ?? results.FirstOrDefault()
            ?? new PageDto();
        var courseTitle = results.Select(x => x.CourseTitle).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? primary.CourseTitle;

        return new PageRecognitionResult
        {
            Url = primary.Url,
            Title = primary.Title,
            CourseTitle = courseTitle,
            TaskType = ParseTaskType(primary.TaskType),
            HasManualIntervention = primary.HasManualIntervention,
            ManualInterventionReason = primary.ManualInterventionReason,
            SelectorSummary = primary.SelectorSummary
        };
    }

    public async Task<PlayerSnapshot> GetPlayerSnapshotAsync(PlayerSnapshot? target = null)
    {
        var hasTarget = target?.Found == true;
        var targetSource = JsonSerializer.Serialize(target?.Source ?? string.Empty);
        var targetMediaId = JsonSerializer.Serialize(target?.MediaId ?? string.Empty);
        var targetDocumentUrl = JsonSerializer.Serialize(target?.DocumentUrl ?? string.Empty);
        var targetDomIndex = target?.DomIndex ?? -1;
        var script = $$"""
(() => {
  const hasTarget = {{(hasTarget ? "true" : "false")}};
  const targetSource = {{targetSource}};
  const targetMediaId = {{targetMediaId}};
  const targetDocumentUrl = {{targetDocumentUrl}};
  const targetDomIndex = {{targetDomIndex}};
  const normalize = s => (s || '').replace(/\s+/g,' ').trim();
  const attr = (el, name) => el?.getAttribute?.(name) || '';
  const cleanVideoTitle = raw => {
    let s = normalize(raw);
    s = s.replace(/\b(?:未完成|未看完|待完成任务点|待完成|未开始|进行中|已完成|已看完|全部完成)\b/gi, ' ');
    s = s.replace(/\s+[·|｜-]?\s*(?:视频|任务点)\s*$/i, '').trim();
    if (/^(视频|video|播放器|player)$/i.test(s)) return '';
    return s.length <= 160 ? s : s.slice(0, 160).trim();
  };
  const chapterIdFromUrl = value => {
    try {
      const u = new URL(value || location.href, location.href);
      for (const key of ['chapterId','knowledgeId','knowledgeid']) {
        const id = u.searchParams.get(key);
        if (id) return id;
      }
    } catch {}
    return '';
  };
  const videos = Array.from(document.querySelectorAll('video'));
  const notFound = () => ({
    found:false, currentTime:0, duration:0, playbackRate:1, paused:true, ended:false,
    readyState:0, source:'', mediaId:'', domIndex:-1, isVisible:false, taskKey:'', documentUrl:location.href,
    chapterId:chapterIdFromUrl(location.href), chapterTitleHint:'', videoTitle:''
  });
  if (!videos.length) return {
    found:false, currentTime:0, duration:0, playbackRate:1, paused:true, ended:false,
    readyState:0, source:'', mediaId:'', domIndex:-1, isVisible:false, taskKey:'', documentUrl:location.href,
    chapterId:chapterIdFromUrl(location.href), chapterTitleHint:'', videoTitle:''
  };
  if (hasTarget && targetDocumentUrl && normalize(location.href) !== normalize(targetDocumentUrl))
    return notFound();
  const domOrder = videos.slice();
  const sourceOf = v => normalize(v?.currentSrc || v?.src || '');
  const sameSourcePath = (left, right) => {
    try {
      const a = new URL(left, location.href), b = new URL(right, location.href);
      return a.protocol === b.protocol && a.host === b.host && a.pathname === b.pathname;
    } catch { return false; }
  };
  const mediaIdOf = v => {
    const block = v?.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]') || v?.parentElement;
    return [
      attr(v,'data-objectid'), attr(v,'data-object-id'), attr(v,'data-id'), attr(v,'data-mid'),
      attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
      attr(block,'data-attachment'), attr(block,'data-attach-id')
    ].map(normalize).find(Boolean) || '';
  };
  const matchesTarget = candidate => {
    const index = domOrder.indexOf(candidate);
    let matched = false;
    if (targetDomIndex >= 0) {
      if (index !== targetDomIndex) return false;
      matched = true;
    }
    const source = sourceOf(candidate);
    if (targetSource && source) {
      if (source !== normalize(targetSource) && !sameSourcePath(source, targetSource)) return false;
      matched = true;
    }
    const mediaId = mediaIdOf(candidate);
    if (targetMediaId && mediaId) {
      if (mediaId !== normalize(targetMediaId)) return false;
      matched = true;
    }
    return matched;
  };
  const targetedVideo = hasTarget ? domOrder.find(matchesTarget) : null;
  if (hasTarget && !targetedVideo) return notFound();
  const score = v => {
    const r = v.getBoundingClientRect();
    const visible = r.width > 10 && r.height > 10 ? 1000000 : 0;
    const playing = !v.paused && !v.ended ? 500000 : 0;
    const notEnded = !v.ended ? 250000 : 0;
    const d = Number.isFinite(v.duration) ? v.duration : 0;
    return visible + playing + notEnded + d;
  };
  videos.sort((a,b) => score(b)-score(a));
  const v = targetedVideo || videos[0];
  const domIndex = domOrder.indexOf(v);
  const rect = v.getBoundingClientRect?.() || { width:0, height:0 };
  const isVisible = rect.width > 10 && rect.height > 10;
  const block = v.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]') || v.parentElement;
  const mediaId = [
    attr(v,'data-objectid'), attr(v,'data-object-id'), attr(v,'data-id'), attr(v,'data-mid'),
    attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
    attr(block,'data-attachment'), attr(block,'data-attach-id')
  ].map(normalize).find(Boolean) || '';
  const titleNode = block?.querySelector?.('.video-name,.video-title,.task-title,.title,.ans-job-title,.ans-attach-title,[data-title],[data-name],[title]');
  const headingBefore = (() => {
    let node = block;
    for (let i = 0; node && i < 5; i++, node = node.parentElement) {
      const h = node?.querySelector?.('h1,h2,h3,h4,.video-name,.video-title,.task-title,.ans-job-title,.ans-attach-title');
      if (h && h !== block) return h;
    }
    return null;
  })();
  const titleCandidates = [
    attr(v,'title'), attr(v,'aria-label'), attr(v,'data-title'), attr(v,'data-name'),
    attr(block,'data-title'), attr(block,'data-name'), attr(block,'title'),
    titleNode?.getAttribute?.('data-title'), titleNode?.getAttribute?.('data-name'), titleNode?.getAttribute?.('title'),
    titleNode?.innerText, headingBefore?.innerText,
    document.querySelector?.('.ans-video .video-name,.ans-video .video-title,.ans-job-title,.ans-attach-title')?.innerText
  ];
  let videoTitle = '';
  for (const candidate of titleCandidates) {
    const cleaned = cleanVideoTitle(candidate || '');
    if (cleaned && cleaned.length >= 2) { videoTitle = cleaned; break; }
  }
  let chapterId = chapterIdFromUrl(location.href);
  if (!chapterId) {
    const holder = v.closest?.('[data-knowledgeid],[data-knowledge-id],[data-chapterid],[data-chapter-id]');
    chapterId = attr(holder,'data-knowledgeid') || attr(holder,'data-knowledge-id') || attr(holder,'data-chapterid') || attr(holder,'data-chapter-id') || '';
  }
  const chapterNode = v.closest?.('.chapter_item,.chapter_unit,.catalog_item,.ncells,[data-chapter-title],[data-knowledge-title]');
  const chapterTitleHint = cleanVideoTitle(
    attr(chapterNode,'data-chapter-title') || attr(chapterNode,'data-knowledge-title') ||
    chapterNode?.querySelector?.('.catalog_name,.chapter_name,.chapterText,.articlename,h4')?.innerText || ''
  );
  const source = v.currentSrc || v.src || '';
  const taskKey = mediaId ? `media:${mediaId}|src:${source}|doc:${location.href}|dom:${domIndex}` :
                  source ? `src:${source}` : `doc:${location.href}|dom:${domIndex}`;
  return {
    found:true,
    currentTime:Number.isFinite(v.currentTime) ? v.currentTime : 0,
    duration:Number.isFinite(v.duration) ? v.duration : 0,
    playbackRate:Number.isFinite(v.playbackRate) ? v.playbackRate : 1,
    paused:!!v.paused,
    ended:!!v.ended,
    readyState:v.readyState || 0,
    source,
    mediaId,
    domIndex,
    isVisible,
    taskKey,
    documentUrl:location.href,
    chapterId,
    chapterTitleHint,
    videoTitle
  };
})()
""";
        var snapshots = await ExecuteAcrossDocumentsAsync<PlayerSnapshot>(script);
        return snapshots
            .Where(x => x.Found)
            // 跨 iframe 汇总时，结束的旧长视频绝不能压过新打开但暂停的真实播放器。
            .OrderByDescending(x => !x.Ended && !x.Paused)
            .ThenByDescending(x => !x.Ended)
            .ThenByDescending(x => x.IsVisible)
            .ThenByDescending(x => x.ReadyState)
            .ThenByDescending(x => x.CurrentTime > 0)
            .ThenByDescending(x => x.Duration)
            .FirstOrDefault() ?? new PlayerSnapshot();
    }

    public async Task<bool> PauseVideoAsync()
    {
        const string script = """
(() => {
  const videos = Array.from(document.querySelectorAll('video'));
  for (const v of videos) { try { v.pause(); } catch {} }
  return videos.length;
})()
""";
        var counts = await ExecuteAcrossDocumentsAsync<int>(script);
        return counts.Sum() > 0;
    }

    public async Task<bool> PlayVideoAsync(PlayerSnapshot? target = null)
    {
        if (_disposed || _playRequestInProgress) return false;
        _playRequestInProgress = true;
        var documentVersion = _documentVersion;
        try
        {
        var hasTarget = target?.Found == true;
        var targetSource = JsonSerializer.Serialize(target?.Source ?? string.Empty);
        var targetMediaId = JsonSerializer.Serialize(target?.MediaId ?? string.Empty);
        var targetDocumentUrl = JsonSerializer.Serialize(target?.DocumentUrl ?? string.Empty);
        var targetDomIndex = target?.DomIndex ?? -1;
        // ExecuteScriptAsync 返回 JSON，不把 Promise 当作 int 解析。
        // JS 只提交一次播放请求；C# 随后核对播放器实际状态再报告成功。
        var script = $$"""
(() => {
  const hasTarget = {{(hasTarget ? "true" : "false")}};
  const targetSource = {{targetSource}};
  const targetMediaId = {{targetMediaId}};
  const targetDocumentUrl = {{targetDocumentUrl}};
  const targetDomIndex = {{targetDomIndex}};
  const norm = s => (s || '').trim();
  const attr = (el, name) => el?.getAttribute?.(name) || '';
  const videos = Array.from(document.querySelectorAll('video'));
  if (hasTarget && targetDocumentUrl && norm(location.href) !== norm(targetDocumentUrl)) return 0;
  const domOrder = videos.slice();
  const sourceOf = v => norm(v?.currentSrc || v?.src || '');
  const sameSourcePath = (left, right) => {
    try {
      const a = new URL(left, location.href), b = new URL(right, location.href);
      return a.protocol === b.protocol && a.host === b.host && a.pathname === b.pathname;
    } catch { return false; }
  };
  const mediaIdOf = v => {
    const block = v?.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]') || v?.parentElement;
    return [
      attr(v,'data-objectid'), attr(v,'data-object-id'), attr(v,'data-id'), attr(v,'data-mid'),
      attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
      attr(block,'data-attachment'), attr(block,'data-attach-id')
    ].map(norm).find(Boolean) || '';
  };
  videos.sort((a,b) => {
    const visible = v => { const r = v.getBoundingClientRect(); return r.width > 10 && r.height > 10 ? 1 : 0; };
    return visible(b) - visible(a);
  });
  const v = hasTarget
    ? videos.find(candidate => {
        const index = domOrder.indexOf(candidate);
        let matched = false;
        if (targetDomIndex >= 0) {
          if (index !== targetDomIndex) return false;
          matched = true;
        }
        const source = sourceOf(candidate);
        if (targetSource && source) {
          if (source !== norm(targetSource) && !sameSourcePath(source, targetSource)) return false;
          matched = true;
        }
        const mediaId = mediaIdOf(candidate);
        if (targetMediaId && mediaId) {
          if (mediaId !== norm(targetMediaId)) return false;
          matched = true;
        }
        return matched;
      })
    : videos[0];
  if (!v) return 0;
  try {
    const request = v.play();
    if (request && typeof request.catch === 'function') request.catch(() => {});
    return 1;
  } catch {
    return 0;
  }
})()
""";
        var counts = await ExecuteAcrossDocumentsAsync<int>(script, x => x > 0);
        if (!counts.Any(x => x > 0)) return false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (_disposed || documentVersion != _documentVersion) return false;
            var snapshot = await GetPlayerSnapshotAsync(target);
            if (_disposed || documentVersion != _documentVersion) return false;
            if (snapshot.Found && !snapshot.Paused && !snapshot.Ended &&
                (!hasTarget || target is null || PlayerMediaEvidence.MatchesPlaybackTarget(target, snapshot)))
                return true;
            await Task.Delay(150);
        }
        return false;
        }
        finally { _playRequestInProgress = false; }
    }

    public async Task<bool> BootstrapChapterCatalogAsync()
    {
        const string script = """
(() => {
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const taskPoint = el => !!el?.closest?.(
    '.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-attachment],[data-objectid],[data-object-id]'
  );
  const hrefOf = el => {
    const raw = el?.getAttribute?.('href') || el?.href || '';
    if (!raw || /^javascript:/i.test(raw)) return '';
    try { return new URL(raw, location.href).href; } catch { return ''; }
  };
  const identityOf = el => {
    if (!el) return '';
    const href = hrefOf(el);
    if (/(?:chapterId|knowledgeId|knowledgeid)=/i.test(href)) return href;
    const action = el.getAttribute?.('onclick') || '';
    if (/toOld\s*\(/i.test(action)) return action;
    return /^cur.+/i.test(String(el.id || '')) ? String(el.id) : '';
  };
  const clickableOf = node => {
    if (!node) return null;
    if (identityOf(node)) return node;
    return node.querySelector?.(
      '[onclick*="toOld"],a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="],[id^="cur"][onclick]'
    );
  };
  const titleOf = node => norm(
    node?.querySelector?.('.catalog_name span[title],.chapter_Thats_bnt span[title],.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a')?.getAttribute?.('title') ||
    node?.querySelector?.('.catalog_name span[title],.chapter_Thats_bnt span[title],.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a')?.innerText ||
    node?.getAttribute?.('title') || node?.innerText || node?.textContent
  );
  const selectors = [
    '.chapter_item','.chapter_unit','.catalog_title','.catalog_item','.posCatalog_select',
    '.menulist-menu-title','.menulist-menu','.ncells','[id^="cur"]',
    '[onclick*="toOld"]','a[href*="chapterId="]','a[href*="knowledgeId="]','a[href*="knowledgeid="]'
  ];
  const raw = Array.from(new Set(document.querySelectorAll(selectors.join(','))));
  for (const item of raw) {
    if (taskPoint(item)) continue;
    const node = item.closest?.(
      '.chapter_item,.chapter_unit,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells,li[id^="cur"],dd[id^="cur"],[id^="cur"]'
    ) || item;
    if (taskPoint(node)) continue;
    const action = clickableOf(node);
    if (!action || !identityOf(action)) continue;
    const title = titleOf(node);
    if (!title || /^(?:返回|返回课程|课程首页|测验|章节测验|测试|作业|签到|考试)$/i.test(title)) continue;
    const rect = action.getBoundingClientRect?.();
    if (rect && (rect.width <= 0 || rect.height <= 0)) continue;
    try { node.scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
    try { action.click(); return true; } catch {}
  }
  return false;
})()
""";
        var documentVersionBeforeClick = _documentVersion;
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x) || _documentVersion != documentVersionBeforeClick;
    }

    public async Task<bool> OpenChapterAsync(ChapterItem chapter)
    {
        var targetId = JsonSerializer.Serialize(chapter.ChapterId ?? string.Empty);
        var targetUrl = JsonSerializer.Serialize(chapter.Url ?? string.Empty);
        var targetTitle = JsonSerializer.Serialize(chapter.Title ?? string.Empty);
        var script = $$"""
(() => {
  const targetId = {{targetId}};
  const targetUrl = {{targetUrl}};
  const targetTitle = {{targetTitle}};
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const clean = raw => norm(raw)
    .replace(/未完成|未看完|待完成任务点|待完成|未开始|进行中|已完成|已看完|全部完成/gi, ' ')
    .replace(/\s+[·|｜-]?\s*(?:视频|资料|文档|测验|作业|签到|考试)\s*$/i, '')
    .replace(/\s+/g,' ').trim();
  const getHref = el => {
    const raw = el?.getAttribute?.('href') || el?.href || '';
    if (!raw || /^javascript:/i.test(raw)) return '';
    try { return new URL(raw, location.href).href; } catch { return ''; }
  };
  const getIdOne = el => {
    if (!el) return '';
    const href = getHref(el);
    if (href) {
      try {
        const u = new URL(href, location.href);
        for (const key of ['chapterId','knowledgeId','knowledgeid']) {
          const value = u.searchParams.get(key);
          if (value) return value;
        }
      } catch {}
    }
    const action = el.getAttribute?.('onclick') || '';
    let m = action.match(/toOld\s*\(\s*['"][^'"]+['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (m?.[1]) return m[1];
    m = String(el.id || '').match(/^cur(.+)$/i);
    return m?.[1] || '';
  };
  const getId = el => getIdOne(el) || getIdOne(el?.querySelector?.('[id^="cur"]')) ||
                    getIdOne(el?.querySelector?.('[onclick*="toOld"]')) ||
                    getIdOne(el?.querySelector?.('a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="]'));
  const getTitle = el => {
    const t = el?.querySelector?.('.catalog_name span[title],.chapter_Thats_bnt span[title],.catalog_name,.chapter_name,.chapterText,.articlename,h4 > a') || el;
    return clean(t?.getAttribute?.('title') || t?.innerText || t?.textContent || el?.getAttribute?.('title') || '');
  };
  const isTaskPointContainer = el => {
    if (!el) return false;
    const cls = norm(el.getAttribute?.('class') || el.className || '');
    if (/\b(?:ans-attach-ct|ans-job|ans-videoquiz|task-point|taskPoint)\b/i.test(cls)) return true;
    return !!(el.getAttribute?.('data-attachment') || el.getAttribute?.('data-objectid') || el.getAttribute?.('data-object-id'));
  };
  const isTaskPointNode = el => {
    if (!el) return false;
    if (el.matches?.('.chapter_item,.chapter_unit,.catalog_title,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells,[id^="cur"]')) return false;
    if (isTaskPointContainer(el)) return true;
    const task = el.closest?.('.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-attachment],[data-objectid]');
    return isTaskPointContainer(task);
  };
  const catalogNode = el => {
    if (!el || isTaskPointNode(el)) return null;
    return el.closest?.('.chapter_item,.chapter_unit,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells,li[id^="cur"],dd[id^="cur"],[id^="cur"]') || el;
  };
  const clickable = el => {
    if (!el) return null;
    if (el.matches?.('[onclick*="toOld"],a[href],[id^="cur"][onclick],.catalog_title[onclick],.chapter_item[onclick]')) return el;
    return el.querySelector?.('[onclick*="toOld"],a[href*="chapterId="],a[href*="knowledgeId="],a[href*="knowledgeid="],a[href*="studentstudy"],a[href*="nodedetail"],[id^="cur"][onclick]') || el;
  };
  const primary = [
    '.chapter_item[onclick*="toOld"]','.chapter_item','.chapter_unit','.catalog_title[onclick*="toOld"]',
    '.catalog_item[id^="cur"]','.posCatalog_select[id^="cur"]','.menulist-menu-title[id^="cur"]',
    '.menulist-menu[id^="cur"]','[id^="cur"][onclick]','.ncells'
  ];
  const fallback = [
    'a[href*="chapterId="]','a[href*="knowledgeId="]','a[href*="knowledgeid="]',
    'a[href*="studentstudy"]','a[href*="nodedetail"]'
  ];
  let raw = Array.from(new Set(Array.from(document.querySelectorAll(primary.join(',')))));
  if (!raw.length) raw = Array.from(new Set(Array.from(document.querySelectorAll(fallback.join(',')))));
  const seen = new Set();
  for (const rawNode of raw) {
    const node = catalogNode(rawNode);
    if (!node) continue;
    const action = clickable(node);
    const id = getId(node) || getId(action);
    const href = getHref(action) || getHref(node);
    const title = getTitle(node) || getTitle(action);
    const key = id ? `id:${id}` : `url:${href}`;
    if (seen.has(key)) continue;
    seen.add(key);
    const idMatch = !!targetId && id === targetId;
    const urlMatch = !targetId && !!targetUrl && href === targetUrl;
    const titleMatch = !targetId && !targetUrl && !!targetTitle && title === clean(targetTitle);
    if (!idMatch && !urlMatch && !titleMatch) continue;
    try { node.scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
    try { action?.click?.(); return true; } catch {}
  }
  return false;
})()
""";
        var documentVersionBeforeClick = _documentVersion;
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x) || _documentVersion != documentVersionBeforeClick;
    }

    public async Task<bool> FocusFirstUnfinishedVideoTaskAsync()
    {
        const string script = """
(() => {
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const completion = el => {
    const visible = norm(el?.innerText || '');
    if (/未完成|未看完|待完成|待完成任务点|未开始|进行中|incomplete|unfinished/i.test(visible)) return false;
    if (/已完成|已看完|全部完成|\b(?:finished|completed)\b/i.test(visible)) return true;
    const markup = (el?.outerHTML || '').slice(0,3200);
    const count = markup.match(/jobUnfinishCount["']?\s*[:=]\s*["']?(\d+)/i);
    if (count) return Number(count[1]) === 0;
    const unfinished = el?.querySelector?.('.orange01,.ans-job-unfinished,.ans-job-unfinish,[class*="unfinished"],[class*="unfinish"]');
    const finished = el?.querySelector?.('.ans-job-finished,[class~="finished"]');
    if (finished && !unfinished) return true;
    if (unfinished && !finished) return false;
    return null;
  };
  const isVideoTask = el => {
    const frame = el?.querySelector?.('iframe');
    const frameEvidence = norm([
      frame?.src, frame?.getAttribute?.('src'), frame?.getAttribute?.('_src'), frame?.getAttribute?.('class'),
      frame?.getAttribute?.('title'), frame?.getAttribute?.('data-type'), frame?.getAttribute?.('type'),
      frame?.getAttribute?.('module'), frame?.getAttribute?.('data')
    ].filter(Boolean).join(' '));
    const label = norm((el?.innerText || el?.textContent || '') + ' ' + (el?.getAttribute?.('title') || '') + ' ' + frameEvidence);
    if (/章节测验|测试题|测验|作业|签到|考试|homework|exam|workid|worktype|module["']?\s*[:=]\s*["']?work/i.test(label)) return false;
    return !!el?.querySelector?.('video,[data-type*="video"],[class*="ans-video"],[class*="video"]') ||
           (!!frame && /video|richvideo|insertvideo|shipin|module["']?\s*[:=]\s*["']?video/i.test(frameEvidence)) ||
           /视频|video|shipin/i.test(norm(el?.getAttribute?.('title') || '') + ' ' + norm(el?.className || ''));
  };
  const blocks = Array.from(new Set(Array.from(document.querySelectorAll(
    '.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-type*="video"],[class*="ans-video"]'
  )))).filter(isVideoTask);
  if (!blocks.length) return false;
  // 严格按页面顺序选择第一条未明确完成的视频；未知项必须先核验，不能越过。
  const target = blocks.find(x => completion(x) !== true);
  if (!target) return false;
  try { target.scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
  const clickTarget = target.querySelector?.(
    '[role="tab"],button,a[href],[onclick],.catalog_title,.task-title,.title,iframe,.ans-job-icon'
  ) || target;
  if (clickTarget && !clickTarget.matches?.('video')) {
    try { clickTarget.click?.(); return true; } catch {}
  }
  return false;
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x);
    }

    /// <summary>
    /// 只点击学习通播放器明确提供的“下一视频”控件。
    /// 这里只提交真实页面点击，是否真的换视频由调用方再次读取 PlayerSnapshot 确认。
    /// </summary>
    public async Task<bool> ClickNativeNextVideoControlAsync()
    {
        const string script = """
(() => {
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const visible = el => {
    if (!el) return false;
    try {
      const r = el.getBoundingClientRect?.();
      if (r && (r.width <= 0 || r.height <= 0)) return false;
    } catch {}
    return !el.disabled && el.getAttribute?.('aria-disabled') !== 'true';
  };
  const textOf = el => norm([
    el?.getAttribute?.('title'), el?.getAttribute?.('aria-label'), el?.getAttribute?.('data-title'),
    el?.innerText, el?.textContent, el?.className
  ].filter(Boolean).join(' '));
  const looksNext = el => {
    const text = textOf(el);
    const cls = norm(el?.className || '');
    if (!text || /上一|previous|prev/i.test(text)) return false;
    if (/下一页|下一(?:节|章|个任务|题)|测试|测验|作业|签到|考试|next\s*(?:page|chapter|lesson|task|quiz|exam)/i.test(text)) return false;
    return /下一(?:个)?视频|下一个视频|next\s*video/i.test(text) ||
           /(?:^|[-_\s])(?:vjs-)?next-(?:video-?)?(?:control|button|btn)(?:[-_\s]|$)/i.test(cls) ||
           /(?:^|[-_\s])nextvideo(?:btn|button)?(?:[-_\s]|$)/i.test(cls);
  };
  const videos = Array.from(document.querySelectorAll('video'));
  const playerFrames = Array.from(document.querySelectorAll('iframe[src]')).filter(frame => {
    const src = norm(frame.getAttribute?.('src') || frame.src || '');
    const cls = norm(frame.className || '');
    const holder = frame.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]');
    return !!holder || /video|play|objectid|media|shipin/i.test(src + ' ' + cls);
  });
  // 学习通常把真实 <video> 放进子 iframe，而“下一节”按钮留在父页面。
  // 父页面本身没有 <video> 时也必须继续搜索，但只有存在明确播放器 iframe 才允许点击，避免在无关页面误点“下一个”。
  if (!videos.length && !playerFrames.length) return false;
  const score = v => {
    const r = v.getBoundingClientRect?.() || { width:0, height:0 };
    return (r.width > 10 && r.height > 10 ? 100000 : 0) + (!v.paused ? 10000 : 0) + (v.ended ? 5000 : 0);
  };
  const current = videos.length ? videos.slice().sort((a,b) => score(b)-score(a))[0] : null;
  const roots = [];
  let node = current;
  for (let i = 0; node && i < 6; i++, node = node.parentElement) roots.push(node);
  for (const frame of playerFrames) {
    let frameNode = frame;
    for (let i = 0; frameNode && i < 6; i++, frameNode = frameNode.parentElement) roots.push(frameNode);
  }
  const selectors = [
    '.vjs-next-control','.vjs-next-button','.next-video','.nextVideo','.next-video-btn','.nextVideoBtn',
    'button[title*="下一视频"]','button[aria-label*="下一视频"]',
    '[role="button"][title*="下一视频"]','[role="button"][aria-label*="下一视频"]'
  ];
  const candidates = [];
  for (const root of roots) {
    for (const sel of selectors) {
      try { for (const el of root.querySelectorAll?.(sel) || []) candidates.push(el); } catch {}
    }
  }
  // 某些学习通模板把“下一视频”放在播放器 iframe 外层/页面底部，不能只查 video 的祖先。
  for (const sel of selectors) {
    try { for (const el of document.querySelectorAll?.(sel) || []) candidates.push(el); } catch {}
  }
  // 文本型候选只在播放器/播放器 iframe 附近搜索，并仍须明确写着“下一视频”。
  for (const root of roots) {
    try {
      for (const el of root.querySelectorAll?.('button,a,[role="button"],[onclick]') || []) {
        if (looksNext(el)) candidates.push(el);
      }
    } catch {}
  }
  for (const el of Array.from(new Set(candidates))) {
    if (!visible(el) || !looksNext(el)) continue;
    try { el.scrollIntoView?.({ block:'nearest', behavior:'auto' }); } catch {}
    try {
      el.dispatchEvent?.(new MouseEvent('mousedown', { bubbles:true, cancelable:true, view:window }));
      el.dispatchEvent?.(new MouseEvent('mouseup', { bubbles:true, cancelable:true, view:window }));
    } catch {}
    try { el.click?.(); return true; } catch {}
  }
  return false;
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x);
    }

    /// <summary>
    /// 当普通章节节点 click 只改变目录高亮却没有切换真实播放器时，
    /// 再调用页面自身 onclick 中的 toOld(...)。必须保留网页原始参数个数；
    /// 学习通常见模板会使用 toOld(courseId, knowledgeId, clazzId, 0)，少传第 4 参数可能只高亮目录而不切学习页。
    /// 调用方仍以真实 PlayerSnapshot 变化作为成功标准。
    /// </summary>
    public async Task<bool> InvokeNativeChapterActionAsync(ChapterItem chapter)
    {
        var targetId = JsonSerializer.Serialize(chapter.ChapterId ?? string.Empty);
        var targetTitle = JsonSerializer.Serialize(chapter.Title ?? string.Empty);
        var script = $$"""
(() => {
  const targetId = {{targetId}};
  const targetTitle = {{targetTitle}};
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const getId = el => {
    if (!el) return '';
    const own = String(el.id || '').match(/^cur(.+)$/i);
    if (own?.[1]) return own[1];
    const action = el.getAttribute?.('onclick') || '';
    let m = action.match(/toOld\s*\(\s*['"][^'"]+['"]\s*,\s*['"]([^'"]+)['"]/i);
    if (m?.[1]) return m[1];
    const href = el.getAttribute?.('href') || el.href || '';
    try {
      const u = new URL(href, location.href);
      for (const key of ['chapterId','knowledgeId','knowledgeid']) {
        const value = u.searchParams.get(key);
        if (value) return value;
      }
    } catch {}
    return '';
  };
  const titleOf = el => norm(el?.getAttribute?.('title') || el?.innerText || el?.textContent || '');
  const nodes = Array.from(document.querySelectorAll(
    '[onclick*="toOld"],[id^="cur"],.chapter_item,.catalog_title,.catalog_item,.posCatalog_select,.menulist-menu-title,.menulist-menu,.ncells'
  ));
  for (const node of nodes) {
    const nested = node.matches?.('[onclick*="toOld"]') ? node : node.querySelector?.('[onclick*="toOld"]');
    const actionEl = nested || node;
    const id = getId(actionEl) || getId(node);
    const title = titleOf(node) || titleOf(actionEl);
    if (targetId ? id !== targetId : (targetTitle && !title.includes(targetTitle))) continue;
    const action = actionEl.getAttribute?.('onclick') || node.getAttribute?.('onclick') || '';
    const call = action.match(/toOld\s*\(([^)]*)\)/i);
    if (!call) continue;
    const rawArgs = call[1];
    const args = [];
    const token = /'((?:\\.|[^'\\])*)'|"((?:\\.|[^"\\])*)"|(-?\d+(?:\.\d+)?)|\b(true|false|null|undefined)\b/g;
    let part;
    while ((part = token.exec(rawArgs))) {
      if (part[1] !== undefined || part[2] !== undefined) {
        const text = (part[1] !== undefined ? part[1] : part[2])
          .replace(/\\(['"\\])/g, '$1');
        args.push(text);
      } else if (part[3] !== undefined) {
        args.push(Number(part[3]));
      } else {
        args.push(part[4] === 'true' ? true : part[4] === 'false' ? false : null);
      }
    }
    if (args.length < 3) continue;
    const scopes = [];
    try { scopes.push(window); } catch {}
    try { if (parent && parent !== window) scopes.push(parent); } catch {}
    try { if (top && top !== window && top !== parent) scopes.push(top); } catch {}
    for (const scope of scopes) {
      try {
        if (typeof scope.toOld === 'function') {
          scope.toOld.apply(scope, args);
          return true;
        }
      } catch {}
    }
    try {
      if (typeof actionEl.onclick === 'function') {
        actionEl.onclick.call(actionEl, new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
        return true;
      }
    } catch {}
    try { actionEl.click?.(); return true; } catch {}
  }
  return false;
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x);
    }

    /// <summary>
    /// 从当前已经登录的学习页生成“同一课程上下文 + 目标章节 ID”的真实学习页地址。
    /// 不新造 enc/cpi/openc 等鉴权参数，只原样保留当前地址已有参数并替换章节 ID。
    /// 这用于 DOM click/toOld 都只改高亮却不切播放器时的最终真实导航路径。
    /// </summary>
    public async Task<string?> BuildContextPreservingChapterUrlAsync(ChapterItem chapter)
    {
        if (string.IsNullOrWhiteSpace(chapter.ChapterId) || _disposed)
            return null;

        var targetId = JsonSerializer.Serialize(chapter.ChapterId);
        var script = $$"""
(() => {
  const targetId = {{targetId}};
  if (!targetId) return '';
  const tryBuild = raw => {
    try {
      const u = new URL(raw);
      if (!/(?:^|\.)chaoxing\.com$/i.test(u.hostname)) return '';
      if (/\/mycourse\/studentstudy$/i.test(u.pathname) || /\/mycourse\/studentstudy\//i.test(u.pathname)) {
        u.searchParams.set('chapterId', targetId);
        if (u.searchParams.has('knowledgeId')) u.searchParams.set('knowledgeId', targetId);
        if (u.searchParams.has('knowledgeid')) u.searchParams.set('knowledgeid', targetId);
        return u.href;
      }
      // 新版课程页只有已经明确携带 knowledge/chapter 参数时才替换，禁止猜测新版路由结构。
      if (/\/mooc2-ans\/mycourse\/stu/i.test(u.pathname)) {
        if (u.searchParams.has('knowledgeid')) { u.searchParams.set('knowledgeid', targetId); return u.href; }
        if (u.searchParams.has('knowledgeId')) { u.searchParams.set('knowledgeId', targetId); return u.href; }
        if (u.searchParams.has('chapterId')) { u.searchParams.set('chapterId', targetId); return u.href; }
      }
    } catch {}
    return '';
  };
  const candidates = [];
  try { candidates.push(location.href); } catch {}
  try { if (parent && parent !== window) candidates.push(parent.location.href); } catch {}
  try { if (top && top !== window) candidates.push(top.location.href); } catch {}
  for (const raw of candidates) {
    const built = tryBuild(raw);
    if (built) return built;
  }
  return '';
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<string>(script, x => !string.IsNullOrWhiteSpace(x));
        return results.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    /// <summary>
    /// 优先在当前章节页面内部寻找“当前已结束视频之后”的下一个真实视频任务点。
    /// 只对真实 video/视频任务容器执行滚动、点击或 play()，不修改平台完成状态。
    /// 返回 true 只表示已提交真实页面动作；调用方仍必须通过 PlayerSnapshot 核验播放器是否真的变化。
    /// </summary>
    public async Task<bool> AdvanceToNextVideoTaskAsync(
        string? currentMediaId,
        string? currentSource,
        string? nextMediaId = null,
        string? nextDocumentUrl = null,
        string? nextSource = null)
    {
        var targetMediaId = JsonSerializer.Serialize(currentMediaId ?? string.Empty);
        var targetSource = JsonSerializer.Serialize(currentSource ?? string.Empty);
        var targetNextMediaId = JsonSerializer.Serialize(nextMediaId ?? string.Empty);
        var targetNextDocumentUrl = JsonSerializer.Serialize(nextDocumentUrl ?? string.Empty);
        var targetNextSource = JsonSerializer.Serialize(nextSource ?? string.Empty);
        var script = $$"""
(() => {
  const currentMediaId = {{targetMediaId}};
  const currentSource = {{targetSource}};
  const nextMediaId = {{targetNextMediaId}};
  const nextDocumentUrl = {{targetNextDocumentUrl}};
  const nextSource = {{targetNextSource}};
  const norm = s => (s || '').replace(/\s+/g,' ').trim();
  const attr = (el, name) => el?.getAttribute?.(name) || '';
  const taskBlock = v => v?.closest?.('.ans-video,.ans-attach-ct,.ans-job,.ans-videoquiz,.video-box,.videoBox,.video-container,.task-point,.taskPoint,[data-objectid],[data-object-id]') || v?.parentElement || null;
  const mediaIdOf = v => {
    const block = taskBlock(v);
    return [
      attr(v,'data-objectid'), attr(v,'data-object-id'), attr(v,'data-id'), attr(v,'data-mid'),
      attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
      attr(block,'data-attachment'), attr(block,'data-attach-id')
    ].map(norm).find(Boolean) || '';
  };
  const sourceOf = v => norm(v?.currentSrc || v?.src || '');
  const completion = el => {
    const visible = norm(el?.innerText || '');
    if (/未完成|未看完|待完成|待完成任务点|未开始|进行中|incomplete|unfinished/i.test(visible)) return false;
    if (/已完成|已看完|全部完成|\b(?:finished|completed)\b/i.test(visible)) return true;
    const markup = (el?.outerHTML || '').slice(0,3200);
    const count = markup.match(/jobUnfinishCount["']?\s*[:=]\s*["']?(\d+)/i);
    if (count) return Number(count[1]) === 0;
    const unfinished = el?.querySelector?.('.orange01,.ans-job-unfinished,.ans-job-unfinish,[class*="unfinished"],[class*="unfinish"]');
    const finished = el?.querySelector?.('.ans-job-finished,[class~="finished"]');
    if (finished && !unfinished) return true;
    if (unfinished && !finished) return false;
    return null;
  };
  const videos = Array.from(document.querySelectorAll('video'));
  const blocks = Array.from(new Set(Array.from(document.querySelectorAll(
    '.ans-attach-ct,.ans-job,.ans-videoquiz,.task-point,.taskPoint,[data-type*="video"],[class*="ans-video"]'
  ))));
  const frameEvidenceOf = block => {
    const frame = block?.querySelector?.('iframe');
    return norm([
      frame?.src, attr(frame,'src'), attr(frame,'_src'), attr(frame,'class'), attr(frame,'title'),
      attr(frame,'data-type'), attr(frame,'type'), attr(frame,'module'), attr(frame,'data')
    ].filter(Boolean).join(' '));
  };
  const isVideoBlock = block => {
    const evidence = norm((block?.innerText || block?.textContent || '') + ' ' + attr(block,'title') + ' ' +
                          norm(block?.className || '') + ' ' + frameEvidenceOf(block));
    if (/章节测验|测试题|测验|作业|签到|考试|homework|exam|workid|worktype|module["']?\s*[:=]\s*["']?work/i.test(evidence)) return false;
    return !!block?.querySelector?.('video,[data-type*="video"],[class*="ans-video"],[class*="video"]') ||
           /视频|video|richvideo|insertvideo|shipin|module["']?\s*[:=]\s*["']?video/i.test(evidence);
  };
  if (!videos.length && !blocks.length) return 0;

  // 当前视频可能在子 iframe，而任务顺序只存在于父页面。调用方能从合并后的扫描结果
  // 明确给出下一视频时，先用目标 iframe URL / source / mediaId 点击对应任务块，
  // 避免父页面无法反推“当前子 iframe 位于第几个任务点”而停住。
  const activateVideo = v => {
    const block = taskBlock(v);
    const clickTarget = block?.querySelector?.('.ans-job-icon,[role="tab"],button,a[href],[onclick],.task-title,.title');
    if (clickTarget && !clickTarget.matches?.('video')) {
      try { clickTarget.click?.(); } catch {}
    }
    try {
      const request = v.play();
      if (request && typeof request.catch === 'function') request.catch(() => {});
      return 2;
    } catch { return 1; }
  };
  const strongTarget = !!(nextDocumentUrl || nextSource);
  if (nextMediaId || strongTarget) {
    const exactVideo = videos.find(v =>
      (nextSource && sourceOf(v) === norm(nextSource)) ||
      (nextDocumentUrl && norm(location.href) === norm(nextDocumentUrl)) ||
      (!strongTarget && nextMediaId && mediaIdOf(v) === nextMediaId));
    if (exactVideo &&
        (!currentSource || sourceOf(exactVideo) !== norm(currentSource)) &&
        (!currentMediaId || strongTarget || mediaIdOf(exactVideo) !== currentMediaId)) {
      try { (taskBlock(exactVideo) || exactVideo).scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
      return activateVideo(exactVideo);
    }

    const targetBlockMediaId = block => [
      attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
      attr(block,'data-attachment'), attr(block,'data-attach-id'),
      attr(block?.querySelector?.('iframe'),'objectid'), attr(block?.querySelector?.('iframe'),'data-objectid'),
      attr(block?.querySelector?.('iframe'),'mid'),
      attr(block?.querySelector?.('[data-objectid],[data-object-id],[data-id],[data-mid]'),'data-objectid'),
      attr(block?.querySelector?.('[data-objectid],[data-object-id],[data-id],[data-mid]'),'data-object-id')
    ].map(norm).find(Boolean) || '';
    const exactBlock = blocks.find(block => {
      const frame = block.querySelector?.('iframe');
      const frameSource = norm(frame?.src || attr(frame,'src') || attr(frame,'_src'));
      return (nextDocumentUrl && frameSource === norm(nextDocumentUrl)) ||
             (nextSource && frameSource === norm(nextSource)) ||
             (!strongTarget && nextMediaId && targetBlockMediaId(block) === nextMediaId);
    });
    if (exactBlock) {
      try { exactBlock.scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
      const clickTarget = exactBlock.querySelector?.('[role="tab"],button,a[href],[onclick],.task-title,.title,iframe,.ans-job-icon') || exactBlock;
      if (clickTarget) {
        try { clickTarget.click?.(); return 1; } catch {}
      }
      const embeddedVideo = exactBlock.querySelector?.('video');
      if (embeddedVideo) return activateVideo(embeddedVideo);
    }
  }

  let currentIndex = -1;
  // source 比容器 ID 更能区分真实媒体；平台可能复用同一个 data-objectid。
  if (currentSource) currentIndex = videos.findIndex(v => sourceOf(v) === norm(currentSource));
  if (currentIndex < 0 && currentMediaId) currentIndex = videos.findIndex(v => mediaIdOf(v) === currentMediaId);
  if (currentIndex < 0) {
    const ended = videos
      .map((v, i) => ({ v, i, ended: !!v.ended, t: Number.isFinite(v.currentTime) ? v.currentTime : 0 }))
      .filter(x => x.ended)
      .sort((a,b) => b.t - a.t)[0];
    if (ended) currentIndex = ended.i;
  }
  if (currentIndex >= 0) {
    for (let i = currentIndex + 1; i < videos.length; i++) {
      const v = videos[i];
      const block = taskBlock(v);
      // 顺序续播可以接纳状态未知的视频，但跳过平台明确标记为已完成的视频。
      if (completion(block) === true) continue;
      // 同一媒体身份/同一 source 绝不作为“下一视频”，避免 ended 后从头重播。
      const candidateSource = sourceOf(v);
      if (currentSource && candidateSource && candidateSource === norm(currentSource)) continue;
      if (!currentSource && currentMediaId && mediaIdOf(v) === currentMediaId) continue;
      try { (block || v).scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
      const clickTarget = block?.querySelector?.('.ans-job-icon,[role="tab"],button,a[href],[onclick],.task-title,.title');
      if (clickTarget && !clickTarget.matches?.('video')) {
        try { clickTarget.click?.(); } catch {}
      }
      try {
        const request = v.play();
        if (request && typeof request.catch === 'function') request.catch(() => {});
        return 2;
      } catch {
        return 1;
      }
    }
  }

  // 有些模板每个视频位于独立 iframe。父页面只有任务块，需要按块顺序跳过测试并找到下一视频。
  const currentVideo = currentIndex >= 0 ? videos[currentIndex] : null;
  const currentBlock = taskBlock(currentVideo);
  let currentBlockIndex = currentBlock ? blocks.indexOf(currentBlock) : -1;
  const blockMediaId = block => [
    attr(block,'data-objectid'), attr(block,'data-object-id'), attr(block,'data-id'), attr(block,'data-mid'),
    attr(block,'data-attachment'), attr(block,'data-attach-id'),
    attr(block?.querySelector?.('iframe'),'objectid'), attr(block?.querySelector?.('iframe'),'data-objectid'),
    attr(block?.querySelector?.('iframe'),'mid'),
    attr(block?.querySelector?.('[data-objectid],[data-object-id],[data-id],[data-mid]'),'data-objectid'),
    attr(block?.querySelector?.('[data-objectid],[data-object-id],[data-id],[data-mid]'),'data-object-id')
  ].map(norm).find(Boolean) || '';
  if (currentBlockIndex < 0 && currentMediaId)
    currentBlockIndex = blocks.findIndex(block => blockMediaId(block) === currentMediaId);
  if (currentBlockIndex < 0 && currentSource) {
    currentBlockIndex = blocks.findIndex(block => {
      const media = block.querySelector?.('video,iframe');
      return norm(media?.currentSrc || media?.src || attr(media,'src') || attr(media,'_src')) === norm(currentSource);
    });
  }
  if (currentBlockIndex >= 0) {
    for (let i = currentBlockIndex + 1; i < blocks.length; i++) {
      const block = blocks[i];
      if (!isVideoBlock(block) || completion(block) === true) continue;
      try { block.scrollIntoView?.({ block:'center', behavior:'auto' }); } catch {}
      const clickTarget = block.querySelector?.('[role="tab"],button,a[href],[onclick],.task-title,.title,iframe,.ans-job-icon') || block;
      if (clickTarget) {
        try { clickTarget.click?.(); return 1; } catch {}
      }
      const embeddedVideo = block.querySelector?.('video');
      if (embeddedVideo) {
        try {
          const request = embeddedVideo.play();
          if (request && typeof request.catch === 'function') request.catch(() => {});
          return 2;
        } catch { return 1; }
      }
    }
  }
  return 0;
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<int>(script, x => x > 0);
        return results.Any(x => x > 0);
    }

    public async Task<bool> RestorePlaybackRateUsingUiAsync(double rate, PlayerSnapshot? player = null)
    {
        if (!double.IsFinite(rate) || rate <= 0) return false;
        player ??= await GetPlayerSnapshotAsync();
        if (!player.Found || player.DomIndex < 0 || string.IsNullOrWhiteSpace(player.DocumentUrl)) return false;
        rate = Math.Round(rate, 2);
        var invariant = rate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var targetDocumentUrl = JsonSerializer.Serialize(player.DocumentUrl);
        var targetSource = JsonSerializer.Serialize(player.Source);
        var targetDomIndex = player.DomIndex;
        var script = $$"""
(() => {
  if (location.href !== {{targetDocumentUrl}}) return false;
  const video = Array.from(document.querySelectorAll('video'))[{{targetDomIndex}}];
  if (!video || ({{targetSource}} && (video.currentSrc || video.src) !== {{targetSource}})) return false;
  const root = video.closest?.('.video-js,.ans-video,.video-container') || video.parentElement;
  if (!root) return false;
  const usable = el => {
    const r = el.getBoundingClientRect();
    const s = getComputedStyle(el);
    return !el.disabled && el.getAttribute?.('aria-disabled') !== 'true' &&
      r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
  };
  const target = {{invariant}};
  const normalSpeed = () => {
    try { video.defaultPlaybackRate = 1; video.playbackRate = 1; } catch {}
    return false;
  };
  if (target === 1) { normalSpeed(); return video.playbackRate === 1; }
  const labels = [
    String(target) + 'x',
    String(target) + 'X',
    String(target) + '倍',
    (Number.isInteger(target) ? target.toFixed(1) : String(target)) + 'x'
  ];
  const norm = s => (s || '').replace(/\s+/g,'').toLowerCase();

  for (const select of Array.from(root.querySelectorAll('.vjs-playback-rate select,select[aria-label*="倍速"],select[aria-label*="速度"],select[aria-label*="playback rate" i]'))) {
    if (!usable(select)) continue;
    for (const option of Array.from(select.options || [])) {
      const txt = norm(option.textContent);
      const val = Number(option.value);
      const textMatch = labels.some(x => txt === norm(x));
      const valueMatch = Number.isFinite(val) && Math.abs(val-target) < 0.001;
      if (!option.disabled && (textMatch || valueMatch)) {
        select.value = option.value;
        select.dispatchEvent(new Event('change', {bubbles:true}));
        return true;
      }
    }
  }

  const candidates = Array.from(root.querySelectorAll('.vjs-playback-rate .vjs-menu-item,[role="menuitemradio"][aria-label*="倍速"],[role="menuitemradio"][aria-label*="speed" i]'));
  for (const el of candidates) {
    const txt = norm(el.textContent);
    if (!labels.some(x => txt === norm(x))) continue;
    const r = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    const visible = r.width > 0 && r.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    if (!visible || !usable(el)) continue;
    try { el.click(); return true; } catch {}
  }

  return normalSpeed();
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<bool>(script, x => x);
        return results.Any(x => x);
    }

    public async Task<bool> SeekAsync(double positionSeconds)
    {
        if (!double.IsFinite(positionSeconds)) return false;
        var p = Math.Max(0, positionSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var script = $$"""
(() => {
  const target = {{p}};
  const videos = Array.from(document.querySelectorAll('video'));
  for (const v of videos) {
    if (!Number.isFinite(v.duration) || v.duration <= 0) continue;
    const safe = Math.min(target, Math.max(0, v.duration - 1));
    try { v.currentTime = safe; return true; } catch {}
  }
  return false;
})()
""";
        var results = await ExecuteAcrossDocumentsAsync<bool>(script);
        return results.Any(x => x);
    }

    private void OnFrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        RegisterFrame(e.Frame);
    }

    private void RegisterFrame(CoreWebView2Frame frame)
    {
        if (_disposed || _frames.Contains(frame))
            return;

        _frames.Add(frame);
        frame.Destroyed += (_, _) => _frames.Remove(frame);
        frame.FrameCreated += (_, e) => RegisterFrame(e.Frame);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        => _documentVersion++;

    private async Task<List<T>> ExecuteAcrossDocumentsAsync<T>(string script, Func<T, bool>? stopWhen = null)
    {
        var result = new List<T>();
        if (_disposed) return result;
        var documentVersion = _documentVersion;

        try
        {
            var json = await _webView.ExecuteScriptAsync(script);
            if (_disposed || documentVersion != _documentVersion) return new List<T>();
            if (TryDeserialize(json, out T? value) && value is not null)
            {
                result.Add(value);
                if (stopWhen?.Invoke(value) == true) return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("CX-JS-MAIN", $"主文档脚本执行失败：{ex.Message}");
        }

        foreach (var frame in _frames.ToArray())
        {
            if (_disposed || documentVersion != _documentVersion) return new List<T>();
            try
            {
                if (frame.IsDestroyed() != 0)
                    continue;

                var json = await frame.ExecuteScriptAsync(script);
                if (_disposed || documentVersion != _documentVersion) return new List<T>();
                if (TryDeserialize(json, out T? value) && value is not null)
                {
                    result.Add(value);
                    if (stopWhen?.Invoke(value) == true) return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("CX-JS-FRAME", $"子框架脚本执行失败：{ex.Message}");
            }
        }

        return result;
    }

    private bool TryDeserialize<T>(string json, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(json) || json is "null" or "undefined")
            return false;

        try
        {
            value = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            return value is not null;
        }
        catch (JsonException ex)
        {
            _logger.Debug("CX-JSON", $"脚本返回值解析失败：{ex.Message}");
            return false;
        }
    }

    private static string GetCourseIdentity(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url ?? string.Empty;
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = Uri.UnescapeDataString(parts[0]);
            if (!key.Equals("courseId", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("courseId_", StringComparison.OrdinalIgnoreCase)) continue;
            var value = Uri.UnescapeDataString(parts[1]);
            if (!string.IsNullOrWhiteSpace(value))
                return $"id:{value}";
        }
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + uri.Query;
    }

    private static TaskType ParseTaskType(string? value)
        => Enum.TryParse<TaskType>(value, true, out var type) ? type : TaskType.Unknown;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _documentVersion++;
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.FrameCreated -= OnFrameCreated;
            _webView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
        }
        _frames.Clear();
    }

    private sealed class CourseDto
    {
        public string ProgressText { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    private sealed class ChapterDto
    {
        public string ProgressText { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ChapterId { get; set; } = string.Empty;
        public string DocumentUrl { get; set; } = string.Empty;
        public bool IsSyntheticUrl { get; set; }
        public bool IsActive { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool CompletionKnown { get; set; }
        public int Order { get; set; }
    }

    private sealed class VideoTaskDto
    {
        public int Index { get; set; }
        public string TaskKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ChapterId { get; set; } = string.Empty;
        public string ChapterTitle { get; set; } = string.Empty;
        public string DocumentUrl { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string MediaId { get; set; } = string.Empty;
        public int DomIndex { get; set; } = -1;
        public bool IsVisible { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsCompleted { get; set; }
        public bool CompletionKnown { get; set; }
    }

    private sealed class PageDto
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public bool HasManualIntervention { get; set; }
        public string ManualInterventionReason { get; set; } = string.Empty;
        public string SelectorSummary { get; set; } = string.Empty;
    }
}
