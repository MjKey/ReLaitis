using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Recognition;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Офлайн-распознаватель речи на базе встроенного в Windows компонента SAPI (System.Speech.Recognition).
/// </summary>
public class WindowsSapiSpeechRecognizer : ISpeechRecognizer
{
    public event EventHandler<string>? PartialResultRecognized;
    public event EventHandler<string>? FinalResultRecognized;

    private SpeechRecognitionEngine? _engine;
    private AudioStreamQueue? _audioStream;
    private readonly object _lock = new();

    public bool IsModelLoaded => _engine != null;

    public WindowsSapiSpeechRecognizer(string culture = "ru-RU")
    {
        Initialize(culture);
    }

    private void Initialize(string culture)
    {
        lock (_lock)
        {
            try
            {
                var installed = SpeechRecognitionEngine.InstalledRecognizers();
                if (installed == null || installed.Count == 0)
                {
                    return;
                }

                // Ищем распознаватель для запрашиваемой культуры (например ru-RU), иначе берем первый доступный
                var info = installed.FirstOrDefault(r => r.Culture.Name.Equals(culture, StringComparison.OrdinalIgnoreCase))
                           ?? installed.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase))
                           ?? installed.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
                           ?? installed[0];

                _engine = new SpeechRecognitionEngine(info.Id);
                _engine.LoadGrammar(new DictationGrammar());

                _engine.SpeechHypothesized += (s, e) =>
                {
                    if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        PartialResultRecognized?.Invoke(this, e.Result.Text.Trim());
                    }
                };

                _engine.SpeechRecognized += (s, e) =>
                {
                    if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        FinalResultRecognized?.Invoke(this, e.Result.Text.Trim());
                    }
                };

                _audioStream = new AudioStreamQueue();
                var format = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
                _engine.SetInputToAudioStream(_audioStream, format);
                _engine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch
            {
                _engine = null;
                _audioStream?.Dispose();
                _audioStream = null;
            }
        }
    }

    public bool LoadModel(string modelPath)
    {
        // Для SAPI загрузка внешней папки моделей не требуется - используется системный голос
        return IsModelLoaded;
    }

    public void ProcessAudio(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        lock (_lock)
        {
            _audioStream?.WriteData(data);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _audioStream?.Clear();
            try
            {
                _engine?.RecognizeAsyncCancel();
                if (_audioStream != null && _engine != null)
                {
                    var format = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
                    _engine.SetInputToAudioStream(_audioStream, format);
                    _engine.RecognizeAsync(RecognizeMode.Multiple);
                }
            }
            catch { }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _engine?.RecognizeAsyncCancel();
                _engine?.Dispose();
            }
            catch { }

            _audioStream?.Dispose();
            _engine = null;
            _audioStream = null;
        }
    }

    /// <summary>
    /// Потокобезопасный кольцевой буферный поток для передачи аудиоданных в SAPI RecognitionEngine.
    /// </summary>
    private sealed class AudioStreamQueue : Stream
    {
        private readonly MemoryStream _buffer = new();
        private readonly AutoResetEvent _dataAvailable = new(false);
        private readonly object _streamLock = new();
        private bool _isDisposed;

        public override bool CanRead => !_isDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => !_isDisposed;
        public override long Length => _buffer.Length;
        public override long Position
        {
            get => _buffer.Position;
            set => throw new NotSupportedException();
        }

        public void WriteData(byte[] data)
        {
            if (_isDisposed) return;
            lock (_streamLock)
            {
                var curPos = _buffer.Position;
                _buffer.Seek(0, SeekOrigin.End);
                _buffer.Write(data, 0, data.Length);
                _buffer.Position = curPos;
                _dataAvailable.Set();
            }
        }

        public void Clear()
        {
            lock (_streamLock)
            {
                _buffer.SetLength(0);
                _buffer.Position = 0;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (!_isDisposed)
            {
                lock (_streamLock)
                {
                    var available = _buffer.Length - _buffer.Position;
                    if (available > 0)
                    {
                        var bytesToRead = (int)Math.Min(available, count);
                        var bytesRead = _buffer.Read(buffer, offset, bytesToRead);

                        // Если прочитали весь буфер, сжимаем его к началу
                        if (_buffer.Position >= _buffer.Length)
                        {
                            _buffer.SetLength(0);
                            _buffer.Position = 0;
                        }
                        return bytesRead;
                    }
                }

                _dataAvailable.WaitOne(100);
            }
            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => WriteData(buffer[offset..(offset + count)]);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposed = true;
                _dataAvailable.Set();
                _dataAvailable.Dispose();
                lock (_streamLock)
                {
                    _buffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
