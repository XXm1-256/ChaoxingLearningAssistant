// Runs the actual JavaScript embedded in ChaoxingAdapter.cs against small DOM fixtures.
// Node's DOM fixtures do not replace Windows/WebView2 integration validation.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(fileURLToPath(new URL('../src/ChaoxingLearningAssistant/Chaoxing/ChaoxingAdapter.cs', import.meta.url)), 'utf8');
function script(method) {
  const start = source.indexOf(` ${method}(`);
  assert.ok(start >= 0, `Missing method ${method}`);
  const match = source.slice(start).match(/const string script = """\r?\n([\s\S]*?)\r?\n""";/);
  assert.ok(match, `Missing script ${method}`);
  return match[1];
}

function interpolatedScript(method, replacements = {}) {
  const start = source.indexOf(` ${method}(`);
  assert.ok(start >= 0, `Missing method ${method}`);
  const match = source.slice(start).match(/var script = \$\$"""\r?\n([\s\S]*?)\r?\n""";/);
  assert.ok(match, `Missing interpolated script ${method}`);
  let code = match[1];
  const values = {
    targetNextMediaId: '',
    targetNextDocumentUrl: '',
    targetNextSource: '',
    ...replacements,
  };
  for (const [key, value] of Object.entries(values)) {
    code = code.split(`{{${key}}}`).join(JSON.stringify(value));
  }
  return code;
}
function evaluate(method, nodes) {
  return vm.runInNewContext(script(method), {
    document: { querySelectorAll: () => nodes },
    location: { href: 'https://example.test/course/' }, URL, URLSearchParams,
  }, { timeout: 1000 });
}
function chapter(status) {
  const text = `第一节 视频 ${status}`;
  const container = { innerText: text, className: '', getAttribute: () => '' };
  return { innerText: '第一节', href: 'https://example.test/studentstudy?chapterId=1',
    closest: () => container, getAttribute: () => '' };
}

test('course scanner excludes 返回课程 and keeps the real course card title', () => {
  const back = {
    href: 'https://mooc1.chaoxing.com/mycourse/studentcourse?courseId=course-a',
    innerText: '返回课程', textContent: '返回课程', parentElement: null,
    getAttribute: key => key === 'title' ? '返回课程' : '',
    closest: () => null,
  };
  const card = {
    querySelector: selector => selector.includes('.course-name')
      ? { innerText: '中国现代文学', getAttribute: () => '中国现代文学' }
      : null,
  };
  const course = {
    href: 'https://mooc1.chaoxing.com/mycourse/studentcourse?courseId=course-b',
    innerText: '进入课程', textContent: '进入课程', parentElement: card,
    getAttribute: key => key === 'title' ? '' : '',
    closest: () => card,
  };
  const result = vm.runInNewContext(script('ScanCoursesAsync'), {
    document: { querySelectorAll: () => [back, course] },
    location: { href: 'https://mooc1.chaoxing.com/space/index' }, URL,
  }, { timeout: 1000 });
  assert.equal(result.length, 1);
  assert.equal(result[0].title, '中国现代文学');
  assert.match(result[0].url, /course-b/);
});

test('course scanner does not turn a nearby 提示 heading into a course', () => {
  const wrapper = {
    querySelector: selector => selector.includes('h3')
      ? { innerText: '提示', getAttribute: () => '提示' }
      : null,
  };
  const back = {
    href: 'https://mooc1.chaoxing.com/mycourse/studentcourse?courseId=course-a',
    innerText: '返回课程', textContent: '返回课程', parentElement: wrapper,
    getAttribute: key => key === 'title' ? '返回课程' : '',
    closest: () => wrapper,
  };

  const result = vm.runInNewContext(script('ScanCoursesAsync'), {
    document: { querySelectorAll: () => [back] },
    location: { href: 'https://mooc1.chaoxing.com/mycourse/studentstudy?courseId=course-a' }, URL,
  }, { timeout: 1000 });

  assert.equal(result.length, 0);
});

