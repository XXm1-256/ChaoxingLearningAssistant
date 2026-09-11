namespace ChaoxingLearningAssistant.Services;

/// <summary>同一提示在短时间内只通知一次，避免轮询/导航回调造成提示音和气泡轰炸。</summary>
public sealed class NotificationThrottle
{
    private readonly Dictionary<string, DateTimeOffset> _recent = new(StringComparer.Ordinal);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    public bool ShouldNotify(string key, DateTimeOffset now)
    {
        if (_recent.TryGetValue(key, out var previous) && now - previous < Cooldown)
            return false;
        foreach (var expired in _recent.Where(x => now - x.Value >= Cooldown).Select(x => x.Key).ToArray())
            _recent.Remove(expired);
        if (_recent.Count >= 128)
            _recent.Remove(_recent.MinBy(x => x.Value).Key);
        _recent[key] = now;
        return true;
    }
}
