using System.Text;
using System.Text.Json.Serialization;
using GpsImageExtractor.Models;
using GpsImageExtractor.Services;

Console.OutputEncoding = Encoding.UTF8;
return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Contains("--help") || args.Contains("-h"))
    {
        Console.WriteLine("""
            圖片 GPS 擷取工具（Windows 本機 OCR）
            dotnet run -- [圖片目錄] [輸出目錄] [--recursive] [--language zh-Hant-TW]
            預設：目前目錄下的 Input、Output；不掃描子目錄。
            無法辨識有效座標的圖片會移至目前目錄下的 Check，掃描時排除此目錄。
            每次執行會重新產生 GPS.json、errors.json、ocr.json，並為每筆地點產生新 UUID。
            """);
        return 0;
    }

    try
    {
        bool recursive = false;
        string language = "zh-Hant-TW";
        var paths = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--recursive":
                    recursive = true;
                    break;
                case "--language":
                    if (++i >= args.Length || args[i].StartsWith("--"))
                        throw new ArgumentException("--language 後需提供語言代碼。");
                    language = args[i];
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        throw new ArgumentException($"未知選項：{args[i]}");
                    paths.Add(args[i]);
                    break;
            }
        }

        if (paths.Count > 2)
            throw new ArgumentException("最多只能提供圖片目錄及輸出目錄。使用 --help 查看用法。");

        string input = Path.GetFullPath(paths.Count > 0 ? paths[0] : "Input");
        string output = Path.GetFullPath(paths.Count > 1 ? paths[1] : "Output");
        string checkDirectory = Path.GetFullPath("Check");
        var files = ImageScanner.Scan(input, recursive, checkDirectory);
        if (files.Count == 0)
            throw new InvalidOperationException($"目錄中沒有支援的圖片：{input}");

        var ocr = new OcrService(language);
        var markers = new List<GpsMarker>();
        var errors = new List<ExtractionError>();
        var recognized = new List<OcrText>();
        foreach (string file in files)
        {
            string? text = null;
            try
            {
                text = await ocr.RecognizeAsync(file);
                recognized.Add(new OcrText(file, text));
                var parsed = MarkerParser.Parse(text);
                markers.AddRange(parsed);
                Console.WriteLine($"{Path.GetFileName(file)}：擷取 {parsed.Count} 筆");
            }
            catch (Exception ex) when (ex is FormatException or IOException
                or UnauthorizedAccessException or ArgumentException
                or System.Runtime.InteropServices.COMException)
            {
                string? checkFile = null;
                string message = ex.Message;
                if (ex is FormatException)
                {
                    try
                    {
                        checkFile = ImageScanner.MoveToCheck(file, checkDirectory);
                        Console.Error.WriteLine($"{Path.GetFileName(file)}：已移至 {checkFile}");
                    }
                    catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
                    {
                        message += $" 移至 Check 失敗：{moveError.Message}";
                    }
                }

                errors.Add(new ExtractionError(file, message, text, checkFile));
                Console.Error.WriteLine($"{Path.GetFileName(file)}：失敗：{message}");
            }
        }

        await JsonWriter.WriteAsync(Path.Combine(output, "GPS.json"), markers);
        await JsonWriter.WriteAsync(Path.Combine(output, "errors.json"), errors);
        await JsonWriter.WriteAsync(Path.Combine(output, "ocr.json"), recognized);
        Console.WriteLine($"完成：{markers.Count} 筆地點，{errors.Count} 張失敗。輸出：{output}");
        return errors.Count == 0 ? 0 : 2;
    }
    catch (Exception ex) when (ex is ArgumentException or IOException
        or UnauthorizedAccessException or InvalidOperationException
        or System.Runtime.InteropServices.COMException)
    {
        Console.Error.WriteLine($"錯誤：{ex.Message}");
        return 1;
    }
}

internal sealed record ExtractionError(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("ocr_text")] string? OcrText,
    [property: JsonPropertyName("check_file")] string? CheckFile);

internal sealed record OcrText(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("text")] string Text);