namespace ChaoxingLearningAssistant.Services;

/// <summary>
/// Accumulates time only while the real player was observed playing. Long UI stalls are
/// capped so sleep/resume or a blocked WebView cannot create implausible statistics.
/// </summary>
public sealed class PlaybackWatchTracker
{
    private string _identity = string.Empty;
    private DateTimeOffset _lastSampleAt;
    private bool _wasPlaying;

    public TimeSpan Elapsed { get; private set; }

    public void Reset(string identity = "", DateTimeOffset? now = null, bool isPlaying = false)
    {
        _identity = identity ?? string.Empty;
        _lastSampleAt = now ?? DateTimeOffset.MinValue;
        _wasPlaying = isPlaying;
        Elapsed = TimeSpan.Zero;
    }

    public void Sample(string identity, bool isPlaying, DateTimeOffset now)
    {
        identity ??= string.Empty;
        if (!string.Equals(identity, _identity, StringComparison.OrdinalIgnoreCase))
        {
            Reset(identity, now, isPlaying);
            return;
        }

        if (_lastSampleAt != DateTimeOffset.MinValue && _wasPlaying && now > _lastSampleAt)
        {
            var delta = now - _lastSampleAt;
            // Normal polling is 1.5s. A generous cap keeps brief UI stalls while excluding sleep time.
            if (delta <= TimeSpan.FromSeconds(15))
                Elapsed += delta;
        }

        _lastSampleAt = now;
        _wasPlaying = isPlaying;
    }
}
