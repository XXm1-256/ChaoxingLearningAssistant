using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Chaoxing;

/// <summary>
/// 合并“父页面任务块”和“子 iframe 真实 video”的证据。
/// 父页面通常掌握章节、标题和完成状态；子 iframe 才掌握真实媒体源。
/// </summary>
public static class VideoTaskEvidence
{
    public static bool PlaceholderMatchesReal(VideoTaskItem placeholder, VideoTaskItem real)
    {
        if (placeholder.DomIndex >= 0 || real.DomIndex < 0)
            return false;

        if (!string.IsNullOrWhiteSpace(placeholder.MediaId) &&
            !string.IsNullOrWhiteSpace(real.MediaId) &&
            string.Equals(placeholder.MediaId, real.MediaId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(placeholder.Source) &&
            !string.IsNullOrWhiteSpace(real.DocumentUrl) &&
            UriEquivalent(placeholder.Source, real.DocumentUrl))
            return true;

        // 标题只能作为同章节内的补充证据，禁止仅凭“视频一/课程视频”之类重复标题跨章节合并。
        if (!string.IsNullOrWhiteSpace(placeholder.Title) &&
            !string.IsNullOrWhiteSpace(real.Title) &&
            !string.IsNullOrWhiteSpace(placeholder.ChapterId) &&
            !string.IsNullOrWhiteSpace(real.ChapterId) &&
            string.Equals(placeholder.ChapterId, real.ChapterId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeTitle(placeholder.Title), NormalizeTitle(real.Title), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static void MergeParentEvidence(VideoTaskItem real, VideoTaskItem parent)
    {
        if (!PlaceholderMatchesReal(parent, real))
            return;

        if (string.IsNullOrWhiteSpace(real.ChapterId) && !string.IsNullOrWhiteSpace(parent.ChapterId))
            real.ChapterId = parent.ChapterId;
        if (string.IsNullOrWhiteSpace(real.ChapterTitle) && !string.IsNullOrWhiteSpace(parent.ChapterTitle))
            real.ChapterTitle = parent.ChapterTitle;
        if (string.IsNullOrWhiteSpace(real.Title) && !string.IsNullOrWhiteSpace(parent.Title))
            real.Title = parent.Title;
        if (string.IsNullOrWhiteSpace(real.MediaId) && !string.IsNullOrWhiteSpace(parent.MediaId))
            real.MediaId = parent.MediaId;

        // 子 iframe 内通常只有一个 video，DomIndex 都是 0；父任务块的 Index 才代表章节内真实顺序。
        // 合并时继承父顺序，避免多个 iframe 因创建先后不同而把“下一视频”排错。
        if (parent.Index >= 0)
            real.Index = parent.Index;

        // 真实 <video> 自己往往没有完成标记；父任务块有明确标记时必须继承。
        if (!real.CompletionKnown && parent.CompletionKnown)
        {
            real.CompletionKnown = true;
            real.IsCompleted = parent.IsCompleted;
        }
    }

    private static string NormalizeTitle(string value)
        => new string(value.Where(ch => !char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch)).ToArray());

    private static bool UriEquivalent(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!Uri.TryCreate(left, UriKind.Absolute, out var a) ||
            !Uri.TryCreate(right, UriKind.Absolute, out var b))
            return false;

        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
               a.Port == b.Port &&
               string.Equals(a.AbsolutePath.TrimEnd('/'), b.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Query, b.Query, StringComparison.OrdinalIgnoreCase);
    }
}
