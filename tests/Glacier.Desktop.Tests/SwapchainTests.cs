namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Windowing;
using Glacier.Desktop.Windowing.Platform;
using Glacier.Desktop.Windowing.Platform.Headless;
using Glacier.Desktop.Windowing.Platform.Win32;
using Glacier.Desktop.Windowing.Swapchain;
using Glacier.Desktop.Windowing.Swapchain.D3D12;
using Glacier.Desktop.Windowing.Swapchain.Metal;
using Glacier.Desktop.Windowing.Swapchain.Software;
using Glacier.Desktop.Windowing.Swapchain.Vulkan;
using Xunit;

public class SwapchainTests
{
    [Fact]
    public void SwapchainDescription_DefaultsAndRecords_Work()
    {
        var desc = new SwapchainDescription(1920, 1080);
        Assert.Equal(1920, desc.Width);
        Assert.Equal(1080, desc.Height);
        Assert.Equal(2, desc.BufferCount);
        Assert.False(desc.EnableHdr);
        Assert.True(desc.LowLatencyWaitable);

        var custom = new SwapchainDescription(3840, 2160, BufferCount: 3, EnableHdr: true, LowLatencyWaitable: false);
        Assert.Equal(3840, custom.Width);
        Assert.Equal(2160, custom.Height);
        Assert.Equal(3, custom.BufferCount);
        Assert.True(custom.EnableHdr);
        Assert.False(custom.LowLatencyWaitable);
    }

