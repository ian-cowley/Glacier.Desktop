namespace Glacier.Desktop.Audio.Wasapi;

using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Low-latency Windows WASAPI shared-mode audio output stream backed by a lock-free SPSC ring buffer
/// and genuine COM <c>IMMDevice</c> / <c>IAudioClient</c> / <c>IAudioRenderClient</c> rendering.
/// </summary>
public sealed unsafe class WasapiAudioStream : IAudioStream
{
    private readonly SpscAudioRingBuffer _ringBuffer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private Thread? _workerThread;
    private readonly AutoResetEvent _eventHandle = new(false);
    private volatile bool _isRunning;
    private bool _disposed;
    private float _volume = 1.0f;

    public static bool IsSupported
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
            if (!WasapiInterop.TryAcquireDefaultRenderEndpoint(out IntPtr endpoint))
            {
                return false;
            }
            Marshal.Release(endpoint);
            WasapiInterop.CoUninitialize();
            return true;
        }
    }

    public int SampleRate => _sampleRate;
    public int Channels => _channels;
    public int BufferCapacity => _ringBuffer.Capacity;
    public int AvailableWrite => _ringBuffer.AvailableWrite;
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public WasapiAudioStream(int sampleRate = 48000, int channels = 2, int bufferCapacity = 16384)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WasapiAudioStream is only supported on Windows.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        if (!WasapiInterop.TryAcquireDefaultRenderEndpoint(out IntPtr probeEndpoint))
        {
            throw new InvalidOperationException("No active WASAPI audio render endpoint is available on this Windows host.");
        }
        Marshal.Release(probeEndpoint);
        WasapiInterop.CoUninitialize();

        _sampleRate = sampleRate;
        _channels = channels;
        _ringBuffer = new SpscAudioRingBuffer(bufferCapacity);
    }

    public bool Write(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool success = _ringBuffer.TryWrite(samples);
        if (success)
        {
            _eventHandle.Set();
        }
        return success;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isRunning) return;

        _isRunning = true;
        if (_workerThread == null || !_workerThread.IsAlive)
        {
            _workerThread = new Thread(RenderLoop)
            {
                Name = "Glacier.WASAPI.AudioPump",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _workerThread.Start();
        }
    }

    public void Stop()
    {
        _isRunning = false;
        if (!_disposed)
        {
            try { _eventHandle.Set(); } catch (ObjectDisposedException) { }
        }
        Thread? worker = _workerThread;
        if (worker is { IsAlive: true } && Thread.CurrentThread != worker)
        {
            worker.Join(millisecondsTimeout: 250);
        }
    }

    private void RenderLoop()
    {
        int hrCoInit = WasapiInterop.CoInitializeEx(IntPtr.Zero, WasapiInterop.COINIT_MULTITHREADED);
        bool mustUninit = hrCoInit is 0 or 1;

        IntPtr avrtHandle = IntPtr.Zero;
        uint taskIndex = 0;
        IntPtr mmDevice = IntPtr.Zero;
        IntPtr audioClient = IntPtr.Zero;
        IntPtr renderClient = IntPtr.Zero;
        bool clientStarted = false;

        try
        {
            try
            {
                avrtHandle = AvrtInterop.AvSetMmThreadCharacteristicsW("Pro Audio", ref taskIndex);
            }
            catch
            {
                // Avrt optional
            }

            int hrEnum = WasapiInterop.CoCreateInstance(
                in WasapiInterop.CLSID_MMDeviceEnumerator,
                IntPtr.Zero,
                WasapiInterop.CLSCTX_ALL,
                in WasapiInterop.IID_IMMDeviceEnumerator,
                out IntPtr enumerator);

            if (hrEnum < 0 || enumerator == IntPtr.Zero)
            {
                return;
            }

            try
            {
                void** enumVtbl = *(void***)enumerator;
                var getDefaultEndpoint = (delegate* unmanaged[Stdcall]<IntPtr, int, int, out IntPtr, int>)enumVtbl[4];
                if (getDefaultEndpoint(enumerator, WasapiInterop.eRender, WasapiInterop.eConsole, out mmDevice) < 0 || mmDevice == IntPtr.Zero)
                {
                    return;
                }
            }
            finally
            {
                Marshal.Release(enumerator);
            }

            // IMMDevice::Activate is vtable slot 3
            void** devVtbl = *(void***)mmDevice;
            var activate = (delegate* unmanaged[Stdcall]<IntPtr, in Guid, uint, IntPtr, out IntPtr, int>)devVtbl[3];
            if (activate(mmDevice, in WasapiInterop.IID_IAudioClient, WasapiInterop.CLSCTX_ALL, IntPtr.Zero, out audioClient) < 0 || audioClient == IntPtr.Zero)
            {
                return;
            }

            ushort blockAlign = (ushort)(_channels * sizeof(float));
            WasapiInterop.WAVEFORMATEX wfx = new()
            {
                wFormatTag = WasapiInterop.WAVE_FORMAT_IEEE_FLOAT,
                nChannels = (ushort)_channels,
                nSamplesPerSec = (uint)_sampleRate,
                wBitsPerSample = 32,
                nBlockAlign = blockAlign,
                nAvgBytesPerSec = (uint)_sampleRate * blockAlign,
                cbSize = 0
            };

            void** acVtbl = *(void***)audioClient;
            var initialize = (delegate* unmanaged[Stdcall]<IntPtr, int, uint, long, long, WasapiInterop.WAVEFORMATEX*, IntPtr, int>)acVtbl[3];
            var getBufferSize = (delegate* unmanaged[Stdcall]<IntPtr, out uint, int>)acVtbl[4];
            var getCurrentPadding = (delegate* unmanaged[Stdcall]<IntPtr, out uint, int>)acVtbl[6];
            var startClient = (delegate* unmanaged[Stdcall]<IntPtr, int>)acVtbl[10];
            var stopClient = (delegate* unmanaged[Stdcall]<IntPtr, int>)acVtbl[11];
            var getService = (delegate* unmanaged[Stdcall]<IntPtr, in Guid, out IntPtr, int>)acVtbl[14];

            uint flags = WasapiInterop.AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM |
                         WasapiInterop.AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY |
                         WasapiInterop.AUDCLNT_STREAMFLAGS_NOPERSIST;

            // 20ms shared-mode engine buffer (200,000 100-ns units)
            if (initialize(audioClient, WasapiInterop.AUDCLNT_SHAREMODE_SHARED, flags, 200_000, 0, &wfx, IntPtr.Zero) < 0)
            {
                return;
            }

            if (getBufferSize(audioClient, out uint bufferFrameCount) < 0 || bufferFrameCount == 0)
            {
                return;
            }

            if (getService(audioClient, in WasapiInterop.IID_IAudioRenderClient, out renderClient) < 0 || renderClient == IntPtr.Zero)
            {
                return;
            }

            void** rcVtbl = *(void***)renderClient;
            var getBuffer = (delegate* unmanaged[Stdcall]<IntPtr, uint, out byte*, int>)rcVtbl[3];
            var releaseBuffer = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)rcVtbl[4];

            if (startClient(audioClient) < 0)
            {
                return;
            }
            clientStarted = true;

            Span<float> staging = stackalloc float[1024];

            while (_isRunning)
            {
                try
                {
                    _eventHandle.WaitOne(3);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (!_isRunning) break;

                if (getCurrentPadding(audioClient, out uint numFramesPadding) < 0)
                {
                    break;
                }

                uint framesAvailable = bufferFrameCount > numFramesPadding ? bufferFrameCount - numFramesPadding : 0;
                if (framesAvailable == 0)
                {
                    continue;
                }

                int maxFramesPerIter = staging.Length / _channels;
                int framesToPull = (int)Math.Min(framesAvailable, (uint)maxFramesPerIter);
                int samplesWanted = framesToPull * _channels;

                int samplesRead = _ringBuffer.Read(staging[..samplesWanted]);
                int framesRead = samplesRead / _channels;
                if (framesRead <= 0)
                {
                    continue;
                }

                if (getBuffer(renderClient, (uint)framesRead, out byte* pData) >= 0 && pData != null)
                {
                    float vol = _volume;
                    float* dst = (float*)pData;
                    int sampleCount = framesRead * _channels;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        dst[i] = staging[i] * vol;
                    }
                    releaseBuffer(renderClient, (uint)framesRead, 0);
                }
            }

            if (clientStarted)
            {
                stopClient(audioClient);
                clientStarted = false;
            }
        }
        finally
        {
            if (renderClient != IntPtr.Zero) Marshal.Release(renderClient);
            if (audioClient != IntPtr.Zero) Marshal.Release(audioClient);
            if (mmDevice != IntPtr.Zero) Marshal.Release(mmDevice);

            if (avrtHandle != IntPtr.Zero)
            {
                try { AvrtInterop.AvRevertMmThreadCharacteristics(avrtHandle); } catch { }
            }

            if (mustUninit)
            {
                WasapiInterop.CoUninitialize();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;

        _eventHandle.Dispose();
        _ringBuffer.Clear();

        GC.SuppressFinalize(this);
    }
}
