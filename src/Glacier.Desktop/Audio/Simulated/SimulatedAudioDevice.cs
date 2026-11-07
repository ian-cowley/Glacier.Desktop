namespace Glacier.Desktop.Audio.Simulated;

using System;

/// <summary>
/// High-fidelity simulated audio sink used for headless testing, audio pipeline verification, and microbenchmarks.
/// </summary>
public sealed class SimulatedAudioDevice : IAudioDevice, IAudioStream
{
    private readonly SpscAudioRingBuffer _ringBuffer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float _volume = 1.0f;
    private long _totalSamplesPlayed;
    private long _totalSamplesWritten;
    private int _underrunCount;
    private bool _isRunning;
    private bool _disposed;

    public int SampleRate => _sampleRate;
    public int Channels => _channels;
    public int BufferCapacity => _ringBuffer.Capacity;
    public int AvailableWrite => _ringBuffer.AvailableWrite;
    public int AvailableRead => _ringBuffer.AvailableRead;
    public float MasterVolume => _volume;
    public long TotalSamplesPlayed => _totalSamplesPlayed;
    public long TotalSamplesWritten => _totalSamplesWritten;
    public int UnderrunCount => _underrunCount;
    public bool IsRunning => _isRunning;

    public SimulatedAudioDevice(int sampleRate = 48000, int channels = 2, int bufferCapacity = 16384)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _ringBuffer = new SpscAudioRingBuffer(bufferCapacity);
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Write(pcmSamples);
    }

    public bool Write(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool success = _ringBuffer.TryWrite(samples);
        if (success)
        {
            _totalSamplesWritten += samples.Length;
        }
        else
        {
            _underrunCount++;
        }
        return success;
    }

    /// <summary>
    /// Simulates hardware consumption of the specified number of samples.
    /// </summary>
    /// <param name="sampleCount">Maximum samples to consume.</param>
    /// <returns>Actual number of samples consumed.</returns>
    public int Drain(int sampleCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Span<float> drainBuffer = stackalloc float[Math.Min(sampleCount, 1024)];
        int totalRead = 0;

        while (totalRead < sampleCount)
        {
            int batch = Math.Min(sampleCount - totalRead, drainBuffer.Length);
            int read = _ringBuffer.Read(drainBuffer[..batch]);
            if (read == 0) break;
            totalRead += read;
        }

        _totalSamplesPlayed += totalRead;
        return totalRead;
    }

    public void SetMasterVolume(float volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isRunning = true;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _ringBuffer.Clear();
        GC.SuppressFinalize(this);
    }
}
