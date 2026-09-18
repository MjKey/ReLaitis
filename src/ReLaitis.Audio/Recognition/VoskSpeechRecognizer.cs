using System.Text.Json;
using Vosk;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Офлайн-распознаватель речи на базе Vosk.
/// Работает с нулевой задержкой (<50 мс) на CPU без обращения к серверам.
/// </summary>
public class VoskSpeechRecognizer : ISpeechRecognizer
{
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private readonly object _lock = new();

    public event EventHandler<string>? PartialResultRecognized;
    public event EventHandler<string>? FinalResultRecognized;

    public bool IsModelLoaded => _model != null;

    static VoskSpeechRecognizer()
    {
        // Отключаем избыточный вывод логов Vosk в консоль
        Vosk.Vosk.SetLogLevel(-1);
    }

    public bool LoadModel(string modelPath)
    {
        lock (_lock)
        {
            if (!Directory.Exists(modelPath))
                return false;

            try
            {
                _recognizer?.Dispose();
                _model?.Dispose();

                _model = new Model(modelPath);
                _recognizer = new VoskRecognizer(_model, 16000.0f);
                return true;
            }
            catch
            {
                _model = null;
                _recognizer = null;
                return false;
            }
        }
    }

    public void ProcessAudio(byte[] data)
    {
        lock (_lock)
        {
            if (_recognizer == null)
                return;

            if (_recognizer.AcceptWaveform(data, data.Length))
            {
                var json = _recognizer.Result();
                var text = ExtractTextFromJson(json, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    FinalResultRecognized?.Invoke(this, text.Trim());
                }
            }
            else
            {
                var json = _recognizer.PartialResult();
                var partial = ExtractTextFromJson(json, "partial");
                if (!string.IsNullOrWhiteSpace(partial))
                {
                    PartialResultRecognized?.Invoke(this, partial.Trim());
                }
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            if (_recognizer != null)
            {
                var json = _recognizer.FinalResult();
                var text = ExtractTextFromJson(json, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    FinalResultRecognized?.Invoke(this, text.Trim());
                }
                _recognizer.Reset();
            }
        }
    }

    private static string ExtractTextFromJson(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // Игнорируем ошибки парсинга неполных JSON
        }
        return string.Empty;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _recognizer?.Dispose();
            _model?.Dispose();
            _recognizer = null;
            _model = null;
        }
    }
}
