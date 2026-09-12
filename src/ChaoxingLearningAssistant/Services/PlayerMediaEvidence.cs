using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

/// <summary>只使用稳定媒体证据判断“还是不是同一条真实视频”，避免标题/时长延迟加载造成假切换。</summary>
public static class PlayerMediaEvidence
{
    public static readonly TimeSpan PlaybackWaitTimeout = TimeSpan.FromSeconds(90);

    public static PlayerSnapshot? PlaybackTarget(VideoTaskItem task, IEnumerable<VideoTaskItem> scannedTasks)
    {
        if (task.DomIndex < 0)
        {
            // Lazy task URLs describe the containing page and iframe, not the video element.
            var loaded = scannedTasks.Where(x => x.DomIndex >= 0 &&
                string.Equals(x.ChapterId, task.ChapterId, StringComparison.OrdinalIgnoreCase) &&
                ((!string.IsNullOrWhiteSpace(task.MediaId) &&
                  string.Equals(x.MediaId, task.MediaId, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(task.Source) &&
                  string.Equals(x.DocumentUrl, task.Source, StringComparison.OrdinalIgnoreCase) &&
                  (string.IsNullOrWhiteSpace(task.MediaId) || string.IsNullOrWhiteSpace(x.MediaId) ||
                   string.Equals(x.MediaId, task.MediaId, StringComparison.OrdinalIgnoreCase)))))
                .ToArray();
            if (loaded.Length != 1) return null;
            task = loaded[0];
        }
        return new PlayerSnapshot
        {
            Found = true, DocumentUrl = task.DocumentUrl, DomIndex = task.DomIndex,
            MediaId = task.MediaId, Source = task.Source, TaskKey = task.TaskKey, ChapterId = task.ChapterId
        };
    }

    public static bool HasPlaybackProgress(PlayerSnapshot before, PlayerSnapshot after)
        => MatchesPlaybackTarget(before, after) && after.Found && !after.Paused && !after.Ended &&
           double.IsFinite(before.CurrentTime) && double.IsFinite(after.CurrentTime) &&
           after.CurrentTime > before.CurrentTime + 0.05;

    public static bool IsReadyForPlayback(PlayerSnapshot snapshot)
        => snapshot.Found && !snapshot.Ended &&
           (!snapshot.Paused || snapshot.Duration > 0 ||
            (snapshot.ReadyState >= 1 && !string.IsNullOrWhiteSpace(snapshot.Source)));

    /// <summary>
    /// 判断播放器快照是否仍属于一次播放请求最初锁定的 DOM 目标。
    /// 目标刚创建时可以没有媒体地址；已经存在的证据一旦冲突则绝不漂移到其他播放器。
    /// </summary>
    public static bool MatchesPlaybackTarget(PlayerSnapshot target, PlayerSnapshot candidate)
    {
        if (!target.Found || !candidate.Found)
            return false;

        var matched = false;
        var targetDocument = Normalize(target.DocumentUrl);
        var candidateDocument = Normalize(candidate.DocumentUrl);
        if (!string.IsNullOrWhiteSpace(targetDocument))
        {
            if (!string.Equals(targetDocument, candidateDocument, StringComparison.OrdinalIgnoreCase))
                return false;
            matched = true;
        }

        if (target.DomIndex >= 0)
        {
            if (candidate.DomIndex != target.DomIndex)
                return false;
            matched = true;
        }

        var targetMediaId = Normalize(target.MediaId);
        var candidateMediaId = Normalize(candidate.MediaId);
        if (!string.IsNullOrWhiteSpace(targetMediaId) && !string.IsNullOrWhiteSpace(candidateMediaId))
        {
            if (!string.Equals(targetMediaId, candidateMediaId, StringComparison.OrdinalIgnoreCase))
                return false;
            matched = true;
        }

        var targetSource = Normalize(target.Source);
        var candidateSource = Normalize(candidate.Source);
        if (!string.IsNullOrWhiteSpace(targetSource) && !string.IsNullOrWhiteSpace(candidateSource))
        {
            if (!string.Equals(targetSource, candidateSource, StringComparison.OrdinalIgnoreCase) &&
                !SameSourcePath(targetSource, candidateSource))
                return false;
            matched = true;
        }

        return matched;
    }

    public static string StableIdentity(PlayerSnapshot snapshot)
    {
        if (!snapshot.Found) return string.Empty;

        var source = Normalize(snapshot.Source);
        var mediaId = Normalize(snapshot.MediaId);
        var documentUrl = Normalize(snapshot.DocumentUrl);

        if (!string.IsNullOrWhiteSpace(source))
            return $"src:{source}|media:{mediaId}|doc:{documentUrl}|dom:{snapshot.DomIndex}";
        if (!string.IsNullOrWhiteSpace(mediaId))
            return $"media:{mediaId}|doc:{documentUrl}|dom:{snapshot.DomIndex}";
        if (!string.IsNullOrWhiteSpace(snapshot.TaskKey))
            return $"task:{Normalize(snapshot.TaskKey)}|doc:{documentUrl}|dom:{snapshot.DomIndex}";
        return $"doc:{documentUrl}|dom:{snapshot.DomIndex}";
    }

    public static string StableTaskIdentity(PlayerSnapshot snapshot)
    {
        if (!snapshot.Found) return string.Empty;
        var documentUrl = Normalize(snapshot.DocumentUrl);
        if (!string.IsNullOrWhiteSpace(snapshot.TaskKey))
            return $"task:{Normalize(snapshot.TaskKey)}|media:{Normalize(snapshot.MediaId)}|doc:{documentUrl}|dom:{snapshot.DomIndex}";
        if (!string.IsNullOrWhiteSpace(snapshot.MediaId))
            return $"media:{Normalize(snapshot.MediaId)}|doc:{documentUrl}|dom:{snapshot.DomIndex}";
        return StableIdentity(snapshot);
    }

    public static bool IsSameMedia(PlayerSnapshot a, PlayerSnapshot b)
    {
        if (!a.Found || !b.Found) return false;

        var aSource = Normalize(a.Source);
        var bSource = Normalize(b.Source);
        if (!string.IsNullOrWhiteSpace(aSource) && !string.IsNullOrWhiteSpace(bSource))
        {
            if (!string.Equals(aSource, bSource, StringComparison.OrdinalIgnoreCase))
            {
                // CDN signatures can rotate while the same task is playing. Strong task evidence
                // takes precedence over a mutable currentSrc query string.
                if (!SameStrongTaskEvidence(a, b))
                    return false;
            }

            // 同一 URL 也可能被平台复用给不同任务；双方都有明确任务/章节证据且发生变化时视为新视频。
            if (!string.IsNullOrWhiteSpace(a.ChapterId) && !string.IsNullOrWhiteSpace(b.ChapterId) &&
                !string.Equals(a.ChapterId, b.ChapterId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrWhiteSpace(a.TaskKey) && !string.IsNullOrWhiteSpace(b.TaskKey) &&
                !string.Equals(a.TaskKey, b.TaskKey, StringComparison.OrdinalIgnoreCase))
            {
                var generatedKeysChangedOnlyWithSource = LooksSourceDerivedTaskKey(a.TaskKey) &&
                    LooksSourceDerivedTaskKey(b.TaskKey) && SameSourcePath(a.Source, b.Source) &&
                    a.DomIndex == b.DomIndex &&
                    string.Equals(Normalize(a.DocumentUrl), Normalize(b.DocumentUrl), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Normalize(a.MediaId), Normalize(b.MediaId), StringComparison.OrdinalIgnoreCase);
                if (!generatedKeysChangedOnlyWithSource)
                    return false;
            }
            return true;
        }

        if (!string.IsNullOrWhiteSpace(a.MediaId) && !string.IsNullOrWhiteSpace(b.MediaId) &&
            string.Equals(a.MediaId, b.MediaId, StringComparison.OrdinalIgnoreCase) &&
            a.DomIndex == b.DomIndex &&
            string.Equals(Normalize(a.DocumentUrl), Normalize(b.DocumentUrl), StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(a.TaskKey) && !string.IsNullOrWhiteSpace(b.TaskKey) &&
            string.Equals(a.TaskKey, b.TaskKey, StringComparison.OrdinalIgnoreCase))
            return true;

        var ai = StableIdentity(a);
        var bi = StableIdentity(b);
        return !string.IsNullOrWhiteSpace(ai) && string.Equals(ai, bi, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameStrongTaskEvidence(PlayerSnapshot a, PlayerSnapshot b)
    {
        if (!string.IsNullOrWhiteSpace(a.TaskKey) && !string.IsNullOrWhiteSpace(b.TaskKey) &&
            string.Equals(a.TaskKey, b.TaskKey, StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(a.MediaId) &&
               !string.IsNullOrWhiteSpace(b.MediaId) &&
               string.Equals(a.MediaId, b.MediaId, StringComparison.OrdinalIgnoreCase) &&
               a.DomIndex == b.DomIndex &&
               string.Equals(Normalize(a.DocumentUrl), Normalize(b.DocumentUrl), StringComparison.OrdinalIgnoreCase) &&
               SameSourcePath(a.Source, b.Source);
    }

    private static bool SameSourcePath(string? a, string? b)
    {
        if (!Uri.TryCreate(a, UriKind.Absolute, out var aUri) ||
            !Uri.TryCreate(b, UriKind.Absolute, out var bUri))
            return false;

        return string.Equals(aUri.Scheme, bUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(aUri.Host, bUri.Host, StringComparison.OrdinalIgnoreCase) &&
               aUri.Port == bUri.Port &&
               string.Equals(aUri.AbsolutePath, bUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSourceDerivedTaskKey(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains("|src:", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
