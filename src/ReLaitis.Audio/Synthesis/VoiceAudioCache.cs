using System.IO;

namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Дисковый кэш озвученных фраз (канонический аналог rg.cs из оригинального Laitis).
/// Кэширует короткие системные фразы (<= 50 символов) для мгновенного отклика (0 мс задержки).
/// </summary>
public static class VoiceAudioCache
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReLaitis",
        "VoiceCache"
    );

    public const int MaxCachedPhraseLength = 50;

    public static string GetCacheDirectory(string voiceKey)
    {
        var cleanKey = SanitizeFileName(voiceKey);
        var path = Path.Combine(CacheRoot, cleanKey);
        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch { }
        }
        return path;
    }

    public static byte[]? GetCachedAudio(string phrase, string voiceKey, bool isWav = false)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > MaxCachedPhraseLength)
            return null;

        try
        {
            var dir = GetCacheDirectory(voiceKey);
            var fileName = SanitizeFileName(phrase.Trim()) + (isWav ? ".wav" : ".mp3");
            var fullPath = Path.Combine(dir, fileName);

            if (File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                if (fi.Length > 100)
                {
                    return File.ReadAllBytes(fullPath);
                }
            }
        }
        catch { }

        return null;
    }

    public static void SaveAudio(string phrase, string voiceKey, byte[] audioData, bool isWav = false)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > MaxCachedPhraseLength || audioData == null || audioData.Length < 100)
            return;

        try
        {
            var dir = GetCacheDirectory(voiceKey);
            var fileName = SanitizeFileName(phrase.Trim()) + (isWav ? ".wav" : ".mp3");
            var fullPath = Path.Combine(dir, fileName);

            File.WriteAllBytes(fullPath, audioData);
        }
        catch { }
    }

    public static void ClearCache()
    {
        try
        {
            if (Directory.Exists(CacheRoot))
            {
                Directory.Delete(CacheRoot, true);
            }
        }
        catch { }
    }

    public static long GetCacheSizeBytes()
    {
        try
        {
            if (!Directory.Exists(CacheRoot))
                return 0;

            var di = new DirectoryInfo(CacheRoot);
            return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string SanitizeFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = text.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
