using System.IO;
using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaoxingLearningAssistant.Tests;

[TestClass]
public sealed class PersistenceRegressionTests
{
    private string _root = null!;
    private string DataPath => Path.Combine(_root, "session.json");
    private FileLogger Logger => new(Path.Combine(_root, "logs"));

    [TestInitialize]
    public void Setup() => Directory.CreateDirectory(_root = Path.Combine(Path.GetTempPath(), "cla-tests-" + Guid.NewGuid().ToString("N")));

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public void ClearSession_DoesNotResurrectBackup()
    {
        var service = new SessionRecoveryService(DataPath, Logger);
        service.Save(new SessionSnapshot { WasRunning = true });
        service.Save(new SessionSnapshot { WasRunning = true });
        Assert.IsTrue(File.Exists(DataPath + ".bak"));
        service.Clear();
        Assert.IsFalse(File.Exists(DataPath));
        Assert.IsFalse(File.Exists(DataPath + ".bak"));
        Assert.IsFalse(new SessionRecoveryService(DataPath, Logger).Load().WasRunning);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{broken")]
    [DataRow("[]")]
    public void UnreadablePrimary_RecoversBackup_AndNextSavePreservesIt(string primary)
    {
        File.WriteAllText(DataPath, primary);
        File.WriteAllText(DataPath + ".bak", "{\"WasRunning\":true}");
        var service = new SessionRecoveryService(DataPath, Logger);
        Assert.IsTrue(service.Load().WasRunning);
        service.Save(new SessionSnapshot { WasRunning = false });
        File.Delete(DataPath);
        Assert.IsTrue(service.Load().WasRunning, "An unreadable primary must not destroy the valid backup.");
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length);
    }

    [TestMethod]
    public void ValidSave_KeepsPreviousReadableVersion()
    {
        var service = new SessionRecoveryService(DataPath, Logger);
        service.Save(new SessionSnapshot { WasRunning = true });
        service.Save(new SessionSnapshot { WasRunning = false });
        Assert.IsFalse(service.Load().WasRunning);
        File.Delete(DataPath);
        Assert.IsTrue(service.Load().WasRunning);
    }

    [TestMethod]
    public void Cache_NullEntryDoesNotPreventValidCoursesLoading()
    {
        File.WriteAllText(DataPath, "{\"SavedAt\":\"" + DateTime.Now.ToString("O") + "\",\"Courses\":[null,{\"Title\":\"Course\",\"Url\":\"https://example.com/course\"}]}");
        var courses = new CourseCacheService(DataPath, Logger).Load();
        Assert.AreEqual(1, courses.Count);
        Assert.AreEqual("Course", courses[0].Title);
    }

    [TestMethod]
    public void CourseRefresh_RetainsSelectedObjectAndProgress()
    {
        var selected = new CourseItem { Title = "Old", Url = "https://example.com/a", VideoCount = 8, CompletedCount = 3, LastChapter = "Chapter 2" };
        var result = CourseListRefresh.Merge(new[] { selected }, new[] {
            new CourseItem { Title = "New", Url = selected.Url },
            new CourseItem { Title = "Duplicate", Url = selected.Url },
            new CourseItem { Title = "B", Url = "https://example.com/b" }
        });
        Assert.AreEqual(2, result.Count);
        Assert.AreSame(selected, result[0]);
        Assert.AreEqual("New", selected.Title);
        Assert.AreEqual(8, selected.VideoCount);
        Assert.AreEqual(3, selected.CompletedCount);
        Assert.AreEqual("Chapter 2", selected.LastChapter);
    }

    [TestMethod]
    public void CourseRefresh_RemovesCoursesAbsentFromFreshScan()
    {
        var old = new CourseItem { Url = "https://example.com/a" };
        var fresh = new CourseItem { Url = "https://example.com/b" };
        var result = CourseListRefresh.Merge(new[] { old }, new[] { fresh });
        Assert.AreEqual(1, result.Count);
        Assert.AreSame(fresh, result[0]);
    }
}
