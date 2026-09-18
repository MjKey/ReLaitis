using System.IO.Compression;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Менеджер загрузки и управления офлайн-моделями распознавания речи Vosk.
/// </summary>
public static class ModelManager
{
    public const string DefaultModelUrl = "https://huggingface.co/rhasspy/vosk-models/resolve/main/ru/vosk-model-small-ru-0.22.zip";
    public const string FallbackModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip";
    public const string DefaultModelFolderName = "vosk-model-small-ru-0.22";

    public static string GetModelsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "ReLaitis", "Models");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetDefaultModelPath()
    {
        return Path.Combine(GetModelsDirectory(), DefaultModelFolderName);
    }

    public static bool IsModelInstalled()
    {
        var path = GetDefaultModelPath();
        if (!Directory.Exists(path))
            return false;

        // Проверяем наличие ключевых файлов модели Vosk
        var hasAm = Directory.Exists(Path.Combine(path, "am")) || File.Exists(Path.Combine(path, "final.mdl"));
        var hasConf = Directory.Exists(Path.Combine(path, "conf")) || File.Exists(Path.Combine(path, "model.conf"));

        return hasAm || hasConf;
    }

    public static async Task<string> EnsureModelInstalledAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = GetDefaultModelPath();

        if (IsModelInstalled())
        {
            progress?.Report(1.0);
            return targetDir;
        }

        var modelsRoot = GetModelsDirectory();
        var tempZip = Path.Combine(modelsRoot, "vosk-model-small-ru-0.22.zip");

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(DefaultModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 46236750L;

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[16384];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;
                    progress?.Report((double)totalRead / totalBytes);
                }
            }

            // Распаковываем модель
            ZipFile.ExtractToDirectory(tempZip, modelsRoot, overwriteFiles: true);

            return targetDir;
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }
}
