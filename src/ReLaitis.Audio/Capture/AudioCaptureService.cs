using NAudio.Wave;

namespace ReLaitis.Audio.Capture;

/// <summary>
/// Сервис захвата аудиопотока с микрофона (16kHz 16-bit Mono PCM)
/// с расчетом уровня громкости в реальном времени.
/// </summary>
public class AudioCaptureService : IDisposable
{
    private WaveIn? _waveIn;
    private bool _isCapturing;

    public const int SampleRate = 16000;
    public const int BitsPerSample = 16;
    public const int Channels = 1;

    public event EventHandler<byte[]>? AudioDataAvailable;
    public event EventHandler<float>? AudioLevelChanged;

    public bool IsCapturing => _isCapturing;
    public int CurrentDeviceIndex { get; private set; } = 0;

    public static List<(int DeviceIndex, string Name)> GetAvailableInputDevices()
    {
        var list = new List<(int DeviceIndex, string Name)>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            list.Add((i, caps.ProductName));
        }
        return list;
    }

    public static List<(int DeviceId, string ProductName)> GetAvailableDevices() =>
        GetAvailableInputDevices().Select(d => (d.DeviceIndex, d.Name)).ToList();

    public int SelectedDeviceId
    {
        get => CurrentDeviceIndex;
        set => SetDevice(value);
    }

    public void SetDevice(int deviceNumber)
    {
        CurrentDeviceIndex = Math.Clamp(deviceNumber, 0, Math.Max(0, WaveIn.DeviceCount - 1));
        if (_isCapturing)
        {
            StopCapture();
            StartCapture(CurrentDeviceIndex);
        }
    }

    public void StartCapture(int? deviceNumber = null)
    {
        if (_isCapturing)
            return;

        if (WaveIn.DeviceCount == 0)
            throw new InvalidOperationException("Аудиоустройства ввода (микрофоны) не обнаружены.");

        var dev = deviceNumber ?? CurrentDeviceIndex;
        CurrentDeviceIndex = Math.Clamp(dev, 0, WaveIn.DeviceCount - 1);

        _waveIn = new WaveIn
        {
            DeviceNumber = CurrentDeviceIndex,
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _isCapturing = true;
    }

    public void StopCapture()
    {
        if (!_isCapturing || _waveIn == null)
            return;

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _waveIn = null;
        _isCapturing = false;
        AudioLevelChanged?.Invoke(this, 0f);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;

        // Расчет пикового уровня громкости для индикатора HUD
        short maxSample = 0;
        for (var i = 0; i < e.BytesRecorded - 1; i += 2)
        {
            var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            var abs = Math.Abs(sample);
            if (abs > maxSample)
                maxSample = abs;
        }

        var level = Math.Min(1.0f, (float)maxSample / 32767f);
        AudioLevelChanged?.Invoke(this, level);

        // Передаем буфер аудиоданных подписчикам (STT)
        var bufferCopy = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, bufferCopy, e.BytesRecorded);
        AudioDataAvailable?.Invoke(this, bufferCopy);
    }

    public void Dispose()
    {
        StopCapture();
    }
}
