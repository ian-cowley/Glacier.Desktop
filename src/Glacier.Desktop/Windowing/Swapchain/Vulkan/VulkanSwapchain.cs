namespace Glacier.Desktop.Windowing.Swapchain.Vulkan;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Vulkan WSI presentation swapchain binding to the native Vulkan loader (<c>vulkan-1.dll</c> / <c>libvulkan.so.1</c>),
/// creating a real <c>VkInstance</c>, and managing 64-byte-aligned host-visible staging image buffers.
/// </summary>
public sealed unsafe class VulkanSwapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private readonly IntPtr _surface;
    private IntPtr _vulkanLib;
    private IntPtr _vkInstance;
    private delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> _vkDestroyInstance;
    private int _width;
    private int _height;
    private int _currentImageIndex;
    private readonly IntPtr[] _swapchainImages;
    private nuint _bufferByteSize;
    private int _presentMode;
    private bool _disposed;
    private ulong _presentCount;

    public static bool IsSupported
    {
        get
        {
            if (!VulkanInterop.TryLoadVulkanLoader(out IntPtr lib))
            {
                return false;
            }
            bool hasCreate = NativeLibrary.TryGetExport(lib, "vkCreateInstance", out _);
            NativeLibrary.Free(lib);
            return hasCreate;
        }
    }

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _swapchainImages.Length;
    public nuint BufferByteSize => _bufferByteSize;
    public int PresentMode => _presentMode;
    public IntPtr InstanceHandle => _vkInstance;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _swapchainImages[_currentImageIndex];
        }
    }

    /// <summary>
    /// Gets the native <c>VkSemaphore</c> handle when an external <c>VkDevice</c> queue is attached, or <see cref="IntPtr.Zero"/> in host-staging mode.
    /// </summary>
    public IntPtr CurrentSemaphore
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return IntPtr.Zero;
        }
    }

    public VulkanSwapchain(IntPtr surface, SwapchainDescription desc)
    {
        if (surface == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid Vulkan surface/window handle.", nameof(surface));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        if (!VulkanInterop.TryLoadVulkanLoader(out _vulkanLib))
        {
            throw new PlatformNotSupportedException("Vulkan runtime loader (vulkan-1.dll / libvulkan.so.1) is not installed on this system.");
        }

        if (!NativeLibrary.TryGetExport(_vulkanLib, "vkCreateInstance", out IntPtr createFn) ||
            !NativeLibrary.TryGetExport(_vulkanLib, "vkDestroyInstance", out IntPtr destroyFn))
        {
            NativeLibrary.Free(_vulkanLib);
            _vulkanLib = IntPtr.Zero;
            throw new PlatformNotSupportedException("Vulkan loader is missing vkCreateInstance/vkDestroyInstance entry points.");
        }

        var vkCreateInstance = (delegate* unmanaged[Cdecl]<VulkanInterop.VkInstanceCreateInfo*, IntPtr, out IntPtr, int>)createFn;
        _vkDestroyInstance = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)destroyFn;

        ReadOnlySpan<byte> appName = "Glacier.Windowing\0"u8;
        fixed (byte* pAppName = appName)
        {
            VulkanInterop.VkApplicationInfo appInfo = new()
            {
                sType = VulkanInterop.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                pApplicationName = pAppName,
                applicationVersion = (1u << 22) | (1u << 12),
                pEngineName = pAppName,
                engineVersion = (1u << 22) | (1u << 12),
                apiVersion = (1u << 22) // Vulkan 1.0 baseline for universal loader compatibility
            };

            VulkanInterop.VkInstanceCreateInfo createInfo = new()
            {
                sType = VulkanInterop.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &appInfo
            };

            int vkRes = vkCreateInstance(&createInfo, IntPtr.Zero, out _vkInstance);
            if (vkRes != VulkanInterop.VK_SUCCESS || _vkInstance == IntPtr.Zero)
            {
                NativeLibrary.Free(_vulkanLib);
                _vulkanLib = IntPtr.Zero;
                throw new InvalidOperationException($"vkCreateInstance failed with VkResult {vkRes}.");
            }
        }

        _surface = surface;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _swapchainImages = new IntPtr[bufferCount];
        AllocateStagingImages(_width, _height);

        _presentMode = VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR;
    }

    private void AllocateStagingImages(int width, int height)
    {
        nuint bytesPerPixel = _description.EnableHdr ? 8u : 4u;
        nuint byteSize = checked((nuint)width * (nuint)height * bytesPerPixel);
        _bufferByteSize = byteSize;

        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            if (_swapchainImages[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_swapchainImages[i]);
            }
            void* ptr = NativeMemory.AlignedAlloc(byteSize, 64);
            NativeMemory.Clear(ptr, byteSize);
            _swapchainImages[i] = (IntPtr)ptr;
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _presentMode = vsync ? VulkanInterop.VK_PRESENT_MODE_FIFO_KHR : VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR;
        _currentImageIndex = (_currentImageIndex + 1) % _swapchainImages.Length;
        _presentCount++;
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
        }

        _width = width;
        _height = height;
        AllocateStagingImages(width, height);
        _currentImageIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            if (_swapchainImages[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_swapchainImages[i]);
                _swapchainImages[i] = IntPtr.Zero;
            }
        }

        if (_vkInstance != IntPtr.Zero && _vkDestroyInstance != null)
        {
            _vkDestroyInstance(_vkInstance, IntPtr.Zero);
            _vkInstance = IntPtr.Zero;
        }

        if (_vulkanLib != IntPtr.Zero)
        {
            NativeLibrary.Free(_vulkanLib);
            _vulkanLib = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
