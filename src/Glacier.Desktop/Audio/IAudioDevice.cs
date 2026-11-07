namespace Glacier.Desktop.Audio;

using System;

/// <summary>
/// Defines the contract for an ultra-low-latency audio output device.
/// </summary>
public interface IAudioDevice : IDisposable
{
    /// <summary>
    /// Gets the native hardware sampling rate in Hz (e.g. 48000, 96000, 192000).
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio output channels (e.g. 1 for mono, 2 for stereo, 6 for 5.1 surround).
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// Plays interleaved floating-point PCM samples through the audio hardware.
    /// </summary>
    /// <param name="pcmSamples">Interleaved 32-bit float samples normalized between -1.0f and +1.0f.</param>
    void Play(ReadOnlySpan<float> pcmSamples);

    /// <summary>
    /// Sets the master volume level.
    /// </summary>
    /// <param name="volume">Volume multiplier from 0.0f (mute) to 1.0f (unity gain).</param>
    void SetMasterVolume(float volume);
}
