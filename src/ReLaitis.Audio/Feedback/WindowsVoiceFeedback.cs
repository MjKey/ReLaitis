using System.Speech.Synthesis;
using NAudio.Wave;
using ReLaitis.Core.Interfaces;

namespace ReLaitis.Audio.Feedback;

/// <summary>
/// Реализация локального синтеза речи (TTS) через Windows SAPI (System.Speech) и воспроизведения звуков.
/// 100% офлайн, использует системные голоса Windows (Irina, Elena, Pavel, David и др.).
/// </summary>
public class WindowsVoiceFeedback : IVoiceFeedback, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;
    private WaveOut? _currentWaveOut;

    public string CurrentVoiceName { get; private set; } = string.Empty;

    public WindowsVoiceFeedback()
    {
        _synthesizer = new SpeechSynthesizer();
        try
        {
            _synthesizer.SetOutputToDefaultAudioDevice();
            CurrentVoiceName = _synthesizer.Voice.Name;
        }
        catch { }
    }

    public static List<string> GetInstalledVoices()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public void SetVoice(string? voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName)) return;

        try
        {
            _synthesizer.SelectVoice(voiceName);
            CurrentVoiceName = voiceName;
        }
        catch { }
    }

    public void SetRate(int rate)
    {
        try
        {
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
        }
        catch { }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, SpeakCompletedEventArgs e)
        {
            if (e.Cancelled)
                tcs.TrySetCanceled();
            else if (e.Error != null)
                tcs.TrySetException(e.Error);
            else
                tcs.TrySetResult();
        }

        _synthesizer.SpeakCompleted += Handler;

        try
        {
            using (cancellationToken.Register(() =>
            {
                try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
                tcs.TrySetCanceled(cancellationToken);
            }))
            {
                _synthesizer.SpeakAsync(text);
                await tcs.Task;
            }
        }
        catch when (tcs.Task.IsCanceled)
        {
            // cancellation handled
        }
        finally
        {
            _synthesizer.SpeakCompleted -= Handler;
        }
    }

    public async Task PlayAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            return;

        try
        {
            _currentWaveOut?.Stop();
            _currentWaveOut?.Dispose();
            _currentWaveOut = null;

            using var audioFile = new AudioFileReader(audioFilePath);
            _currentWaveOut = new WaveOut();
            _currentWaveOut.Init(audioFile);
            _currentWaveOut.Play();

            while (_currentWaveOut.PlaybackState == PlaybackState.Playing)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _currentWaveOut.Stop();
                    break;
                }
                await Task.Delay(50, cancellationToken);
            }
        }
        catch
        {
            // Игнорируем ошибки воспроизведения
        }
        finally
        {
            try
            {
                _currentWaveOut?.Stop();
                _currentWaveOut?.Dispose();
            }
            catch { }
            _currentWaveOut = null;
        }
    }

    public void StopSpeaking()
    {
        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _currentWaveOut?.Stop();
        }
        catch { }
    }

    public void Dispose()
    {
        StopSpeaking();
        _synthesizer.Dispose();
        _currentWaveOut?.Dispose();
    }
}