test('course scanner reads a title node from newer course-card markup', () => {
  const title = { innerText: '大学语文', getAttribute: key => key === 'title' ? '大学语文' : '' };
  const card = { querySelector: selector => selector.includes('.title') ? title : null };
  const course = {
    href: 'https://mooc1.chaoxing.com/mycourse/studentcourse?courseId=course-c',
    innerText: '进入课程', textContent: '进入课程', parentElement: card,
    getAttribute: () => '', closest: () => card,
  };
  const result = vm.runInNewContext(script('ScanCoursesAsync'), {
    document: { querySelectorAll: () => [course] },
    location: { href: 'https://mooc1.chaoxing.com/visit/courses' }, URL,
  }, { timeout: 1000 });

  assert.equal(result.length, 1);
  assert.equal(result[0].title, '大学语文');
});

test('course scanner recovers cards that expose ids but only use a JavaScript button', () => {
  const title = { innerText: '外国文学', textContent: '外国文学', getAttribute: () => '' };
  const courseInput = { value: 'course-d', getAttribute: () => 'course-d' };
  const classInput = { value: 'class-d', getAttribute: () => 'class-d' };
  const card = {
    getAttribute: () => '',
    querySelector: selector => selector.includes('input[name="courseId"') ? courseInput
      : selector.includes('input[name="classId"') ? classInput
      : selector.includes('.course-name') ? title : null,
  };
  const result = vm.runInNewContext(script('ScanCoursesAsync'), {
    document: { querySelectorAll: selector => selector === 'a[href]' ? [] : [card] },
    location: {
      href: 'https://i.chaoxing.com/base', hostname: 'i.chaoxing.com', origin: 'https://i.chaoxing.com'
    }, URL, URLSearchParams,
  }, { timeout: 1000 });

  assert.equal(result.length, 1);
  assert.equal(result[0].title, '外国文学');
  assert.match(result[0].url, /courseId=course-d/);
  assert.match(result[0].url, /clazzid=class-d/);
});
for (const [status, completed] of [
  ['未完成', false], ['未看完', false], ['not completed', false], ['unfinished', false],
  ['已完成', true], ['已看完', true], ['completed', true], ['完成度 0%', false],
]) {
  test(`chapter status: ${status}`, () => {
    const result = evaluate('ScanChaptersAsync', [chapter(status)]);
    assert.equal(result.length, 1);
    assert.equal(result[0].taskType, 'Video');
    assert.equal(result[0].isCompleted, completed);
  });
}

function video(overrides = {}) {
  return { currentTime: 25, duration: 100, playbackRate: 1, paused: true, ended: false,
    readyState: 4, currentSrc: 'video.mp4', src: '',
    getBoundingClientRect: () => ({ width: 640, height: 360 }), ...overrides };
}
test('missing player gives a plain not-found snapshot', () => {
  assert.equal(evaluate('GetPlayerSnapshotAsync', []).found, false);
});
test('unloaded or non-finite media metadata stays JSON-safe', () => {
  const result = evaluate('GetPlayerSnapshotAsync', [video({ duration: Infinity, currentTime: NaN, playbackRate: Infinity })]);
  assert.equal(result.duration, 0);
  assert.equal(result.currentTime, 0);
  assert.equal(result.playbackRate, 1);
  assert.ok(!JSON.stringify(result).includes('null'));
});
test('visible player wins over hidden long media in the same document', () => {
  const hidden = video({ duration: 9000, currentSrc: 'hidden.mp4', getBoundingClientRect: () => ({ width: 0, height: 0 }) });
  assert.equal(evaluate('GetPlayerSnapshotAsync', [hidden, video()]).source, 'video.mp4');
});
test('player snapshot exposes stable media and document evidence for UI synchronization', () => {
  const block = {
    getAttribute: key => key === 'data-objectid' ? 'object-77' : (key === 'data-title' ? '真实视频标题' : ''),
    querySelector: () => null,
  };
  const v = video({
    currentSrc: 'https://cdn.example/video-77.mp4',
    closest: () => block,
    getAttribute: () => '',
  });
  const result = evaluate('GetPlayerSnapshotAsync', [v]);
  assert.equal(result.mediaId, 'object-77');
  assert.equal(result.documentUrl, 'https://example.test/course/');
  assert.equal(result.videoTitle, '真实视频标题');
});
test('play request returns a number, never a Promise, and targets one video', () => {
  const calls = [];
  const first = video({ play: () => { calls.push('first'); return Promise.resolve(); } });
  const second = video({ play: () => { calls.push('second'); return Promise.resolve(); } });
  assert.equal(evaluate('PlayVideoAsync', [first, second]), 1);
  assert.deepEqual(calls, ['first']);
});
test('rejected browser play requests do not escape as unhandled JS errors', async () => {
  const result = evaluate('PlayVideoAsync', [video({ play: () => Promise.reject(new Error('NotAllowedError')) })]);
  assert.equal(result, 1); // Submitted only; C# verifies actual playback separately.
  await new Promise(resolve => setImmediate(resolve));
});
test('missing player and synchronous play failure return zero', () => {
  assert.equal(evaluate('PlayVideoAsync', []), 0);
  assert.equal(evaluate('PlayVideoAsync', [video({ play: () => { throw new Error('NotSupportedError'); } })]), 0);
});

