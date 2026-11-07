namespace Glacier.Desktop.Windowing.Swapchain.D3D12;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class D3D12Interop
{
    private const string D3D12Dll = "d3d12.dll";

    public const uint D3D_FEATURE_LEVEL_11_0 = 0xb000;
    public const uint D3D_FEATURE_LEVEL_12_0 = 0xc000;

    [DllImport(D3D12Dll, ExactSpelling = true)]
    public static extern int D3D12CreateDevice(
        IntPtr pAdapter,
        uint MinimumFeatureLevel,
        in Guid riid,
        out IntPtr ppDevice);
}
