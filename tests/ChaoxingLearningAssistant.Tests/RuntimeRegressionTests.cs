using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaoxingLearningAssistant.Tests;

[TestClass]
public sealed class RuntimeRegressionTests
{
    [TestMethod]
    public void RealCourseTemplate_RendersReadOnlyProgress_AndUpdatesCounts()
    {
        // 测试实际发布界面的模板，并触发 WPF 布局，避免 XML 解析通过却运行崩溃。
        RunSta(() =>
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CourseUiUnderTest.xaml")!;
            var xml = XDocument.Load(stream);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var templateXml = xml.Descendants(wpf + "ListBox")
                .Single(e => (string?)e.Attribute(x + "Name") == "CourseList")
                .Descendants(wpf + "DataTemplate").Single();
            var template = (DataTemplate)XamlReader.Parse(templateXml.ToString());
            var course = new CourseItem { Title = "视频课程", VideoCount = 8, CompletedCount = 3 };
            var presenter = new ContentPresenter { Content = course, ContentTemplate = template };
            presenter.Measure(new Size(360, 500));
            presenter.Arrange(new Rect(0, 0, 360, 500));
            presenter.UpdateLayout();
            var progressSummary = Descendants(presenter).OfType<TextBlock>()
                .Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == nameof(CourseItem.ProgressSummary));
            var progressBar = Descendants(presenter).OfType<ProgressBar>()
                .Single(b => BindingOperations.GetBinding(b, ProgressBar.ValueProperty)?.Path.Path == nameof(CourseItem.ProgressPercent));
            BindingOperations.GetBindingExpression(progressSummary, TextBlock.TextProperty)!.UpdateTarget();
            BindingOperations.GetBindingExpression(progressBar, ProgressBar.ValueProperty)!.UpdateTarget();
            Assert.AreEqual("已完成 3 / 8  ·  38%", progressSummary.Text);
            Assert.AreEqual(37.5, progressBar.Value, 0.001);
            course.CompletedCount = 4;
            BindingOperations.GetBindingExpression(progressSummary, TextBlock.TextProperty)!.UpdateTarget();
            BindingOperations.GetBindingExpression(progressBar, ProgressBar.ValueProperty)!.UpdateTarget();
            Assert.AreEqual("已完成 4 / 8  ·  50%", progressSummary.Text);
            Assert.AreEqual(50.0, progressBar.Value, 0.001);
        });
    }

    [TestMethod]
    public void CompletionNotice_StaysSingleAcrossRepeatedTimerTicks()
    {
        var gate = new PlaybackEndGate();
        Assert.IsTrue(gate.TryHandle("video-a", true));
        for (var i = 0; i < 100; i++)
            Assert.IsFalse(gate.TryHandle("video-a", true));
        Assert.IsTrue(gate.TryHandle("video-b", true));
    }

    [TestMethod]
    public void CompletionNotice_RearmsWhenVideoIsReplayed()
    {
        var gate = new PlaybackEndGate();
        Assert.IsTrue(gate.TryHandle("video", true));
        Assert.IsFalse(gate.TryHandle("video", false));
        Assert.IsTrue(gate.TryHandle("video", true));
        gate.Reset();
        Assert.IsTrue(gate.TryHandle("video", true));
    }

    [TestMethod]
    public void Notices_DeduplicateWithoutHidingDifferentErrors()
    {
        var gate = new NotificationThrottle();
        var now = DateTimeOffset.UtcNow;
        Assert.IsTrue(gate.ShouldNotify("network", now));
        Assert.IsTrue(gate.ShouldNotify("verification", now));
        Assert.IsFalse(gate.ShouldNotify("network", now.AddSeconds(1)));
        Assert.IsTrue(gate.ShouldNotify("network", now.AddSeconds(20)));
    }

    [TestMethod]
    public void CourseProgress_NotifiesWhenCountsChange()
    {
        var course = new CourseItem();
        var notifications = new List<string?>();
        course.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        course.VideoCount = 8;
        course.CompletedCount = 4;
        Assert.AreEqual(2, notifications.Count(x => x == nameof(CourseItem.ProgressPercent)));
        Assert.AreEqual(50.0, course.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void PlaybackWatchTracker_ExcludesPausedAndSleepIntervals()
    {
        var tracker = new PlaybackWatchTracker();
        var start = DateTimeOffset.UtcNow;
        tracker.Reset("video-a", start, isPlaying: true);
        tracker.Sample("video-a", true, start.AddSeconds(2));
        tracker.Sample("video-a", false, start.AddSeconds(4));
        tracker.Sample("video-a", false, start.AddMinutes(5));
        tracker.Sample("video-a", true, start.AddMinutes(5).AddSeconds(1));
        tracker.Sample("video-a", false, start.AddMinutes(5).AddSeconds(4));

        Assert.AreEqual(7.0, tracker.Elapsed.TotalSeconds, 0.001);
    }

    [TestMethod]
    public void PlaybackWatchTracker_ResetsWhenMediaChanges()
    {
        var tracker = new PlaybackWatchTracker();
        var start = DateTimeOffset.UtcNow;
        tracker.Reset("video-a", start, isPlaying: true);
        tracker.Sample("video-a", true, start.AddSeconds(3));
        tracker.Sample("video-b", true, start.AddSeconds(4));
        tracker.Sample("video-b", false, start.AddSeconds(6));

        Assert.AreEqual(2.0, tracker.Elapsed.TotalSeconds, 0.001);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "WPF template test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