function onclickChapter(status = '未完成') {
  const text = `第二节 ${status}`;
  const attrs = {
    onclick: "toOld('course-1','chapter-2','class-3')",
    href: '',
    title: ''
  };
  const container = {
    innerText: text,
    className: 'chapter_item',
    getAttribute: key => attrs[key] || '',
    querySelector: () => null,
  };
  return {
    innerText: '第二节',
    textContent: '第二节',
    id: 'curchapter-2',
    href: '',
    matches: selector => selector.includes('.chapter_item[onclick]'),
    closest: () => container,
    querySelector: () => null,
    getAttribute: key => attrs[key] || '',
  };
}

test('onclick chapter nodes are discovered and marked synthetic rather than trusted direct URLs', () => {
  const result = evaluate('ScanChaptersAsync', [onclickChapter()]);
  assert.equal(result.length, 1);
  assert.equal(result[0].chapterId, 'chapter-2');
  assert.equal(result[0].isSyntheticUrl, true);
  assert.match(result[0].url, /studentstudy\?/);
});

test('real chapter hrefs stay eligible as direct navigation fallbacks', () => {
  const result = evaluate('ScanChaptersAsync', [chapter('未完成')]);
  assert.equal(result.length, 1);
  assert.equal(result[0].isSyntheticUrl, false);
});

function modernCatalogChapter() {
  const childAttrs = { onclick: "toOld('course-9','chapter-42','class-7')", href: '', title: '第三节 视频' };
  const child = {
    innerText: '第三节 视频', textContent: '第三节 视频', id: 'curchapter-42', href: '',
    getAttribute: key => childAttrs[key] || '',
    querySelector: () => null,
    matches: selector => selector.includes('[onclick*="toOld"]'),
  };
  const nodeAttrs = { title: '第三节 视频', onclick: '', href: '' };
  const node = {
    innerText: '第三节 视频 待完成任务点', textContent: '第三节 视频 待完成任务点',
    outerHTML: '<div class="posCatalog_select" id="curchapter-42"><span>待完成任务点</span></div>',
    className: 'posCatalog_select', id: 'curchapter-42', href: '',
    getAttribute: key => nodeAttrs[key] || '',
    matches: () => false,
    querySelector: selector => selector.includes('onclick') || selector.includes('[id^="cur"]') ? child : null,
  };
  node.closest = () => node;
  child.closest = () => node;
  return node;
}

test('modern posCatalog/current-id directory nodes are discovered', () => {
  const result = evaluate('ScanChaptersAsync', [modernCatalogChapter()]);
  assert.equal(result.length, 1);
  assert.equal(result[0].chapterId, 'chapter-42');
  assert.equal(result[0].taskType, 'Video');
  assert.equal(result[0].isCompleted, false);
  assert.equal(result[0].isActive, true);
});

function taskPointNoise() {
  const attrs = { class: 'ans-job task-point', href: 'https://example.test/studentstudy?chapterId=noise' };
  const node = {
    innerText: '视频任务点 未完成', textContent: '视频任务点 未完成', className: attrs.class,
    href: attrs.href, getAttribute: key => attrs[key] || '', querySelector: () => null,
  };
  node.closest = () => node;
  return node;
}

test('task-point blocks are not promoted into the chapter library', () => {
  const result = evaluate('ScanChaptersAsync', [taskPointNoise()]);
  assert.equal(result.length, 0);
});

function catalogTitleChapter() {
  const attrs = { onclick: "toOld('course-3','chapter-77','class-2')", title: '第四节', href: '' };
  const node = {
    innerText: '第四节', textContent: '第四节', outerHTML: '<div class="catalog_title">第四节</div>',
    className: 'catalog_title', id: '', href: '',
    getAttribute: key => attrs[key] || '',
    matches: selector => selector.includes('[onclick*="toOld"]'),
    querySelector: () => null,
  };
  node.closest = () => node;
  return node;
}

