using System.Text.Json;
using GpsImageExtractor.Models;
using GpsImageExtractor.Services;
using Xunit;

namespace GpsImageExtractor.Tests;

public sealed class ExtractionTests
{
    [Theory]
    [InlineData("35.220923，138.697007", 35.220923, 138.697007)]
    [InlineData("51 · 8815960 , -3 · 4435900", 51.881596, -3.44359)]
    [InlineData("５１．８８３８３８０，−３．４３６７０００", 51.883838, -3.4367)]
    [InlineData("-90.0, -180.0", -90, -180)]
    [InlineData("+90.0, +180.0", 90, 180)]
    [InlineData("25. 033964, 121. 564468", 25.033964, 121.564468)]
    public void ParsesDecimalCoordinates(string text, double latitude, double longitude)
    {
        var coordinate = Assert.Single(GpsParser.Parse(GpsParser.Normalize(text)));
        Assert.Equal(latitude, coordinate.Latitude);
        Assert.Equal(longitude, coordinate.Longitude);
    }

    [Theory]
    [InlineData("91.0, 121.0")]
    [InlineData("25.0, -181.0")]
    [InlineData("-90.1, 180.0")]
    [InlineData("3,883 點")]
    [InlineData("2026/08/26")]
    [InlineData("")]
    [InlineData("25, 121")]
    [InlineData("25°02'03\"N 121°03'04\"E")]
    public void RejectsInvalidOrUnsupportedCoordinates(string text)
    {
        Assert.Throws<FormatException>(() => GpsParser.Parse(GpsParser.Normalize(text)));
    }

    [Fact]
    public void ExtractsFlowerScreenshotWithEmptyDescription()
    {
        var marker = Assert.Single(MarkerParser.Parse("""
            皮 克 敏 (Pikmin
            Bloo
            Chi-Chih Chen · 22 小 時 ·
            富 士 山 岳 別 針
            35 · 220923 , 138 · 697007
            灑 了 800 朵 花 , 終 於 可 以 看 到 〔 富 士
            """));
        Assert.Equal(35.220923, marker.Latitude);
        Assert.Equal(138.697007, marker.Longitude);
        Assert.Equal("花點", marker.Title);
        Assert.Empty(marker.Description);
        Assert.True(marker.IsFavorite);
        Assert.True(Guid.TryParseExact(marker.Uuid, "D", out _));
        Assert.Equal('4', marker.Uuid[14]);
    }

    [Fact]
    public void ExtractsBothMushroomScreenshotMarkersWithSeparateTags()
    {
        var markers = MarkerParser.Parse("""
            朱 智 豪 · 1 天 ·
            51 · 8815960 , -3 · 4435900
            # 雪 景
            51 · 8838380 , -3 · 4367000
            # 太 陽
            Unnamed Road
            小 藍 色 菇
            小 黃 色 菇
            """);
        Assert.Equal(2, markers.Count);
        Assert.Equal(51.881596, markers[0].Latitude);
        Assert.Equal(-3.44359, markers[0].Longitude);
        Assert.Equal("#雪景", markers[0].Description);
        Assert.Equal(51.883838, markers[1].Latitude);
        Assert.Equal(-3.4367, markers[1].Longitude);
        Assert.Equal("#太陽", markers[1].Description);
        Assert.All(markers, marker => Assert.Equal("茹點", marker.Title));
        Assert.NotEqual(markers[0].Uuid, markers[1].Uuid);
    }

    [Fact]
    public void KeepsDescriptionEmptyWhenOnlyCoordinatesArePresent()
    {
        var marker = Assert.Single(MarkerParser.Parse("25.0, 121.0"));
        Assert.Empty(marker.Description);
        Assert.Equal("花點", marker.Title);
    }

    [Fact]
    public void LocalClassificationTakesPriorityOverWholeImage()
    {
        var markers = MarkerParser.Parse("25.0, 121.0\n#花點\n26.0, 122.0\n#茹點");
        Assert.Equal("花點", markers[0].Title);
        Assert.Equal("茹點", markers[1].Title);
        Assert.Equal("#花點", markers[0].Description);
        Assert.Equal("#茹點", markers[1].Description);
    }

