namespace Glacier.Desktop.Audio.Wasapi;

using System;

/// <summary>
/// Low-latency Windows WASAPI audio device implementing IAudioDevice.
/// </summary>
public sealed class WasapiAudioDevice : IAudioDevice
{
    private readonly WasapiAudioStream _stream;
    private bool _disposed;

    public int SampleRate => _stream.SampleRate;
    public int Channels => _stream.Channels;

    public WasapiAudioDevice(int sampleRate = 48000, int channels = 2)
    {
        _stream = new WasapiAudioStream(sampleRate, channels);
        _stream.Start();
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stream.Write(pcmSamples);
    }

    public void SetMasterVolume(float volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stream.Volume = volume;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
