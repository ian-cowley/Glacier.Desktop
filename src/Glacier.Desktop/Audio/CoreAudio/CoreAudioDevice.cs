namespace Glacier.Desktop.Audio.CoreAudio;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Apple macOS and iOS CoreAudio output device driving an <c>AudioUnit</c> via a lock-free SPSC ring buffer
/// and a Native AOT <c>[UnmanagedCallersOnly]</c> render callback.
/// </summary>
public sealed unsafe class CoreAudioDevice : IAudioDevice
{
    private const string CoreAudioDll = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    private const uint kAudioUnitProperty_StreamFormat = 8;
    private const uint kAudioUnitProperty_SetRenderCallback = 23;
    private const uint kAudioUnitScope_Input = 1;
    private const uint kAudioFormatLinearPCM = 0x6C70636D; // 'lpcm'
    private const uint kAudioFormatFlagIsFloat = 1u << 0;
    private const uint kAudioFormatFlagIsPacked = 1u << 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioComponentDescription
    {
        public uint componentType;
        public uint componentSubType;
        public uint componentManufacturer;
        public uint componentFlags;
        public uint componentFlagsMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint mFormatID;
        public uint mFormatFlags;
        public uint mBytesPerPacket;
        public uint mFramesPerPacket;
        public uint mBytesPerFrame;
        public uint mChannelsPerFrame;
        public uint mBitsPerChannel;
        public uint mReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AURenderCallbackStruct
    {
        public delegate* unmanaged[Cdecl]<IntPtr, uint*, void*, uint, uint, AudioBufferList*, int> inputProc;
        public IntPtr inputProcRefCon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioBuffer
    {
        public uint mNumberChannels;
        public uint mDataByteSize;
        public void* mData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioBufferList
    {
        public uint mNumberBuffers;
        public AudioBuffer mBuffers;
    }

    [DllImport(CoreAudioDll)]
    private static extern IntPtr AudioComponentFindNext(IntPtr inComponent, ref AudioComponentDescription inDesc);

    [DllImport(CoreAudioDll)]
    private static extern int AudioComponentInstanceNew(IntPtr inComponent, out IntPtr outComponentInstance);

    [DllImport(CoreAudioDll)]
    private static extern int AudioComponentInstanceDispose(IntPtr inComponentInstance);

    [DllImport(CoreAudioDll)]
    private static extern int AudioUnitSetProperty(
        IntPtr inUnit,
        uint inID,
        uint inScope,
        uint inElement,
        void* inData,
        uint inDataSize);

    [DllImport(CoreAudioDll)]
    private static extern int AudioUnitInitialize(IntPtr inUnit);

    [DllImport(CoreAudioDll)]
    private static extern int AudioUnitUninitialize(IntPtr inUnit);

    [DllImport(CoreAudioDll)]
    private static extern int AudioOutputUnitStart(IntPtr ci);

    [DllImport(CoreAudioDll)]
    private static extern int AudioOutputUnitStop(IntPtr ci);

    private IntPtr _audioUnit;
    private GCHandle _selfHandle;
    private readonly SpscAudioRingBuffer _ringBuffer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float _volume = 1.0f;
    private bool _disposed;

    public static bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();

    public int SampleRate => _sampleRate;
    public int Channels => _channels;

    public CoreAudioDevice(int sampleRate = 48000, int channels = 2, int bufferCapacity = 32768)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("CoreAudioDevice is only supported on macOS and iOS.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        _sampleRate = sampleRate;
        _channels = channels;
        _ringBuffer = new SpscAudioRingBuffer(bufferCapacity);

        AudioComponentDescription desc = new()
        {
            componentType = 0x61756F75, // 'auou' kAudioUnitType_Output
            componentSubType = OperatingSystem.IsIOS() ? 0x72696F63u : 0x64656620u, // 'rioc' RemoteIO or 'def ' DefaultOutput
            componentManufacturer = 0x6170706C // 'appl'
        };

        IntPtr comp = AudioComponentFindNext(IntPtr.Zero, ref desc);
        if (comp == IntPtr.Zero || AudioComponentInstanceNew(comp, out _audioUnit) != 0 || _audioUnit == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to instantiate CoreAudio output AudioUnit.");
        }

        _selfHandle = GCHandle.Alloc(this);
        try
        {
            uint bytesPerFrame = (uint)(channels * sizeof(float));
            AudioStreamBasicDescription asbd = new()
            {
                mSampleRate = sampleRate,
                mFormatID = kAudioFormatLinearPCM,
                mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked,
                mBytesPerPacket = bytesPerFrame,
                mFramesPerPacket = 1,
                mBytesPerFrame = bytesPerFrame,
                mChannelsPerFrame = (uint)channels,
                mBitsPerChannel = 32
            };

            AURenderCallbackStruct cb = new()
            {
                inputProc = &RenderCallback,
                inputProcRefCon = GCHandle.ToIntPtr(_selfHandle)
            };

            if (AudioUnitSetProperty(_audioUnit, kAudioUnitProperty_StreamFormat, kAudioUnitScope_Input, 0, &asbd, (uint)sizeof(AudioStreamBasicDescription)) != 0 ||
                AudioUnitSetProperty(_audioUnit, kAudioUnitProperty_SetRenderCallback, kAudioUnitScope_Input, 0, &cb, (uint)sizeof(AURenderCallbackStruct)) != 0 ||
                AudioUnitInitialize(_audioUnit) != 0 ||
                AudioOutputUnitStart(_audioUnit) != 0)
            {
                throw new InvalidOperationException("Failed to initialize or start CoreAudio output unit.");
            }
        }
        catch
        {
            if (_selfHandle.IsAllocated) _selfHandle.Free();
            AudioComponentInstanceDispose(_audioUnit);
            _audioUnit = IntPtr.Zero;
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RenderCallback(IntPtr inRefCon, uint* ioActionFlags, void* inTimeStamp, uint inBusNumber, uint inNumberFrames, AudioBufferList* ioData)
    {
        if (ioData == null || ioData->mNumberBuffers == 0 || ioData->mBuffers.mData == null)
        {
            return 0;
        }

        GCHandle handle = GCHandle.FromIntPtr(inRefCon);
        if (handle.Target is not CoreAudioDevice self)
        {
            return 0;
        }

        int totalSamples = (int)inNumberFrames * self._channels;
        int maxFloats = (int)(ioData->mBuffers.mDataByteSize / sizeof(float));
        int count = Math.Min(totalSamples, maxFloats);
        Span<float> outSpan = new(ioData->mBuffers.mData, count);

        int read = self._ringBuffer.Read(outSpan);
        float vol = self._volume;
        for (int i = 0; i < read; i++)
        {
            outSpan[i] *= vol;
        }
        if (read < count)
        {
            outSpan[read..].Clear();
        }

        return 0;
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ringBuffer.Write(pcmSamples);
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

        if (_audioUnit != IntPtr.Zero)
        {
            try
            {
                AudioOutputUnitStop(_audioUnit);
                AudioUnitUninitialize(_audioUnit);
                AudioComponentInstanceDispose(_audioUnit);
            }
            catch
            {
            }
            _audioUnit = IntPtr.Zero;
        }

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _ringBuffer.Clear();
        GC.SuppressFinalize(this);
    }
}
