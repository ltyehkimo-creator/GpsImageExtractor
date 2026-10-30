using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GpsImageExtractor.Services;

public sealed record GpsCoordinate(double Latitude, double Longitude, int Start, int Length);

public static partial class GpsParser
{
    // Decimal points are required to avoid treating dates and image counters as GPS.
    [GeneratedRegex(@"(?<![\d.\p{L}])(?<lat>[+-]?\d{1,3}\s*\.\s*\d+)\s*[,、;]\s*(?<lon>[+-]?\d{1,3}\s*\.\s*\d+)(?![\d.\p{L}])")]
    private static partial Regex CoordinatePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"(?<=\d)\s*·\s*(?=\d)")]
    private static partial Regex OcrDecimalPointPattern();

    public static string Normalize(string text) =>
        OcrDecimalPointPattern().Replace(
            text.Normalize(NormalizationForm.FormKC).Replace('\u2212', '-'), ".");

    public static IReadOnlyList<GpsCoordinate> Parse(string normalizedText)
    {
        var coordinates = new List<GpsCoordinate>();
        foreach (Match match in CoordinatePattern().Matches(normalizedText))
        {
            double latitude = double.Parse(
                WhitespacePattern().Replace(match.Groups["lat"].Value, ""),
                CultureInfo.InvariantCulture);
            double longitude = double.Parse(
                WhitespacePattern().Replace(match.Groups["lon"].Value, ""),
                CultureInfo.InvariantCulture);
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            {
                throw new FormatException($"GPS 座標超出有效範圍：{match.Value}");
            }

            coordinates.Add(new GpsCoordinate(latitude, longitude, match.Index, match.Length));
        }

        if (coordinates.Count == 0)
        {
            throw new FormatException("未辨識到十進位 GPS 座標（預期格式：緯度, 經度）。");
        }

        return coordinates;
    }
}