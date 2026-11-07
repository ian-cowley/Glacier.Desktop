namespace Glacier.Desktop.Audio;

using System;

/// <summary>
/// Defines the contract for an asynchronous streaming audio channel backed by a lock-free ring buffer.
/// </summary>
public interface IAudioStream : IDisposable
{
    /// <summary>
    /// Gets the stream sample rate in Hz.
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Gets the channel count.
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// Gets the ring buffer capacity in total float samples.
    /// </summary>
    int BufferCapacity { get; }

    /// <summary>
    /// Gets the number of samples that can be written without blocking or overflowing.
    /// </summary>
    int AvailableWrite { get; }

    /// <summary>
    /// Writes interleaved float samples into the streaming buffer.
    /// </summary>
    /// <param name="samples">Interleaved PCM float samples.</param>
    /// <returns>True if all samples were successfully written; false if buffer overflow occurred.</returns>
    bool Write(ReadOnlySpan<float> samples);

    /// <summary>
    /// Starts stream playback and activates hardware render pump.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops stream playback.
    /// </summary>
    void Stop();
}
