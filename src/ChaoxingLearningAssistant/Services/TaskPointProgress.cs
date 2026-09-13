using System.Text.RegularExpressions;

namespace ChaoxingLearningAssistant.Services;

/// <summary>Only explicit task-point totals are counts; chapter/video counts are not substitutes.</summary>
public sealed record TaskPointProgress(int Completed, int Total)
{
    public static TaskPointProgress? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text, @"任务点\s*[:：]?\s*(?:已完成\s*)?(\d+)\s*/\s*(\d+)");
        if (!match.Success)
            match = Regex.Match(text, @"已完成\s*(\d+)\s*(?:个)?\s*/\s*(?:共\s*)?(\d+)\s*(?:个)?\s*任务点");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var done) ||
            !int.TryParse(match.Groups[2].Value, out var total) || done > total) return null;
        return new(done, total);
    }
}
