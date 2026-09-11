using ChaoxingLearningAssistant.Chaoxing;
using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaoxingLearningAssistant.Tests;

[TestClass]
public sealed class ModelTests
{
    [TestMethod]
    public void CourseProgress_IsCalculatedFromVideoCounts()
    {
        var course = new CourseItem { VideoCount = 8, CompletedCount = 3 };
        Assert.AreEqual(37.5, course.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void CourseProgress_ClampsAtOneHundred()
    {
        var course = new CourseItem { VideoCount = 2, CompletedCount = 5 };
        Assert.AreEqual(100.0, course.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void ChapterTypeText_MapsVideo()
    {
        var chapter = new ChapterItem { TaskType = TaskType.Video };
        Assert.AreEqual("视频", chapter.TaskTypeText);
    }

    [TestMethod]
    public void PlayerProgress_IsCalculatedSafely()
    {
        var player = new PlayerSnapshot { CurrentTime = 25, Duration = 100 };
        Assert.AreEqual(25.0, player.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void LoginUrlClassifier_RecognizesCommonChaoxingLoginRoutes()
    {
        Assert.IsTrue(ChaoxingUrlClassifier.IsLoginUri("https://passport2.chaoxing.com/login?refer=x"));
        Assert.IsTrue(ChaoxingUrlClassifier.IsLoginUri("https://schoolcas.chaoxing.com/cas/login?service=x"));
        Assert.IsTrue(ChaoxingUrlClassifier.IsLoginUri("https://sso.example.edu/cas/login?service=x"));
        Assert.IsFalse(ChaoxingUrlClassifier.IsLoginUri("https://mooc1.chaoxing.com/mycourse/studentstudy?chapterId=1"));
    }

    [TestMethod]
    public void StudyUrlClassifier_RecognizesLearningPages()
    {
        Assert.IsTrue(ChaoxingUrlClassifier.IsStudyUri("https://mooc1.chaoxing.com/mycourse/studentstudy?chapterId=1"));
        Assert.IsTrue(ChaoxingUrlClassifier.IsStudyUri("https://mooc1-1.chaoxing.com/mooc-ans/nodedetailcontroller/visitnodedetail?knowledgeId=1"));
        Assert.IsFalse(ChaoxingUrlClassifier.IsStudyUri("https://passport2.chaoxing.com/login"));
    }
    [TestMethod]
    public void ChapterVideoTasks_AreTrackedSeparatelyFromChapterType()
    {
        var chapter = new ChapterItem
        {
            Title = "第一章",
            TaskType = TaskType.Unknown,
            VideoTasks = new List<VideoTaskItem>
            {
                new() { Title = "视频一", CompletionKnown = true, IsCompleted = true },
                new() { Title = "视频二", CompletionKnown = true, IsCompleted = false }
            }
        };

        Assert.IsTrue(chapter.IsVideo);
        Assert.AreEqual(2, chapter.VideoCount);
        Assert.AreEqual(1, chapter.CompletedVideoCount);
    }

    [TestMethod]
    public void VideoTaskDisplayTitle_FallsBackWithoutChangingChapterTitle()
    {
        var task = new VideoTaskItem { Index = 2, Title = string.Empty };
        Assert.AreEqual("视频 3", task.DisplayTitle);
    }

    [TestMethod]
    public void CoursePlaybackPlan_KeepsOnlyPendingVideoChaptersInCourseOrder()
    {
        var chapters = new[]
        {
            new ChapterItem { Index = 0, ChapterId = "done", TaskType = TaskType.Video, CompletionKnown = true, IsCompleted = true },
            new ChapterItem { Index = 1, ChapterId = "quiz", TaskType = TaskType.Quiz },
            new ChapterItem { Index = 2, ChapterId = "unknown", TaskType = TaskType.Unknown },
            new ChapterItem { Index = 3, ChapterId = "pending", TaskType = TaskType.Video, CompletionKnown = true, IsCompleted = false }
        };

        var result = CoursePlaybackPlan.BuildPendingChapters(chapters, 0, new HashSet<string>());

        CollectionAssert.AreEqual(new[] { "unknown", "pending" }, result.Select(x => x.ChapterId).ToArray());
    }

    [TestMethod]
    public void CoursePlaybackPlan_RespectsStartAndVerifiedChapters()
    {
        var chapters = new[]
        {
            new ChapterItem { Index = 0, ChapterId = "before", TaskType = TaskType.Unknown },
            new ChapterItem { Index = 1, ChapterId = "checked", TaskType = TaskType.Unknown },
            new ChapterItem { Index = 2, ChapterId = "next", TaskType = TaskType.Unknown }
        };
        var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoursePlaybackPlan.Identity(chapters[1])
        };

        var result = CoursePlaybackPlan.BuildPendingChapters(chapters, 1, verified);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("next", result[0].ChapterId);
    }

    [TestMethod]
    public void CoursePlaybackPlan_UsesVideoTasksInsteadOfStaleChapterStatus()
    {
        var chapter = new ChapterItem
        {
            Index = 0,
            ChapterId = "mixed",
            TaskType = TaskType.Video,
            CompletionKnown = true,
            IsCompleted = true,
            VideoTasks = new List<VideoTaskItem>
            {
                new() { CompletionKnown = true, IsCompleted = true },
                new() { CompletionKnown = false, IsCompleted = false }
            }
        };

        Assert.IsTrue(CoursePlaybackPlan.HasPendingVideo(chapter));
    }

    [TestMethod]
    public void VideoTaskEvidence_MergesParentChapterAndCompletionIntoIframeVideo()
    {
        var parent = new VideoTaskItem
        {
            Index = 3,
            Title = "父页面视频标题",
            ChapterId = "chapter-9",
            ChapterTitle = "第九章",
            Source = "https://mooc1.chaoxing.com/ananas/modules/video/index.html?objectid=abc",
            DomIndex = -1,
            CompletionKnown = true,
            IsCompleted = false
        };
        var real = new VideoTaskItem
        {
            DocumentUrl = "https://mooc1.chaoxing.com/ananas/modules/video/index.html?objectid=abc",
            Source = "https://cdn.example/abc.mp4",
            DomIndex = 0
        };

        Assert.IsTrue(VideoTaskEvidence.PlaceholderMatchesReal(parent, real));
        VideoTaskEvidence.MergeParentEvidence(real, parent);
        Assert.AreEqual("chapter-9", real.ChapterId);
        Assert.AreEqual("第九章", real.ChapterTitle);
        Assert.AreEqual("父页面视频标题", real.Title);
        Assert.AreEqual(3, real.Index);
        Assert.IsTrue(real.CompletionKnown);
        Assert.IsFalse(real.IsCompleted);
    }

    [TestMethod]
    public void PlayerMediaEvidence_IgnoresLateTitleAndDurationButDetectsNewSource()
    {
        var before = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/a.mp4", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0,
            Duration = 0, VideoTitle = string.Empty
        };
        var metadataLoaded = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/a.mp4", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0,
            Duration = 600, VideoTitle = "延迟出现的标题"
        };
        var next = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/b.mp4", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0,
            Duration = 500, VideoTitle = "下一视频"
        };

        Assert.AreEqual(PlayerMediaEvidence.StableIdentity(before), PlayerMediaEvidence.StableIdentity(metadataLoaded));
        Assert.IsTrue(PlayerMediaEvidence.IsSameMedia(before, metadataLoaded));
        Assert.IsFalse(PlayerMediaEvidence.IsSameMedia(metadataLoaded, next));
    }

    [TestMethod]
    public void PlayerMediaEvidence_ToleratesRotatingCdnSignatureForSameTask()
    {
        var before = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/a.mp4?token=old", MediaId = "object-1",
            TaskKey = "task-1", DocumentUrl = "https://player.example/frame", DomIndex = 0
        };
        var refreshed = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/a.mp4?token=new", MediaId = "object-1",
            TaskKey = "task-1", DocumentUrl = "https://player.example/frame", DomIndex = 0
        };
        var differentTask = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/a.mp4?token=new", MediaId = "object-1",
            TaskKey = "task-2", DocumentUrl = "https://player.example/frame", DomIndex = 0
        };

