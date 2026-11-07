namespace Glacier.Desktop.Audio.Wasapi;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class WasapiInterop
{
    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const int AUDCLNT_SHAREMODE_EXCLUSIVE = 1;

    public const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const uint AUDCLNT_STREAMFLAGS_NOPERSIST = 0x00080000;
    public const uint AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    public const uint AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = 0x80000000;

    public const uint CLSCTX_ALL = 0x17;
    public const uint COINIT_MULTITHREADED = 0x0;

    public const int eRender = 0;
    public const int eConsole = 0;

    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    public static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    public static readonly Guid IID_IAudioClient3 = new("7ED4EE07-8E67-4CD4-8C1A-2B7A56608A8F");
    public static readonly Guid IID_IAudioRenderClient = new("F294AC80-3186-42DB-8E0B-E7EA720A4596");

    public const ushort WAVE_FORMAT_IEEE_FLOAT = 0x0003;
    public const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;
    public static readonly Guid KSDATAFORMAT_SUBTYPE_IEEE_FLOAT = new("00000003-0000-0010-8000-00AA00389B71");

    [StructLayout(LayoutKind.Sequential)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WAVEFORMATEXTENSIBLE
    {
        public WAVEFORMATEX Format;
        public ushort Samples;
        public uint dwChannelMask;
        public Guid SubFormat;
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    public static bool TryAcquireDefaultRenderEndpoint(out IntPtr mmDevice)
    {
        mmDevice = IntPtr.Zero;
        int hrCoInit = CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
        bool mustUninit = hrCoInit is 0 or 1; // S_OK or S_FALSE

        try
        {
            int hr = CoCreateInstance(
                in CLSID_MMDeviceEnumerator,
                IntPtr.Zero,
                CLSCTX_ALL,
                in IID_IMMDeviceEnumerator,
                out IntPtr enumerator);

            if (hr < 0 || enumerator == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // IMMDeviceEnumerator::GetDefaultAudioEndpoint is vtable slot 4
                void** vtbl = *(void***)enumerator;
                var getDefaultEndpoint = (delegate* unmanaged[Stdcall]<IntPtr, int, int, out IntPtr, int>)vtbl[4];
                int hrEndpoint = getDefaultEndpoint(enumerator, eRender, eConsole, out mmDevice);
                return hrEndpoint >= 0 && mmDevice != IntPtr.Zero;
            }
            finally
            {
                Marshal.Release(enumerator);
            }
        }
        finally
        {
            if (mustUninit && mmDevice == IntPtr.Zero)
            {
                CoUninitialize();
            }
        }
    }
}

internal static unsafe partial class AvrtInterop
{
    private const string AvrtDll = "avrt.dll";

    [DllImport(AvrtDll, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr AvSetMmThreadCharacteristicsW(string TaskName, ref uint TaskIndex);

    [DllImport(AvrtDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AvRevertMmThreadCharacteristics(IntPtr AvrtHandle);
}
