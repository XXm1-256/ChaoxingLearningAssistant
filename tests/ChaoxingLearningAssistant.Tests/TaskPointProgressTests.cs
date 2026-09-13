using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaoxingLearningAssistant.Tests;

[TestClass]
public sealed class TaskPointProgressTests
{
    [TestMethod]
    public void ReadsOnlyExplicitTaskPointCounts()
    {
        Assert.AreEqual(new TaskPointProgress(3, 8), TaskPointProgress.Parse("任务点：3/8"));
        Assert.AreEqual(new TaskPointProgress(3, 8), TaskPointProgress.Parse("已完成3/共8个任务点"));
        Assert.IsNull(TaskPointProgress.Parse("视频 3/8"));
        Assert.IsNull(TaskPointProgress.Parse("任务点 9/8"));
        Assert.IsNull(TaskPointProgress.Parse("未完成任务点 2"));
    }

    [TestMethod]
    public void VideoCountsDoNotPretendToBeAllTaskPoints()
    {
        var course = new CourseItem { VideoCount = 8, CompletedCount = 8 };
        Assert.AreEqual("任务点进度尚未读取", course.TaskProgressSummary);
        course.TaskCount = 10;
        course.CompletedTaskCount = 8;
        Assert.AreEqual(80.0, course.TaskProgressPercent);
        var refreshed = CourseListRefresh.Merge(new[] { course }, new[] {
            new CourseItem { Url = course.Url, TaskCount = 10, CompletedTaskCount = 9 }
        });
        Assert.AreEqual(90.0, refreshed[0].TaskProgressPercent);
    }
}
