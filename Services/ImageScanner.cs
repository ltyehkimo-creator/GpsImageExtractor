namespace GpsImageExtractor.Services;

public static class ImageScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"
    };

    public static IReadOnlyList<string> Scan(string directory, bool recursive, string? excludedDirectory = null)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"找不到圖片目錄：{directory}");

        string? excludedPrefix = excludedDirectory is null ? null :
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(excludedDirectory)) + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(directory, "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(path => excludedPrefix is null ||
                !Path.GetFullPath(path).StartsWith(excludedPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(path => Extensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string MoveToCheck(string file, string checkDirectory)
    {
        Directory.CreateDirectory(checkDirectory);
        string destination = Path.Combine(checkDirectory, Path.GetFileName(file));
        if (File.Exists(destination))
        {
            destination = Path.Combine(checkDirectory,
                $"{Path.GetFileNameWithoutExtension(file)}_{Guid.NewGuid():N}{Path.GetExtension(file)}");
        }

        File.Move(file, destination, overwrite: false);
        return Path.GetFullPath(destination);
    }
}