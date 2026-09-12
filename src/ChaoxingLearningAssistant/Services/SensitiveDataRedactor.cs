using System.Text.RegularExpressions;

namespace ChaoxingLearningAssistant.Services;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var text = QuerySecretRegex().Replace(value, "$1[REDACTED]");
        text = HeaderSecretRegex().Replace(text, "$1[REDACTED]");
        return CookieSecretRegex().Replace(text, "$1[REDACTED]");
    }

    [GeneratedRegex("(?i)([?&](?:access[_-]?token|refresh[_-]?token|token|enc|openc|signature|sign|auth|authorization|session(?:id)?|ticket|key)=)([^&#\\s\\\"']+)")]
    private static partial Regex QuerySecretRegex();

    [GeneratedRegex("(?i)(\\b(?:authorization|proxy-authorization)\\s*:\\s*(?:bearer\\s+)?)([^\\s,;]+)")]
    private static partial Regex HeaderSecretRegex();

    [GeneratedRegex("(?i)(\\b(?:cookie|set-cookie)\\s*:\\s*)([^\\r\\n]+)")]
    private static partial Regex CookieSecretRegex();
}
