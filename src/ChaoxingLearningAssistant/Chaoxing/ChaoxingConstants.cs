namespace ChaoxingLearningAssistant.Chaoxing;

public static class ChaoxingConstants
{
    public const string DefaultHomeUrl = "https://i.chaoxing.com/";

    public static readonly string[] TrustedDomains =
    {
        "chaoxing.com",
        "chaoxing.cn"
    };

    public static bool IsChaoxingUri(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
            return false;

        return TrustedDomains.Any(domain =>
            uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }
}
