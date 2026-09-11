using System.Text.Json;

namespace ChaoxingLearningAssistant.Services;

internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static T LoadOrDefault<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path))
                return TryLoad(path + ".bak", fallback);

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? TryLoad(path + ".bak", fallback);
        }
        catch
        {
            return TryLoad(path + ".bak", fallback);
        }
    }

    private static T TryLoad<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback;
        }
        catch { return fallback; }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var backup = path + ".bak";
        var json = JsonSerializer.Serialize(value, Options);
        try
        {
            File.WriteAllText(temp, json);
            // Only a value readable as the same model may replace the recovery copy.
            if (File.Exists(path) && IsValidJson<T>(path))
                File.Copy(path, backup, true);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool IsValidJson<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) is not null;
        }
        catch { return false; }
    }
}