test('catalog_title onclick nodes are kept as generic navigable chapters', () => {
  const result = evaluate('ScanChaptersAsync', [catalogTitleChapter()]);
  assert.equal(result.length, 1);
  assert.equal(result[0].chapterId, 'chapter-77');
  assert.equal(result[0].taskType, 'Unknown');
});


test('native next-chapter fallback embedded script parses', () => {
  const code = interpolatedScript('FindNextChapterCandidateAsync', { targetId: 'chapter-42' });
  assert.doesNotThrow(() => new vm.Script(code));
});

test('expanded native chapter-click embedded script parses', () => {
  const code = interpolatedScript('OpenChapterAsync', { targetId: 'chapter-42', targetUrl: '', targetTitle: '第三节' });
  assert.doesNotThrow(() => new vm.Script(code));
});

test('chapter titles drop status/task suffix noise', () => {
  const result = evaluate('ScanChaptersAsync', [modernCatalogChapter()]);
  assert.equal(result[0].title, '第三节');
});

test('completion-known flag distinguishes explicit unfinished from unknown', () => {
  const known = evaluate('ScanChaptersAsync', [chapter('未完成')])[0];
  const unknown = evaluate('ScanChaptersAsync', [chapter('完成度 0%')])[0];
  assert.equal(known.completionKnown, true);
  assert.equal(known.isCompleted, false);
  assert.equal(unknown.completionKnown, false);
});

test('unfinished video-task focus embedded script parses', () => {
  assert.doesNotThrow(() => new vm.Script(script('FocusFirstUnfinishedVideoTaskAsync')));
});

test('page recognition embedded script parses with current-course extraction', () => {
  const code = script('RecognizePageAsync');
  assert.match(code, /courseTitle/);
  assert.doesNotThrow(() => new vm.Script(code));
});

test('native next fallback explicitly excludes the current chapter id', () => {
  const code = interpolatedScript('FindNextChapterCandidateAsync', { targetId: 'chapter-42' });
  assert.match(code, /x\.id !== currentId/);
});

test('natural next-chapter fallback accepts unknown status after the current chapter', () => {
  const makeNode = (id, text) => ({
    innerText: text, textContent: text, outerHTML: `<a href="?chapterId=${id}">${text}</a>`, id: '',
    getAttribute: key => key === 'href' ? `https://example.test/studentstudy?chapterId=${id}` : '',
    querySelector: () => null,
  });
  const code = interpolatedScript('FindNextChapterCandidateAsync', { targetId: 'chapter-42' });
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: () => [makeNode('chapter-42', '第一节'), makeNode('chapter-43', '第二节')] },
    location: { href: 'https://example.test/studentstudy?chapterId=chapter-42' }, URL,
  }, { timeout: 1000 });
  assert.equal(result.chapterId, 'chapter-43');
  assert.equal(result.completionKnown, false);
});

test('same-chapter next-video embedded script parses', () => {
  const code = interpolatedScript('AdvanceToNextVideoTaskAsync', {
    targetMediaId: 'shared-player', targetSource: 'https://cdn.example/old.mp4'
  });
  assert.doesNotThrow(() => new vm.Script(code));
});

test('same-chapter next-video prefers currentSrc and does not replay ended video when mediaId is reused', () => {
  const calls = [];
  const makeBlock = (id, status = '') => ({
    innerText: status, textContent: status, outerHTML: `<div data-objectid="${id}">${status}</div>`, className: 'ans-video',
    getAttribute: key => key === 'data-objectid' ? id : '',
    querySelector: () => null,
    scrollIntoView: () => {},
  });
  const sharedBlockA = makeBlock('shared-player', '已完成');
  const sharedBlockB = makeBlock('shared-player', '未完成');
  const oldVideo = {
    currentSrc: 'https://cdn.example/old.mp4', src: '', ended: true, currentTime: 100,
    getAttribute: () => '', closest: () => sharedBlockA, parentElement: sharedBlockA,
    play: () => { calls.push('old'); return Promise.resolve(); }
  };
  const nextVideo = {
    currentSrc: 'https://cdn.example/new.mp4', src: '', ended: false, currentTime: 0,
    getAttribute: () => '', closest: () => sharedBlockB, parentElement: sharedBlockB,
    play: () => { calls.push('next'); return Promise.resolve(); }
  };
  const code = interpolatedScript('AdvanceToNextVideoTaskAsync', {
    targetMediaId: 'shared-player', targetSource: 'https://cdn.example/old.mp4'
  });
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: selector => selector === 'video' ? [oldVideo, nextVideo] : [sharedBlockA, sharedBlockB] },
  }, { timeout: 1000 });
  assert.equal(result, 2);
  assert.deepEqual(calls, ['next']);
});

