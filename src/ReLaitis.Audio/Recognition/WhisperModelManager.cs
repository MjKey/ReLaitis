using Whisper.net.Ggml;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Менеджер загрузки и управления офлайн-моделями Whisper (GGML формат).
/// </summary>
public static class WhisperModelManager
{
    public static string GetModelsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "ReLaitis", "Models", "Whisper");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetModelPath(string modelName = "base")
    {
        var clean = modelName.Trim().ToLowerInvariant();
        return Path.Combine(GetModelsDirectory(), $"ggml-{clean}.bin");
    }

    public static bool IsModelInstalled(string modelName = "base")
    {
        var path = GetModelPath(modelName);
        if (!File.Exists(path)) return false;

        var fi = new FileInfo(path);
        // Модель tiny весит >70 МБ, base >140 МБ
        return fi.Length > 10 * 1024 * 1024;
    }

    public static async Task<string> EnsureModelInstalledAsync(
        string modelName = "base",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var targetFile = GetModelPath(modelName);

        if (IsModelInstalled(modelName))
        {
            progress?.Report(1.0);
            return targetFile;
        }

        var ggmlType = modelName.ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "small" => GgmlType.Small,
            _ => GgmlType.Base
        };

        // Загрузка через встроенный загрузчик Whisper.net с HuggingFace
        await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
            ggmlType,
            QuantizationType.NoQuantization,
            cancellationToken);

        var tempFile = targetFile + ".tmp";
        try
        {
            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
            {
                var buffer = new byte[32768];
                long totalRead = 0;
                // Приблизительные размеры моделей для шкалы прогресса
                long approximateSize = ggmlType switch
                {
                    GgmlType.Tiny => 75 * 1024 * 1024,
                    GgmlType.Small => 466 * 1024 * 1024,
                    _ => 142 * 1024 * 1024
                };

                int read;
                while ((read = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;
                    progress?.Report(Math.Min(1.0, (double)totalRead / approximateSize));
                }
            }

            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            File.Move(tempFile, targetFile);
            progress?.Report(1.0);
            return targetFile;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
