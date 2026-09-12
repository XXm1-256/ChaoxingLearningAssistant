namespace ChaoxingLearningAssistant.Chaoxing;

public static class ChaoxingUrlClassifier
{
    public static bool IsLoginUri(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsLoginUri(uri);

    public static bool IsLoginUri(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
            return false;

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();
        var query = uri.Query.ToLowerInvariant();
        var authHost = host.StartsWith("passport", StringComparison.Ordinal) ||
                       host.StartsWith("sso.", StringComparison.Ordinal) ||
                       host.StartsWith("cas.", StringComparison.Ordinal) ||
                       host.StartsWith("auth.", StringComparison.Ordinal) ||
                       host.Contains(".sso.", StringComparison.Ordinal) ||
                       host.Contains(".cas.", StringComparison.Ordinal) ||
                       host.Contains(".auth.", StringComparison.Ordinal) ||
                       host.Contains("cas.chaoxing.com", StringComparison.Ordinal);

        // 学校统一认证页可能不在 chaoxing.com 域；仅对明显的认证主机/路径做跨域识别。
        if (path.Contains("/cas/login", StringComparison.Ordinal) ||
            path.Contains("/auth/login", StringComparison.Ordinal) ||
            (authHost && (path.EndsWith("/login", StringComparison.Ordinal) ||
                          path.Contains("/login/", StringComparison.Ordinal) ||
                          path.Contains("/tologin", StringComparison.Ordinal) ||
                          path == "/")))
            return true;

        if (!ChaoxingConstants.IsChaoxingUri(uri))
            return false;

        if (path.EndsWith("/login", StringComparison.Ordinal) ||
            path.Contains("/tologin", StringComparison.Ordinal))
            return true;

        return path == "/" &&
               (host.StartsWith("v20.", StringComparison.Ordinal) || host.StartsWith("v25.", StringComparison.Ordinal)) &&
               (query.Length == 0 || query.Contains("login", StringComparison.Ordinal));
    }

    public static bool IsStudyUri(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !ChaoxingConstants.IsChaoxingUri(uri) || IsLoginUri(uri))
            return false;

        var path = uri.AbsolutePath.ToLowerInvariant();
        var query = uri.Query.ToLowerInvariant();
        return path.Contains("studentstudy", StringComparison.Ordinal) ||
               path.Contains("nodedetail", StringComparison.Ordinal) ||
               path.Contains("/mooc-ans/", StringComparison.Ordinal) ||
               query.Contains("chapterid=", StringComparison.Ordinal) ||
               query.Contains("knowledgeid=", StringComparison.Ordinal);
    }

    public static bool MayContainChapterCatalogUri(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !ChaoxingConstants.IsChaoxingUri(uri) || IsLoginUri(uri))
            return false;

        if (IsStudyUri(url))
            return true;

        var path = uri.AbsolutePath.ToLowerInvariant();
        var query = uri.Query.ToLowerInvariant();
        return path.Contains("studentcourse", StringComparison.Ordinal) ||
               path.Contains("/visit/courses", StringComparison.Ordinal) ||
               (query.Contains("courseid=", StringComparison.Ordinal) &&
                (path.Contains("/mycourse/", StringComparison.Ordinal) ||
                 path.Contains("/course/", StringComparison.Ordinal)));
    }

    public static bool IsGenericHomeUri(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !ChaoxingConstants.IsChaoxingUri(uri) || IsLoginUri(uri))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        return path.Length == 0 || path.Equals("base", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("home", StringComparison.OrdinalIgnoreCase);
    }
}
