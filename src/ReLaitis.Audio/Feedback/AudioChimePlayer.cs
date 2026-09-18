using NAudio.Wave;
using ReLaitis.Core.Enums;

namespace ReLaitis.Audio.Feedback;

/// <summary>
/// Генератор и проигрыватель коротких процедурных звуковых сигналов обратной связи (Audio Chimes).
/// Не требует внешних wav-файлов и воспроизводится с нулевой задержкой.
/// </summary>
public class AudioChimePlayer : IDisposable
{
    private WaveOut? _chimeWaveOut;
    private readonly object _lock = new();

    public void PlayChime(ChimeType type)
    {
        lock (_lock)
        {
            try
            {
                var (freqs, durationMs, volume) = type switch
                {
                    ChimeType.ListeningStart => (new[] { 587.33, 880.0 }, 90, 0.20f),
                    ChimeType.Success => (new[] { 523.25, 659.25 }, 110, 0.22f),
                    ChimeType.Error => (new[] { 220.0 }, 130, 0.18f),
                    _ => (new[] { 440.0 }, 100, 0.20f)
                };

                const int sampleRate = 22050;
                var pcmBytes = GenerateSineWavePcm(sampleRate, freqs, durationMs, volume);

                _chimeWaveOut?.Stop();
                _chimeWaveOut?.Dispose();

                var waveFormat = new WaveFormat(sampleRate, 16, 1);
                var ms = new MemoryStream(pcmBytes);
                var rawStream = new RawSourceWaveStream(ms, waveFormat);

                _chimeWaveOut = new WaveOut();
                _chimeWaveOut.Init(rawStream);
                _chimeWaveOut.Play();
            }
            catch
            {
                // Игнорируем возможные временные сбои звукового устройства
            }
        }
    }

    private static byte[] GenerateSineWavePcm(int sampleRate, double[] frequencies, int durationMs, float volume)
    {
        var sampleCount = (sampleRate * durationMs) / 1000;
        var bytes = new byte[sampleCount * 2];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRate;
            var window = Math.Sin(Math.PI * i / sampleCount); // Плавное затухание для исключения щелчков
            double sampleVal = 0;
            foreach (var freq in frequencies)
            {
                sampleVal += Math.Sin(2 * Math.PI * freq * t);
            }
            sampleVal /= frequencies.Length;

            var sample16 = (short)(sampleVal * window * volume * short.MaxValue);
            bytes[i * 2] = (byte)(sample16 & 0xFF);
            bytes[i * 2 + 1] = (byte)((sample16 >> 8) & 0xFF);
        }

        return bytes;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _chimeWaveOut?.Stop();
            _chimeWaveOut?.Dispose();
            _chimeWaveOut = null;
        }
    }
}
