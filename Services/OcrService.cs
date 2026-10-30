using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace GpsImageExtractor.Services;

public sealed class OcrService
{
    private readonly OcrEngine engine;

    public OcrService(string languageTag)
    {
        var language = new Language(languageTag);
        engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new InvalidOperationException(
                $"未安裝 Windows OCR 語言「{languageTag}」。" +
                "請在 Windows 設定 > 時間與語言 > 語言與地區安裝該語言的基本輸入／文字辨識功能。" +
                $"目前可用：{string.Join(", ", OcrEngine.AvailableRecognizerLanguages.Select(item => item.LanguageTag))}");
    }

    public async Task<string> RecognizeAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        double scale = Math.Min(1, (double)OcrEngine.MaxImageDimension /
            Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale))
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var result = await engine.RecognizeAsync(bitmap);
        if (result.Lines.Count == 0)
            throw new FormatException("OCR 未辨識到任何文字。");

        return string.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
    }
}