    [Theory]
    [InlineData("25.0, 121.0\n一般文字，沒有標籤", "")]
    [InlineData("25.0, 121.0\n#新加坡 # 清真寺 # 花點", "#新加坡 #清真寺 #花點")]
    [InlineData("25.0, 121.0\n說明：# 雪 景！其他文字\n# 太 陽", "#雪景 #太陽")]
    [InlineData("＃花點\n25.0, 121.0", "#花點")]
    [InlineData("25.0, 121.0\n#雪景 #雪景\n#太陽", "#雪景 #太陽")]
    [InlineData("25.0, 121.0\n#PikminBloom #mountain_123", "#PikminBloom #mountain_123")]
    [InlineData("25.0, 121.0\n只有 #，沒有標籤", "")]
    [InlineData("#旅遊\n25.0, 121.0\n#雪景", "#旅遊 #雪景")]
    public void DescriptionContainsOnlyHashtags(string text, string expected)
    {
        var marker = Assert.Single(MarkerParser.Parse(text));
        Assert.Equal(expected, marker.Description);
    }

    [Fact]
    public void DoesNotCopyPreviousCoordinateTagsToNextMarker()
    {
        var markers = MarkerParser.Parse("25.0, 121.0\n#花點\n26.0, 122.0\n一般文字");
        Assert.Equal("#花點", markers[0].Description);
        Assert.Empty(markers[1].Description);
    }

    [Fact]
    public void InvalidCoordinateDoesNotProducePartialImageResults()
    {
        Assert.Throws<FormatException>(() => MarkerParser.Parse("25.0, 121.0\n91.0, 122.0"));
    }

    [Fact]
    public async Task WritesExactJsonSchemaAndOverwritesAtomically()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GpsImageExtractorTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "GPS.json");
        try
        {
            IReadOnlyList<GpsMarker> markers = MarkerParser.Parse("富士山岳別針\n35.220923, 138.697007");
            await JsonWriter.WriteAsync(path, markers);
            string json = await File.ReadAllTextAsync(path);
            using var document = JsonDocument.Parse(json);
            var marker = Assert.Single(document.RootElement.EnumerateArray());
            Assert.Equal(new[]
            {
                "marker_uuid", "marker_latitude", "marker_longitude",
                "marker_is_favorite", "marker_favorite_title", "marker_favorite_description"
            }, marker.EnumerateObject().Select(property => property.Name));
            Assert.Equal(JsonValueKind.Number, marker.GetProperty("marker_latitude").ValueKind);
            Assert.Equal(JsonValueKind.Number, marker.GetProperty("marker_longitude").ValueKind);
            Assert.True(marker.GetProperty("marker_is_favorite").GetBoolean());
            Assert.Equal("", marker.GetProperty("marker_favorite_description").GetString());
            await JsonWriter.WriteAsync(path, Array.Empty<GpsMarker>());
            Assert.Equal("[]", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WritesHashtagsToJsonDescription()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GpsImageExtractorTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "GPS.json");
        try
        {
            await JsonWriter.WriteAsync(path, MarkerParser.Parse("25.0, 121.0\n#雪景 #太陽"));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal("#雪景 #太陽",
                document.RootElement[0].GetProperty("marker_favorite_description").GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ScansSupportedExtensionsAndHonorsRecursion()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GpsImageExtractorTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "nested"));
            File.WriteAllText(Path.Combine(directory, "one.PNG"), "");
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "");
            File.WriteAllText(Path.Combine(directory, "nested", "two.jpg"), "");
            Assert.Single(ImageScanner.Scan(directory, recursive: false));
            Assert.Equal(2, ImageScanner.Scan(directory, recursive: true).Count);
            Assert.Throws<DirectoryNotFoundException>(() =>
                ImageScanner.Scan(Path.Combine(directory, "missing"), recursive: false));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
