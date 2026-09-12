using ChaoxingLearningAssistant.Models;

namespace ChaoxingLearningAssistant.Services;

/// <summary>补偿播放器在高倍速下短暂出现 ended 后立即重置的情况，同时避免把普通“靠近结尾暂停”误判为自然结束。</summary>
public static class PlaybackEndDetector
{
    public static bool IsNaturalEnd(
        PlayerSnapshot snapshot,
        DateTime now,
        DateTime lastPlayingAt,
        string? lastPlayingSource,
        double lastPlayingPosition,
        double lastPlayingDuration,
        double lastPlayingRate,
        TimeSpan pollInterval)
    {
        if (!snapshot.Found) return false;
        if (snapshot.Ended) return true;
        if (!snapshot.Paused) return false;
        // 新播放器载入时常见 0/0，不能沿用上一视频接近结尾的记录。
        if (!double.IsFinite(snapshot.Duration) || snapshot.Duration <= 2 ||
            string.IsNullOrWhiteSpace(snapshot.Source)) return false;

        var recentWindow = TimeSpan.FromSeconds(Math.Max(4.0, pollInterval.TotalSeconds * 2.5 + 0.5));
        if (lastPlayingAt == DateTime.MinValue || now - lastPlayingAt > recentWindow)
            return false;

        if (!string.IsNullOrWhiteSpace(lastPlayingSource) &&
            !string.IsNullOrWhiteSpace(snapshot.Source) &&
            !string.Equals(lastPlayingSource, snapshot.Source, StringComparison.OrdinalIgnoreCase))
            return false;

        // 仍停在视频末端本身就是强证据；阈值保持很小，避免把手动在 99% 附近暂停当成结束。
        if (snapshot.Duration > 2 && snapshot.CurrentTime >= snapshot.Duration - 0.35)
            return true;

        var duration = lastPlayingDuration > 2 ? lastPlayingDuration : snapshot.Duration;
        if (duration <= 2) return false;

        var rate = double.IsFinite(lastPlayingRate) && lastPlayingRate > 0
            ? Math.Clamp(lastPlayingRate, 0.25, 8.0)
            : 1.0;
        var pollingBudget = Math.Clamp(pollInterval.TotalSeconds * rate + 1.25, 2.0, 8.0);
        var wasNearEnd = lastPlayingPosition >= duration - pollingBudget;

        // ended 被网页迅速清掉时常见表现是 currentTime 回到 0/很小，或明显倒退。
        // 普通人工暂停会保持在原位置，因此不能仅凭“上次距离结尾几秒”判结束。
        var resetOrRewound = snapshot.CurrentTime <= 1.5 ||
                            snapshot.CurrentTime + Math.Max(1.5, pollInterval.TotalSeconds * rate * 0.5) < lastPlayingPosition;
        return wasNearEnd && resetOrRewound;
    }
}
