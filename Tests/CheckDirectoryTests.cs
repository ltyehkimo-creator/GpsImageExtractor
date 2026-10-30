using GpsImageExtractor.Services;
using Xunit;

namespace GpsImageExtractor.Tests;

public sealed class CheckDirectoryTests
{
    [Fact]
    public void MovesImageWithoutOverwritingExistingFilesAndExcludesCheckFromScan()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GpsImageExtractorTests-" + Guid.NewGuid().ToString("N"));
        string check = Path.Combine(directory, "Check");
        string source = Path.Combine(directory, "image.png");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(source, "first");
            string first = ImageScanner.MoveToCheck(source, check);
            Assert.False(File.Exists(source));
            Assert.Equal("first", File.ReadAllText(first));

            File.WriteAllText(source, "second");
            string second = ImageScanner.MoveToCheck(source, check);
            Assert.NotEqual(first, second);
            Assert.False(File.Exists(source));
            Assert.Equal("first", File.ReadAllText(first));
            Assert.Equal("second", File.ReadAllText(second));
            Assert.Equal(".png", Path.GetExtension(second));
            Assert.Equal(2, ImageScanner.Scan(directory, recursive: true).Count);
            Assert.Empty(ImageScanner.Scan(directory, recursive: true, excludedDirectory: check));
            Assert.Throws<FileNotFoundException>(() => ImageScanner.MoveToCheck(source, check));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
