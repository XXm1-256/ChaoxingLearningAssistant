namespace ChaoxingLearningAssistant.Services;

/// <summary>同一视频停留在 ended 状态时只处理一次；重播或更换视频后重新允许处理。</summary>
public sealed class PlaybackEndGate
{
    private string? _handledIdentity;

    public bool TryHandle(string identity, bool ended)
    {
        if (!ended)
        {
            Reset();
            return false;
        }
        if (string.Equals(_handledIdentity, identity, StringComparison.Ordinal))
            return false;
        _handledIdentity = identity;
        return true;
    }

    public void Reset() => _handledIdentity = null;
}
