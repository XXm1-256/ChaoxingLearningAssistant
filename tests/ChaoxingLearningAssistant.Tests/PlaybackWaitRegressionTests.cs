using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaoxingLearningAssistant.Tests;

[TestClass]
public sealed class PlaybackWaitRegressionTests
{
    [TestMethod]
    public void WaitBudget_AllowsSlowLoadingUntilNinetySeconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(90), PlayerMediaEvidence.PlaybackWaitTimeout);
        foreach (var seconds in new[] { 6, 19, 30, 40, 89 })
            Assert.IsTrue(TimeSpan.FromSeconds(seconds) < PlayerMediaEvidence.PlaybackWaitTimeout);
    }

    [TestMethod]
    public void LazyTask_ResolvesRealPlayerInsteadOfParentDocument()
    {
        var lazy = new VideoTaskItem { ChapterId = "c", MediaId = "m", DocumentUrl = "https://test/chapter", Source = "https://test/frame" };
        Assert.IsNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { lazy }));
        var real = new VideoTaskItem { ChapterId = "c", MediaId = "m", DocumentUrl = "https://test/frame", Source = "https://test/movie.mp4", DomIndex = 0 };
        var target = PlayerMediaEvidence.PlaybackTarget(lazy, new[] { lazy, real });
        Assert.IsNotNull(target);
        Assert.AreEqual(real.DocumentUrl, target.DocumentUrl);
        Assert.AreEqual(real.Source, target.Source);
        Assert.AreEqual(0, target.DomIndex);
    }

    [TestMethod]
    public void LazyTask_RejectsWrongMediaChapterAndAmbiguousPlayers()
    {
        var lazy = new VideoTaskItem { ChapterId = "c", MediaId = "m", Source = "https://test/frame" };
        var wrong = new VideoTaskItem { ChapterId = "c", MediaId = "other", DocumentUrl = lazy.Source, DomIndex = 0 };
        Assert.IsNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { wrong }));
        wrong.MediaId = "m";
        wrong.ChapterId = "other";
        Assert.IsNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { wrong }));
        wrong.ChapterId = "c";
        Assert.IsNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { wrong, wrong }));
    }

    [TestMethod]
    public void LazyTask_WithoutMediaIdRequiresExactIframeDocument()
    {
        var lazy = new VideoTaskItem { ChapterId = "c", Source = "https://test/frame" };
        var real = new VideoTaskItem { ChapterId = "c", DocumentUrl = lazy.Source, DomIndex = 0 };
        Assert.IsNotNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { real }));
        real.DocumentUrl = "https://test/other";
        Assert.IsNull(PlayerMediaEvidence.PlaybackTarget(lazy, new[] { real }));
    }
}