    [Fact]
    public unsafe void D3D12Swapchain_PlatformGatingAndRealBuffers_WorkCorrectly()
    {
        var desc = new SwapchainDescription(320, 240, BufferCount: 3);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new D3D12Swapchain(new IntPtr(1), desc));
            return;
        }

        // Invalid HWND must be rejected instead of faking a swapchain
        Assert.Throws<ArgumentException>(() => new D3D12Swapchain(new IntPtr(0x1000), desc));

        using var window = WindowFactory.CreateWindow(new WindowOptions { Width = 320, Height = 240, IsVisible = false });
        if (window is not Glacier.Desktop.Windowing.Platform.Win32.Win32Window)
        {
            return;
        }

        using var swapchain = new D3D12Swapchain(window.NativeHandle, desc);
        Assert.Equal(320, swapchain.Width);
        Assert.Equal(240, swapchain.Height);
        Assert.Equal(3, swapchain.BufferCount);
        Assert.Equal(0UL, swapchain.PresentCount);
        Assert.NotEqual(IntPtr.Zero, swapchain.WaitableObject);

        IntPtr b0 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(IntPtr.Zero, b0);

        // Verify CurrentBackBuffer is genuine writable native memory (not a fake magic hex handle)
        Span<byte> pixels = new((void*)b0, (int)swapchain.BufferByteSize);
        pixels[0] = 0xAA;
        pixels[^1] = 0x55;
        Assert.Equal(0xAA, pixels[0]);
        Assert.Equal(0x55, pixels[^1]);

        swapchain.Present(vsync: false);
        Assert.Equal(1UL, swapchain.PresentCount);
        IntPtr b1 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(b0, b1);

        swapchain.Present(vsync: false);
        Assert.Equal(2UL, swapchain.PresentCount);
        IntPtr b2 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(b1, b2);

        swapchain.Present(vsync: false);
        Assert.Equal(3UL, swapchain.PresentCount);
        Assert.Equal(b0, swapchain.CurrentBackBuffer);

        swapchain.Resize(640, 480);
        Assert.Equal(640, swapchain.Width);
        Assert.Equal(480, swapchain.Height);
        Assert.Equal((nuint)(640 * 480 * 4), swapchain.BufferByteSize);
    }

    [Fact]
    public unsafe void VulkanSwapchain_LoaderProbeAndRealStagingImages_FunctionCorrectly()
    {
        var desc = new SwapchainDescription(640, 360, BufferCount: 2);

        if (!VulkanSwapchain.IsSupported)
        {
            Assert.Throws<PlatformNotSupportedException>(() => new VulkanSwapchain(new IntPtr(1), desc));
            return;
        }

        VulkanSwapchain vk;
        try
        {
            vk = new VulkanSwapchain(new IntPtr(1), desc);
        }
        catch (InvalidOperationException)
        {
            // Headless VM with Vulkan loader installed but zero ICD drivers (VK_ERROR_INCOMPATIBLE_DRIVER)
            return;
        }

        using (vk)
        {
            Assert.Equal(640, vk.Width);
            Assert.Equal(360, vk.Height);
            Assert.Equal(2, vk.BufferCount);
            Assert.NotEqual(IntPtr.Zero, vk.InstanceHandle);
            Assert.Equal(IntPtr.Zero, vk.CurrentSemaphore);

            IntPtr img0 = vk.CurrentBackBuffer;
            Assert.NotEqual(IntPtr.Zero, img0);

            // Verify CurrentBackBuffer is genuine writable native memory
            Span<byte> span = new((void*)img0, (int)vk.BufferByteSize);
            span[0] = 0x12;
            span[^1] = 0x34;
            Assert.Equal(0x12, span[0]);
            Assert.Equal(0x34, span[^1]);

            vk.Present(vsync: false);
            Assert.Equal(VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR, vk.PresentMode);

            IntPtr img1 = vk.CurrentBackBuffer;
            Assert.NotEqual(img0, img1);

            vk.Present(vsync: true);
            Assert.Equal(VulkanInterop.VK_PRESENT_MODE_FIFO_KHR, vk.PresentMode);
            Assert.Equal(img0, vk.CurrentBackBuffer);
        }
    }

    [Fact]
    public void MetalSwapchain_EnforcesApplePlatform()
    {
        var desc = new SwapchainDescription(800, 600, BufferCount: 2, EnableHdr: false);
        Assert.Equal(80, MetalSwapchain.MTLPixelFormatBGRA8Unorm);
        Assert.Equal(115, MetalSwapchain.MTLPixelFormatRGBA16Float);

        if (!MetalSwapchain.IsSupported)
        {
            Assert.Throws<PlatformNotSupportedException>(() => new MetalSwapchain(new IntPtr(1), desc));
        }
    }

    [Fact]
    public unsafe void SoftwareSwapchain_AllocatesAlignedWritableBackbuffersAndResizes()
    {
        var desc = new SwapchainDescription(100, 100, BufferCount: 2);
        using var sw = new SoftwareSwapchain(desc);

        Assert.Equal(100, sw.Width);
        Assert.Equal(100, sw.Height);
        Assert.Equal((nuint)(100 * 100 * 4), sw.BufferByteSize);

        IntPtr buf0 = sw.CurrentBackBuffer;
        Assert.NotEqual(IntPtr.Zero, buf0);
        Assert.Equal(0UL, (ulong)buf0 % 64UL);

        Span<byte> pixels = new((void*)buf0, (int)sw.BufferByteSize);
        pixels.Fill(0x7F);
        Assert.Equal(0x7F, pixels[500]);

        sw.Present();
        IntPtr buf1 = sw.CurrentBackBuffer;
        Assert.NotEqual(buf0, buf1);

        sw.Resize(200, 150);
        Assert.Equal(200, sw.Width);
        Assert.Equal(150, sw.Height);
        Assert.Equal((nuint)(200 * 150 * 4), sw.BufferByteSize);
    }

    [Fact]
    public void SwapchainFactory_WithHeadlessWindow_ReturnsSoftwareSwapchain()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        using var sc = SwapchainFactory.Create(window, new SwapchainDescription(640, 480));

        Assert.IsType<SoftwareSwapchain>(sc);
        Assert.Equal(640, sc.Width);
        Assert.Equal(480, sc.Height);
    }
}