test('a newly opened paused video beats the old visible ended video', () => {
  const oldEnded = video({ currentSrc: 'old.mp4', duration: 1000, paused: true, ended: true });
  const nextPaused = video({ currentSrc: 'next.mp4', duration: 100, paused: true, ended: false });
  assert.equal(evaluate('GetPlayerSnapshotAsync', [oldEnded, nextPaused]).source, 'next.mp4');
});

test('platform-native next-video control embedded script parses', () => {
  assert.doesNotThrow(() => new vm.Script(script('ClickNativeNextVideoControlAsync')));
});

test('platform-native next-video control clicks the real player next button', () => {
  const calls = [];
  const nextButton = {
    disabled: false,
    className: 'vjs-next-control',
    innerText: '下一视频', textContent: '下一视频',
    getAttribute: key => key === 'title' ? '下一视频' : '',
    getBoundingClientRect: () => ({ width: 30, height: 30 }),
    scrollIntoView: () => {},
    click: () => calls.push('next'),
  };
  const root = {
    parentElement: null,
    querySelectorAll: selector => selector.includes('next') || selector.includes('下一') ? [nextButton] : [],
  };
  const v = video({
    paused: true, ended: true, parentElement: root,
    getBoundingClientRect: () => ({ width: 640, height: 360 }),
  });
  const result = vm.runInNewContext(script('ClickNativeNextVideoControlAsync'), {
    document: { querySelectorAll: selector => selector === 'video' ? [v] : [] },
  }, { timeout: 1000 });
  assert.equal(result, true);
  assert.deepEqual(calls, ['next']);
});

test('native toOld chapter-action retry embedded script parses', () => {
  const code = interpolatedScript('InvokeNativeChapterActionAsync', {
    targetId: 'chapter-42', targetTitle: '第三节'
  });
  assert.doesNotThrow(() => new vm.Script(code));
});

test('native toOld retry preserves the fourth platform argument', () => {
  const calls = [];
  const attrs = { onclick: "toOld('course-9','chapter-42','class-7',0)", title: '第三节' };
  const node = {
    id: 'curchapter-42', innerText: '第三节', textContent: '第三节',
    getAttribute: key => attrs[key] || '',
    matches: selector => selector.includes('[onclick*="toOld"]'),
    querySelector: () => null,
    click: () => calls.push(['click-fallback']),
  };
  const code = interpolatedScript('InvokeNativeChapterActionAsync', {
    targetId: 'chapter-42', targetTitle: '第三节'
  });
  const windowObj = { toOld: (...args) => calls.push(args) };
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: () => [node] },
    window: windowObj, parent: windowObj, top: windowObj, URL, MouseEvent: class {},
  }, { timeout: 1000 });
  assert.equal(result, true);
  assert.deepEqual(calls, [['course-9', 'chapter-42', 'class-7', 0]]);
});

test('context-preserving direct chapter URL keeps auth parameters and only swaps chapterId', () => {
  const code = interpolatedScript('BuildContextPreservingChapterUrlAsync', { targetId: 'chapter-99' });
  const href = 'https://mooc1.chaoxing.com/mycourse/studentstudy?chapterId=chapter-42&courseId=course-9&clazzid=class-7&cpi=123&enc=abc&openc=xyz&mooc2=1';
  const windowObj = {};
  const result = vm.runInNewContext(code, {
    location: { href }, window: windowObj, parent: windowObj, top: windowObj, URL,
  }, { timeout: 1000 });
  const u = new URL(result);
  assert.equal(u.searchParams.get('chapterId'), 'chapter-99');
  assert.equal(u.searchParams.get('courseId'), 'course-9');
  assert.equal(u.searchParams.get('clazzid'), 'class-7');
  assert.equal(u.searchParams.get('cpi'), '123');
  assert.equal(u.searchParams.get('enc'), 'abc');
  assert.equal(u.searchParams.get('openc'), 'xyz');
});

