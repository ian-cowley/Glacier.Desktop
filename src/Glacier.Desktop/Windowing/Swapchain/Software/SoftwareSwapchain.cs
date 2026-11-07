namespace Glacier.Desktop.Windowing.Swapchain.Software;

using System;
using System.Runtime.InteropServices;
using Glacier.Desktop.Windowing.Platform.Win32;

/// <summary>
/// High-performance CPU software swapchain allocating 64-byte-aligned unmanaged 32-bit RGBA backbuffers.
/// Used for headless testing, software rasterization, and GDI presentation fallback on Win32 windows.
/// </summary>
public sealed unsafe class SoftwareSwapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private readonly IWindow? _targetWindow;
    private int _width;
    private int _height;
    private int _currentBufferIndex;
    private readonly IntPtr[] _buffers;
    private IntPtr _bgraScratch;
    private nuint _bufferByteSize;
    private bool _disposed;
    private ulong _presentCount;

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _buffers.Length;
    public ulong PresentCount => _presentCount;
    public nuint BufferByteSize => _bufferByteSize;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffers[_currentBufferIndex];
        }
    }

    public SoftwareSwapchain(SwapchainDescription desc, IWindow? targetWindow = null)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _description = desc;
        _targetWindow = targetWindow;
        _width = desc.Width;
        _height = desc.Height;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _buffers = new IntPtr[bufferCount];
        AllocateBuffers(_width, _height);
    }

    private void AllocateBuffers(int width, int height)
    {
        nuint byteSize = checked((nuint)width * (nuint)height * 4u);
        _bufferByteSize = byteSize;

        for (int i = 0; i < _buffers.Length; i++)
        {
            if (_buffers[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_buffers[i]);
            }
            void* ptr = NativeMemory.AlignedAlloc(byteSize, 64);
            NativeMemory.Clear(ptr, byteSize);
            _buffers[i] = (IntPtr)ptr;
        }

        if (OperatingSystem.IsWindows() && _targetWindow is Win32Window)
        {
            if (_bgraScratch != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_bgraScratch);
            }
            _bgraScratch = (IntPtr)NativeMemory.AlignedAlloc(byteSize, 64);
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (OperatingSystem.IsWindows() && _targetWindow is Win32Window win32 && win32.NativeHandle != IntPtr.Zero && _bgraScratch != IntPtr.Zero)
        {
            Gdi32.PresentRgbaToHwnd(win32.NativeHandle, (void*)_buffers[_currentBufferIndex], (void*)_bgraScratch, _width, _height);
            if (vsync)
            {
                Dwmapi.DwmFlush();
            }
        }

        _currentBufferIndex = (_currentBufferIndex + 1) % _buffers.Length;
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
        AllocateBuffers(width, height);
        _currentBufferIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _buffers.Length; i++)
        {
            if (_buffers[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_buffers[i]);
                _buffers[i] = IntPtr.Zero;
            }
        }

        if (_bgraScratch != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_bgraScratch);
            _bgraScratch = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
