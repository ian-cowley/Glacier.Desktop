namespace Glacier.Desktop.Windowing.Swapchain;

using System;
using Glacier.Desktop.Windowing.Platform.Mac;
using Glacier.Desktop.Windowing.Platform.Mobile;

/// <summary>
/// Factory for selecting and constructing presentation swapchains matching the target platform and window.
/// </summary>
public static class SwapchainFactory
{
    /// <summary>
    /// Creates a swapchain bound to the specified window.
    /// When <paramref name="backend"/> is <see cref="SwapchainBackend.Auto"/>, attempts the platform's primary GPU swapchain
    /// and cleanly falls back to <see cref="Software.SoftwareSwapchain"/> in headless or non-accelerated environments.
    /// </summary>
    public static ISwapchain Create(IWindow window, SwapchainDescription desc, SwapchainBackend backend = SwapchainBackend.Auto)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (backend == SwapchainBackend.Software)
        {
            return new Software.SoftwareSwapchain(desc, window);
        }

        switch (backend)
        {
            case SwapchainBackend.Direct3D12:
                return new D3D12.D3D12Swapchain(window.NativeHandle, desc);

            case SwapchainBackend.Vulkan:
                return new Vulkan.VulkanSwapchain(window.NativeHandle, desc);

            case SwapchainBackend.Metal:
                IntPtr metalTarget = window switch
                {
                    CocoaWindow cw when cw.MetalLayer != IntPtr.Zero => cw.MetalLayer,
                    IosUiWindow iw when iw.MetalLayer != IntPtr.Zero => iw.MetalLayer,
                    _ => window.NativeHandle
                };
                return new Metal.MetalSwapchain(metalTarget, desc);

            case SwapchainBackend.Auto:
            default:
                if (window is Platform.Headless.HeadlessWindow || window.NativeHandle == IntPtr.Zero)
                {
                    return new Software.SoftwareSwapchain(desc, window);
                }

                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        return new D3D12.D3D12Swapchain(window.NativeHandle, desc);
                    }
                    catch
                    {
                        return new Software.SoftwareSwapchain(desc, window);
                    }
                }

                if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
                {
                    try
                    {
                        IntPtr layer = window switch
                        {
                            CocoaWindow cw when cw.MetalLayer != IntPtr.Zero => cw.MetalLayer,
                            IosUiWindow iw when iw.MetalLayer != IntPtr.Zero => iw.MetalLayer,
                            _ => window.NativeHandle
                        };
                        if (layer != IntPtr.Zero)
                        {
                            return new Metal.MetalSwapchain(layer, desc);
                        }
                    }
                    catch
                    {
                    }
                    return new Software.SoftwareSwapchain(desc, window);
                }

                if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                {
                    try
                    {
                        return new Vulkan.VulkanSwapchain(window.NativeHandle, desc);
                    }
                    catch
                    {
                        return new Software.SoftwareSwapchain(desc, window);
                    }
                }

                return new Software.SoftwareSwapchain(desc, window);
        }
    }
}
