using System.Text.RegularExpressions;
using GpsImageExtractor.Models;

namespace GpsImageExtractor.Services;

public static partial class MarkerParser
{
    [GeneratedRegex(@"(?<=[\p{IsCJKUnifiedIdeographs}]) +(?=[\p{IsCJKUnifiedIdeographs}])")]
    private static partial Regex ChineseSpacesPattern();

    [GeneratedRegex(@"菇|茹\s*點|mushroom", RegexOptions.IgnoreCase)]
    private static partial Regex MushroomPattern();

    [GeneratedRegex(@"花\s*點|flower|大\s*花", RegexOptions.IgnoreCase)]
    private static partial Regex FlowerPattern();

    [GeneratedRegex(@"留言|分享|讚|小時|天\s*[·‧•]|Pikmin|Bloo|^\s*\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex InterfacePattern();

    [GeneratedRegex(@"^#\s+")]
    private static partial Regex HashtagSpacesPattern();

    [GeneratedRegex(@"#\s*[\p{L}\p{M}\p{N}_]+")]
    private static partial Regex HashtagPattern();

    public static IReadOnlyList<GpsMarker> Parse(string ocrText)
    {
        string text = GpsParser.Normalize(ocrText).Replace("\r\n", "\n");
        var coordinates = GpsParser.Parse(text);
        var markers = new List<GpsMarker>();
        for (int i = 0; i < coordinates.Count; i++)
        {
            var coordinate = coordinates[i];
            int previousEnd = i == 0 ? 0 : coordinates[i - 1].Start + coordinates[i - 1].Length;
            int end = coordinate.Start + coordinate.Length;
            int nextStart = i + 1 == coordinates.Count ? text.Length : coordinates[i + 1].Start;
            string before = text[previousEnd..coordinate.Start];
            string after = text[end..nextStart];

            string description = FindDescription(before, after, i == 0);
            string title = Classify(description, text);
            markers.Add(new GpsMarker(
                Guid.NewGuid().ToString("D"),
                coordinate.Latitude,
                coordinate.Longitude,
                true,
                title,
                FindHashtags(i == 0 ? before + "\n" + after : after)));
        }

        return markers;
    }

    private static string FindHashtags(string text)
    {
        var tags = text.Split('\n')
            .Select(line => ChineseSpacesPattern().Replace(line, ""))
            .SelectMany(line => HashtagPattern().Matches(line).Cast<Match>())
            .Select(match => HashtagSpacesPattern().Replace(match.Value, "#"))
            .Distinct(StringComparer.Ordinal);
        return string.Join(" ", tags);
    }

    private static string Classify(string description, string fullText)
    {
        if (MushroomPattern().IsMatch(description))
            return "茹點";
        if (FlowerPattern().IsMatch(description))
            return "花點";
        return MushroomPattern().IsMatch(fullText) ? "茹點" : "花點";
    }

    private static string FindDescription(string before, string after, bool first)
    {
        var following = CleanLines(after);
        // Hashtags in the supplied screenshots label the coordinate immediately above them.
        if (following.Length > 0 && following[0].StartsWith('#'))
            return following[0];

        var preceding = CleanLines(before);
        if (first && preceding.Length > 0 && !preceding[^1].StartsWith('#'))
            return preceding[^1];

        return following.Length > 0 ? following[0] : "";
    }

    private static string[] CleanLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => HashtagSpacesPattern().Replace(ChineseSpacesPattern().Replace(line, ""), "#"))
            .Where(line => !InterfacePattern().IsMatch(line))
            .ToArray();
}