test('platform-native next-video control can live outside the video ancestor tree', () => {
  const calls = [];
  const nextButton = {
    disabled: false, className: 'next-video-btn', innerText: '下一视频', textContent: '下一视频',
    getAttribute: key => key === 'title' ? '下一视频' : '',
    getBoundingClientRect: () => ({ width: 30, height: 30 }),
    scrollIntoView: () => {}, dispatchEvent: () => true, click: () => calls.push('global-next'),
  };
  const root = { parentElement: null, querySelectorAll: () => [] };
  const v = video({ paused: true, ended: true, parentElement: root, getBoundingClientRect: () => ({ width: 640, height: 360 }) });
  const result = vm.runInNewContext(script('ClickNativeNextVideoControlAsync'), {
    document: { querySelectorAll: selector => selector === 'video' ? [v] : (selector.includes('next') || selector.includes('下一') || selector.includes('button') ? [nextButton] : []) },
    window: {}, MouseEvent: class { constructor() {} },
  }, { timeout: 1000 });
  assert.equal(result, true);
  assert.deepEqual(calls, ['global-next']);
});

test('v1.22 video-task scanner keeps chapter, title and explicit unfinished state separate from chapter rows', () => {
  const titleNode = {
    innerText: '真实视频 A',
    getAttribute: key => key === 'data-title' ? '真实视频 A' : '',
  };
  const block = {
    innerText: '真实视频 A 未完成',
    textContent: '真实视频 A 未完成',
    outerHTML: '<div class="ans-video" data-objectid="object-a" data-chapterid="chapter-a">未完成</div>',
    className: 'ans-video',
    getAttribute: key => ({
      'data-objectid': 'object-a',
      'data-chapterid': 'chapter-a',
      'data-chapter-title': '第一章 认识结构',
      'data-title': '真实视频 A',
    }[key] || ''),
    querySelector: selector => {
      if (selector === 'video') return v;
      if (selector.includes('.video-name') || selector.includes('.video-title') || selector.includes('.task-title')) return titleNode;
      return null;
    },
    getBoundingClientRect: () => ({ width: 640, height: 360 }),
  };
  const v = video({
    paused: false,
    ended: false,
    currentSrc: 'https://cdn.example/video-a.mp4',
    getAttribute: () => '',
    closest: () => block,
  });
  const code = script('ScanVideoTasksAsync');
  const result = vm.runInNewContext(code, {
    document: {
      querySelectorAll: selector => selector === 'video' ? [v] : (selector.includes('.ans-video') ? [block] : []),
    },
    location: { href: 'https://mooc1.chaoxing.com/mycourse/studentstudy?courseId=course-a&chapterId=chapter-a' },
    URL,
  }, { timeout: 1000 });
  assert.equal(result.length, 1);
  assert.equal(result[0].chapterId, 'chapter-a');
  assert.equal(result[0].chapterTitle, '第一章 认识结构');
  assert.equal(result[0].title, '真实视频 A');
  assert.equal(result[0].completionKnown, true);
  assert.equal(result[0].isCompleted, false);
  assert.equal(result[0].isPlaying, true);
  assert.match(result[0].taskKey, /object-a/);
});

test('v1.22 page recognition never promotes generic 学生学习页面 into a course name', () => {
  const run = title => vm.runInNewContext(script('RecognizePageAsync'), {
    document: {
      title,
      body: { innerText: '', innerHTML: '' },
      querySelector: () => null,
    },
    location: { href: 'https://mooc1.chaoxing.com/mycourse/studentstudy?courseId=course-a&chapterId=chapter-a' },
  }, { timeout: 1000 });
  assert.equal(run('学生学习页面').courseTitle, '');
  assert.equal(run('材料力学 - 学生学习页面').courseTitle, '材料力学');
});

