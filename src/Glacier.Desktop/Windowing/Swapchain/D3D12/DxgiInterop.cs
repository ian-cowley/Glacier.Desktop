namespace Glacier.Desktop.Windowing.Swapchain.D3D12;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class DxgiInterop
{
    private const string DxgiDll = "dxgi.dll";

    public const uint DXGI_CREATE_FACTORY_DEBUG = 0x01;

    public const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x00000020;

    public const uint DXGI_SWAP_EFFECT_DISCARD = 0;
    public const uint DXGI_SWAP_EFFECT_SEQUENTIAL = 1;
    public const uint DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL = 3;
    public const uint DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;

    public const uint DXGI_SWAP_CHAIN_FLAG_NONPREROTATED = 1;
    public const uint DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH = 2;
    public const uint DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT = 64;
    public const uint DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING = 2048;

    public const uint DXGI_PRESENT_ALLOW_TEARING = 0x00000200;
    public const uint DXGI_PRESENT_DO_NOT_WAIT = 0x00000008;

    public const uint DXGI_FEATURE_PRESENT_ALLOW_TEARING = 0;

    public const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
    public const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    public const int DXGI_FORMAT_R10G10B10A2_UNORM = 24;
    public const int DXGI_FORMAT_R16G16B16A16_FLOAT = 10;

    public static readonly Guid IID_IDXGIFactory4 = new("1bc6ea02-ef36-464f-bf0c-21ca39e5168a");
    public static readonly Guid IID_IDXGIFactory5 = new("7632e1f5-ee65-4dca-87fd-84cd75f8838d");
    public static readonly Guid IID_IDXGISwapChain3 = new("94d99bdb-f1f8-4ab0-b236-7da0170edab1");
    public static readonly Guid IID_IDXGISwapChain4 = new("3d585d5a-bd4a-489e-b1f4-3dbe1ecb6452");
    public static readonly Guid IID_ID3D12Device = new("189819f1-1db6-4b57-be54-1821339b85f7");
    public static readonly Guid IID_ID3D12Resource = new("696442be-a72e-4059-bc79-5b5cc766dce0");

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width;
        public uint Height;
        public int Format;
        public int Stereo;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public uint Scaling;
        public uint SwapEffect;
        public uint AlphaMode;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_FULLSCREEN_DESC
    {
        public DXGI_RATIONAL RefreshRate;
        public uint ScanlineOrdering;
        public uint Scaling;
        public int Windowed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_PRESENT_PARAMETERS
    {
        public uint DirtyRectsCount;
        public IntPtr pDirtyRects;
        public IntPtr pScrollRect;
        public IntPtr pScrollOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_HDR_METADATA_HDR10
    {
        public ushort RedPrimary0;
        public ushort RedPrimary1;
        public ushort GreenPrimary0;
        public ushort GreenPrimary1;
        public ushort BluePrimary0;
        public ushort BluePrimary1;
        public ushort WhitePoint0;
        public ushort WhitePoint1;
        public uint MaxMasteringLuminance;
        public uint MinMasteringLuminance;
        public ushort MaxContentLightLevel;
        public ushort MaxFrameAverageLightLevel;
    }

    [DllImport(DxgiDll, ExactSpelling = true)]
    public static extern int CreateDXGIFactory2(uint Flags, in Guid riid, out IntPtr ppFactory);
}
