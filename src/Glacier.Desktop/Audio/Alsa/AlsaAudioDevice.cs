namespace Glacier.Desktop.Audio.Alsa;

using System;
using System.Buffers;
using System.Runtime.InteropServices;

internal static unsafe partial class LibAsound
{
    private const string AsoundDll = "libasound.so.2";

    public const int SND_PCM_STREAM_PLAYBACK = 0;
    public const int SND_PCM_NONBLOCK = 0x00000001;
    public const int SND_PCM_FORMAT_FLOAT_LE = 14;
    public const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_close(IntPtr pcm);

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_set_params(
        IntPtr pcm,
        int format,
        int access,
        uint channels,
        uint rate,
        int soft_resample,
        uint latency);

    [DllImport(AsoundDll)]
    public static extern IntPtr snd_pcm_writei(IntPtr pcm, void* buffer, UIntPtr size);
}

/// <summary>
/// Linux ALSA PCM audio output device interfacing directly with <c>libasound.so.2</c>.
/// </summary>
public sealed unsafe class AlsaAudioDevice : IAudioDevice
{
    private IntPtr _pcmHandle;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float _volume = 1.0f;
    private bool _disposed;

    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsAndroid();

    public int SampleRate => _sampleRate;
    public int Channels => _channels;

    public AlsaAudioDevice(int sampleRate = 48000, int channels = 2)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("AlsaAudioDevice is only supported on Linux and Android.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        _sampleRate = sampleRate;
        _channels = channels;

        int err;
        try
        {
            err = LibAsound.snd_pcm_open(out _pcmHandle, "default", LibAsound.SND_PCM_STREAM_PLAYBACK, LibAsound.SND_PCM_NONBLOCK);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new PlatformNotSupportedException("libasound.so.2 is not installed on this Linux host.", ex);
        }

        if (err < 0 || _pcmHandle == IntPtr.Zero)
        {
            _pcmHandle = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to open ALSA default playback PCM device (error {err}).");
        }

        int paramErr = LibAsound.snd_pcm_set_params(
            _pcmHandle,
            LibAsound.SND_PCM_FORMAT_FLOAT_LE,
            LibAsound.SND_PCM_ACCESS_RW_INTERLEAVED,
            (uint)channels,
            (uint)sampleRate,
            1,
            20_000);

        if (paramErr < 0)
        {
            LibAsound.snd_pcm_close(_pcmHandle);
            _pcmHandle = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to configure ALSA PCM parameters (error {paramErr}).");
        }
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pcmHandle == IntPtr.Zero || pcmSamples.IsEmpty)
        {
            return;
        }

        int frames = pcmSamples.Length / _channels;
        if (frames <= 0)
        {
            return;
        }

        int count = frames * _channels;
        float vol = _volume;

        if (vol >= 0.9999f)
        {
            fixed (float* ptr = pcmSamples)
            {
                long written = LibAsound.snd_pcm_writei(_pcmHandle, ptr, (UIntPtr)frames).ToInt64();
                if (written < 0)
                {
                    LibAsound.snd_pcm_recover(_pcmHandle, (int)written, 1);
                }
            }
            return;
        }

        float[]? rented = null;
        Span<float> scaled = count <= 1024
            ? stackalloc float[count]
            : (rented = ArrayPool<float>.Shared.Rent(count)).AsSpan(0, count);

        try
        {
            for (int i = 0; i < count; i++)
            {
                scaled[i] = pcmSamples[i] * vol;
            }

            fixed (float* ptr = scaled)
            {
                long written = LibAsound.snd_pcm_writei(_pcmHandle, ptr, (UIntPtr)frames).ToInt64();
                if (written < 0)
                {
                    LibAsound.snd_pcm_recover(_pcmHandle, (int)written, 1);
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<float>.Shared.Return(rented);
            }
        }
    }

    public void SetMasterVolume(float volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pcmHandle != IntPtr.Zero)
        {
            try { LibAsound.snd_pcm_close(_pcmHandle); } catch { }
            _pcmHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