test('v1.22 player snapshot exposes visibility and task identity used by chapter synchronization', () => {
  const block = {
    getAttribute: key => ({
      'data-objectid': 'object-v122',
      'data-title': '桥梁受力分析',
      'data-chapterid': 'chapter-v122',
      'data-chapter-title': '第三章 桥梁',
    }[key] || ''),
    querySelector: () => null,
    parentElement: null,
  };
  const v = video({
    currentSrc: 'https://cdn.example/v122.mp4',
    closest: () => block,
    getAttribute: () => '',
  });
  const result = evaluate('GetPlayerSnapshotAsync', [v]);
  assert.equal(result.isVisible, true);
  assert.match(result.taskKey, /object-v122/);
  assert.equal(result.chapterId, 'chapter-v122');
  assert.equal(result.chapterTitleHint, '第三章 桥梁');
});


test('v1.23 parent page can click next control when the real video lives only in a child iframe', () => {
  const calls = [];
  const nextButton = {
    disabled: false, className: 'next-video-btn', innerText: '下一视频', textContent: '下一视频',
    getAttribute: key => key === 'title' ? '下一视频' : '',
    getBoundingClientRect: () => ({ width: 30, height: 30 }),
    scrollIntoView: () => {}, dispatchEvent: () => true, click: () => calls.push('parent-next'),
  };
  const holder = {
    parentElement: null,
    querySelectorAll: selector => selector.includes('next') || selector.includes('下一') ? [nextButton] : [],
  };
  const frame = {
    src: 'https://mooc1.chaoxing.com/ananas/modules/video/index.html?objectid=abc',
    className: '', parentElement: holder,
    getAttribute: key => key === 'src' ? 'https://mooc1.chaoxing.com/ananas/modules/video/index.html?objectid=abc' : '',
    closest: () => holder,
  };
  const result = vm.runInNewContext(script('ClickNativeNextVideoControlAsync'), {
    document: {
      querySelectorAll: selector => {
        if (selector === 'video') return [];
        if (selector === 'iframe[src]') return [frame];
        if (selector.includes('next') || selector.includes('下一')) return [nextButton];
        return [];
      },
    },
    window: {}, MouseEvent: class { constructor() {} },
  }, { timeout: 1000 });
  assert.equal(result, true);
  assert.deepEqual(calls, ['parent-next']);
});

test('unknown video task remains a pending playback candidate', () => {
  let focused = false;
  const unknown = {
    innerText: '视频任务', textContent: '视频任务', outerHTML: '<div class="ans-video"></div>', className: 'ans-video',
    getAttribute: key => key === 'title' ? '视频' : '',
    querySelector: selector => selector.includes('video') ? {} : null,
    scrollIntoView: () => { focused = true; },
  };
  const result = vm.runInNewContext(script('FocusFirstUnfinishedVideoTaskAsync'), {
    document: { querySelectorAll: () => [unknown] },
  }, { timeout: 1000 });
  assert.equal(result, true);
  assert.equal(focused, true);
});

test('same-chapter sequential playback accepts an unknown-status next video without labeling it unfinished', () => {
  const calls = [];
  const makeBlock = status => ({
    innerText: status, textContent: status, outerHTML: `<div class="ans-video">${status}</div>`, className: 'ans-video',
    getAttribute: () => '', querySelector: () => null, scrollIntoView: () => {},
  });
  const currentBlock = makeBlock('已完成');
  const unknownBlock = makeBlock('');
  const current = {
    currentSrc: 'old.mp4', src: '', ended: true, currentTime: 100,
    getAttribute: () => '', closest: () => currentBlock, parentElement: currentBlock,
    play: () => { calls.push('old'); return Promise.resolve(); },
  };
  const unknown = {
    currentSrc: 'unknown.mp4', src: '', ended: false, currentTime: 0,
    getAttribute: () => '', closest: () => unknownBlock, parentElement: unknownBlock,
    play: () => { calls.push('unknown'); return Promise.resolve(); },
  };
  const code = interpolatedScript('AdvanceToNextVideoTaskAsync', { targetMediaId: '', targetSource: 'old.mp4' });
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: selector => selector === 'video' ? [current, unknown] : [currentBlock, unknownBlock] },
  }, { timeout: 1000 });
  assert.equal(result, 2);
  assert.deepEqual(calls, ['unknown']);
});

