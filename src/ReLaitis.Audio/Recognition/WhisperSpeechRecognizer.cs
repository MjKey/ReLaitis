using Whisper.net;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Офлайн-распознаватель речи на базе OpenAI Whisper (Whisper.net).
/// Обеспечивает максимальную точность распознавания русской речи и диктовки.
/// </summary>
public class WhisperSpeechRecognizer : ISpeechRecognizer
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private readonly object _lock = new();

    private readonly List<float> _audioBuffer = [];
    private bool _hasSpoken = false;
    private int _silenceSamples = 0;
    private bool _isProcessing = false;

    // Параметры голосовой детекции (VAD)
    private const int SampleRate = 16000;
    private const float SilenceThresholdRms = 0.012f;
    private const int MinSpeechSamples = SampleRate / 3; // минимум 300 мс речи
    private const int MaxSilenceSamples = (int)(SampleRate * 0.65); // 650 мс тишины для завершения фразы
    private const int MaxBufferSamples = SampleRate * 12; // максимум 12 секунд на фразу

#pragma warning disable CS0067
    public event EventHandler<string>? PartialResultRecognized;
#pragma warning restore CS0067
    public event EventHandler<string>? FinalResultRecognized;

    public bool IsModelLoaded => _processor != null;

    public bool LoadModel(string modelPath)
    {
        lock (_lock)
        {
            if (!File.Exists(modelPath))
                return false;

            try
            {
                _processor?.Dispose();
                _factory?.Dispose();

                _factory = WhisperFactory.FromPath(modelPath);
                _processor = _factory.CreateBuilder()
                    .WithLanguage("ru")
                    .WithNoSpeechThreshold(0.6f)
                    .Build();

                return true;
            }
            catch
            {
                _processor = null;
                _factory = null;
                return false;
            }
        }
    }

    public void ProcessAudio(byte[] data)
    {
        if (_processor == null || data.Length < 2)
            return;

        // Конвертация 16-bit PCM в нормализованный float [-1.0, 1.0]
        var samplesCount = data.Length / 2;
        var samples = new float[samplesCount];
        double sumSquares = 0;

        for (var i = 0; i < samplesCount; i++)
        {
            short pcm = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            var val = pcm / 32768.0f;
            samples[i] = val;
            sumSquares += val * val;
        }

        var rms = (float)Math.Sqrt(sumSquares / samplesCount);

        float[]? samplesToProcess = null;

        lock (_lock)
        {
            _audioBuffer.AddRange(samples);

            if (rms > SilenceThresholdRms)
            {
                _hasSpoken = true;
                _silenceSamples = 0;
            }
            else if (_hasSpoken)
            {
                _silenceSamples += samplesCount;
            }

            // Проверяем, завершилась ли фраза паузой или достигнут максимальный лимит буфера
            if (_hasSpoken && (_silenceSamples >= MaxSilenceSamples || _audioBuffer.Count >= MaxBufferSamples))
            {
                if (_audioBuffer.Count >= MinSpeechSamples && !_isProcessing)
                {
                    samplesToProcess = _audioBuffer.ToArray();
                    _audioBuffer.Clear();
                    _hasSpoken = false;
                    _silenceSamples = 0;
                }
            }
        }

        if (samplesToProcess != null)
        {
            _ = RunInferenceAsync(samplesToProcess);
        }
    }

    public void Reset()
    {
        float[]? samplesToProcess = null;

        lock (_lock)
        {
            if (_hasSpoken && _audioBuffer.Count >= MinSpeechSamples && !_isProcessing)
            {
                samplesToProcess = _audioBuffer.ToArray();
            }
            _audioBuffer.Clear();
            _hasSpoken = false;
            _silenceSamples = 0;
        }

        if (samplesToProcess != null)
        {
            _ = RunInferenceAsync(samplesToProcess);
        }
    }

    private async Task RunInferenceAsync(float[] samples)
    {
        WhisperProcessor? proc;
        lock (_lock)
        {
            if (_isProcessing || _processor == null)
                return;

            _isProcessing = true;
            proc = _processor;
        }

        try
        {
            var segments = new List<string>();

            await foreach (var segment in proc.ProcessAsync(samples))
            {
                var segText = segment.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(segText))
                {
                    segments.Add(segText);
                }
            }

            var fullText = string.Join(" ", segments).Trim();
            fullText = CleanWhisperOutput(fullText);

            if (!string.IsNullOrWhiteSpace(fullText))
            {
                FinalResultRecognized?.Invoke(this, fullText);
            }
        }
        catch
        {
            // Ошибки инференса не должны ломать поток аудио
        }
        finally
        {
            lock (_lock)
            {
                _isProcessing = false;
            }
        }
    }

    private static string CleanWhisperOutput(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Удаление типичных артефактов Whisper
        var clean = text
            .Replace("[BLANK_AUDIO]", "")
            .Replace("[MUSIC]", "")
            .Replace("(музыка)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(аплодисменты)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(смех)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return clean;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _processor?.Dispose();
            _factory?.Dispose();
            _processor = null;
            _factory = null;
            _audioBuffer.Clear();
        }
    }
}