        Assert.IsTrue(PlayerMediaEvidence.IsSameMedia(before, refreshed));
        Assert.AreEqual(PlayerMediaEvidence.StableTaskIdentity(before), PlayerMediaEvidence.StableTaskIdentity(refreshed));
        Assert.IsFalse(PlayerMediaEvidence.IsSameMedia(before, differentTask));
    }

    [TestMethod]
    public void PlayerMediaEvidence_ToleratesQueryRotationWithoutConfusingDifferentPaths()
    {
        var before = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/course/a.mp4?token=old", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0
        };
        var refreshed = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/course/a.mp4?token=new", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0
        };
        var next = new PlayerSnapshot
        {
            Found = true, Source = "https://cdn.example/course/b.mp4?token=new", MediaId = "shared",
            DocumentUrl = "https://player.example/frame", DomIndex = 0
        };

        Assert.IsTrue(PlayerMediaEvidence.IsSameMedia(before, refreshed));
        Assert.IsFalse(PlayerMediaEvidence.IsSameMedia(refreshed, next));
    }

    [TestMethod]
    public void AppSettings_NormalizeClampsValuesAndRejectsUnsafeResumeUrl()
    {
        var settings = new AppSettings
        {
            Theme = (ThemeMode)999,
            LogRetentionDays = -20,
            PageTimeoutSeconds = 1000,
            MaxPageTimeoutSeconds = -1,
            LastCourseUrl = "file:///C:/secret.txt",
            LastCourseTitle = "  测试课程  "
        };

        settings.Normalize();

        Assert.AreEqual(ThemeMode.Light, settings.Theme);
        Assert.AreEqual(1, settings.LogRetentionDays);
        Assert.AreEqual(60, settings.PageTimeoutSeconds);
        Assert.AreEqual(60, settings.MaxPageTimeoutSeconds);
        Assert.AreEqual(string.Empty, settings.LastCourseUrl);
        Assert.AreEqual("测试课程", settings.LastCourseTitle);
    }

    [TestMethod]
    public void PlaybackEndDetector_CatchesHighRateResetWithoutTreatingNormalPauseAsEnded()
    {
        var now = DateTime.UtcNow;
        var reset = new PlayerSnapshot
        {
            Found = true, Paused = true, Ended = false,
            Source = "video.mp4", CurrentTime = 0, Duration = 100, PlaybackRate = 2
        };
        Assert.IsTrue(PlaybackEndDetector.IsNaturalEnd(
            reset, now, now.AddMilliseconds(-1500), "video.mp4", 97.0, 100.0, 2.0, TimeSpan.FromMilliseconds(1500)));

        var manualPause = new PlayerSnapshot
        {
            Found = true, Paused = true, Ended = false,
            Source = "video.mp4", CurrentTime = 97.0, Duration = 100, PlaybackRate = 2
        };
        Assert.IsFalse(PlaybackEndDetector.IsNaturalEnd(
            manualPause, now, now.AddMilliseconds(-1500), "video.mp4", 97.0, 100.0, 2.0, TimeSpan.FromMilliseconds(1500)));
    }

}