test('same-chapter block fallback skips a test task and opens the following video', () => {
  const calls = [];
  const makeBlock = (id, label, kind) => {
    const button = { click: () => calls.push(id) };
    return {
      innerText: label, textContent: label, outerHTML: `<div>${label}</div>`,
      className: kind === 'video' ? 'ans-video' : 'ans-job',
      getAttribute: key => key === 'data-objectid' ? id : '',
      querySelector: selector => {
        if (selector.includes('video,iframe') || selector === 'video')
          return kind === 'video' ? {} : null;
        if (selector.includes('.ans-job-icon')) return button;
        return null;
      },
      scrollIntoView: () => {},
    };
  };
  const current = makeBlock('old-id', '视频一 已完成', 'video');
  const quiz = makeBlock('quiz-id', '章节测试题 未完成', 'quiz');
  const next = makeBlock('next-id', '视频二', 'video');
  const code = interpolatedScript('AdvanceToNextVideoTaskAsync', { targetMediaId: 'old-id', targetSource: '' });
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: selector => selector === 'video' ? [] : [current, quiz, next] },
  }, { timeout: 1000 });
  assert.equal(result, 1);
  assert.deepEqual(calls, ['next-id']);
});

test('same-chapter cross-iframe target opens the next parent task block', () => {
  const calls = [];
  const makeFrameBlock = (frameUrl, id) => {
    const frame = { src: frameUrl, getAttribute: key => key === 'src' ? frameUrl : '' };
    const button = { click: () => calls.push(id), matches: () => false };
    return {
      className: 'ans-video', innerText: '', textContent: '', outerHTML: '<div class="ans-video"></div>',
      getAttribute: key => key === 'data-objectid' ? id : '',
      querySelector: selector => {
        if (selector === 'iframe[src]') return frame;
        if (selector.includes('.ans-job-icon')) return button;
        return null;
      },
      scrollIntoView: () => {},
    };
  };
  const current = makeFrameBlock('https://example.test/player/one', 'video-1');
  const next = makeFrameBlock('https://example.test/player/two', 'video-2');
  const code = interpolatedScript('AdvanceToNextVideoTaskAsync', {
    targetMediaId: 'video-1', targetSource: 'https://cdn.example/one.mp4',
    targetNextMediaId: 'video-2', targetNextDocumentUrl: 'https://example.test/player/two', targetNextSource: 'https://cdn.example/two.mp4',
  });
  const result = vm.runInNewContext(code, {
    document: { querySelectorAll: selector => selector === 'video' ? [] : [current, next] },
    location: { href: 'https://example.test/course/chapter' },
  }, { timeout: 1000 });
  assert.equal(result, 1);
  assert.deepEqual(calls, ['video-2']);
});

test('native player fallback refuses a generic next-chapter control', () => {
  const calls = [];
  const nextChapter = {
    disabled: false, className: 'next-chapter', innerText: '下一节', textContent: '下一节',
    getAttribute: key => key === 'title' ? '下一节' : '',
    getBoundingClientRect: () => ({ width: 30, height: 30 }),
    click: () => calls.push('chapter'),
  };
  const root = {
    parentElement: null,
    querySelectorAll: selector => selector === 'button,a,[role="button"],[onclick]' ? [nextChapter] : [],
  };
  const v = video({ paused: true, ended: true, parentElement: root });
  const result = vm.runInNewContext(script('ClickNativeNextVideoControlAsync'), {
    document: { querySelectorAll: selector => selector === 'video' ? [v] : [] },
    MouseEvent: class {}, window: {},
  }, { timeout: 1000 });
  assert.equal(result, false);
  assert.deepEqual(calls, []);
});

test('video-task scanner excludes an explicit chapter quiz placeholder', () => {
  const frame = { src: 'https://example.test/play/quiz', getAttribute: key => key === 'src' ? 'https://example.test/play/quiz' : '' };
  const quiz = {
    innerText: '章节测验 测试题', textContent: '章节测验 测试题',
    outerHTML: '<div class="ans-videoquiz">章节测验</div>', className: 'ans-videoquiz',
    getAttribute: key => key === 'title' ? '章节测验' : '',
    querySelector: selector => selector === 'iframe[src]' ? frame : null,
    closest: () => null,
    getBoundingClientRect: () => ({ width: 640, height: 360 }),
  };
  const result = vm.runInNewContext(script('ScanVideoTasksAsync'), {
    document: { querySelectorAll: selector => selector === 'video' ? [] : [quiz] },
    location: { href: 'https://example.test/studentstudy?chapterId=chapter-1' }, URL,
  }, { timeout: 1000 });
  assert.deepEqual(Array.from(result), []);
});
