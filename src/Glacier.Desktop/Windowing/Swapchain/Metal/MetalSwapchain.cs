namespace Glacier.Desktop.Windowing.Swapchain.Metal;

using System;
using System.Runtime.InteropServices;
using Glacier.Desktop.Windowing.Platform.Mac;

/// <summary>
/// Apple Metal presentation swapchain acquiring the system <c>MTLDevice</c>, configuring <c>CAMetalLayer</c>,
/// and managing 64-byte-aligned staging drawable buffers.
/// </summary>
public sealed unsafe class MetalSwapchain : ISwapchain
{
    public const int MTLPixelFormatBGRA8Unorm = 80;
    public const int MTLPixelFormatRGBA16Float = 115;

    private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";

    [DllImport(MetalFramework)]
    private static extern IntPtr MTLCreateSystemDefaultDevice();

    private readonly SwapchainDescription _description;
    private readonly IntPtr _metalLayer;
    private IntPtr _mtlDevice;
    private int _width;
    private int _height;
    private int _currentDrawableIndex;
    private readonly IntPtr[] _drawables;
    private nuint _bufferByteSize;
    private readonly int _pixelFormat;
    private bool _disposed;
    private ulong _presentCount;

    public static bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _drawables.Length;
    public nuint BufferByteSize => _bufferByteSize;
    public int PixelFormat => _pixelFormat;
    public IntPtr DeviceHandle => _mtlDevice;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _drawables[_currentDrawableIndex];
        }
    }

    public MetalSwapchain(IntPtr metalLayer, SwapchainDescription desc)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("MetalSwapchain is only supported on macOS and iOS.");
        }
        if (metalLayer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid CAMetalLayer handle.", nameof(metalLayer));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _mtlDevice = MTLCreateSystemDefaultDevice();
        if (_mtlDevice == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Apple Metal GPU device is available on this host.");
        }

        _metalLayer = metalLayer;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;
        _pixelFormat = desc.EnableHdr ? MTLPixelFormatRGBA16Float : MTLPixelFormatBGRA8Unorm;

        ConfigureMetalLayer();

        int bufferCount = Math.Max(2, desc.BufferCount);
        _drawables = new IntPtr[bufferCount];
        AllocateDrawables(_width, _height);
    }

    private void ConfigureMetalLayer()
    {
        IntPtr setDeviceSel = LibObjC.sel_registerName("setDevice:");
        LibObjC.objc_msgSend(_metalLayer, setDeviceSel, _mtlDevice);

        IntPtr setPixelFormatSel = LibObjC.sel_registerName("setPixelFormat:");
        LibObjC.objc_msgSend(_metalLayer, setPixelFormatSel, (nuint)_pixelFormat);

        IntPtr setDrawableSizeSel = LibObjC.sel_registerName("setDrawableSize:");
        LibObjC.objc_msgSend_CGSize(_metalLayer, setDrawableSizeSel, new LibObjC.CGSize { Width = _width, Height = _height });
    }

    private void AllocateDrawables(int width, int height)
    {
        nuint bytesPerPixel = _pixelFormat == MTLPixelFormatRGBA16Float ? 8u : 4u;
        nuint byteSize = checked((nuint)width * (nuint)height * bytesPerPixel);
        _bufferByteSize = byteSize;

        for (int i = 0; i < _drawables.Length; i++)
        {
            if (_drawables[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_drawables[i]);
            }
            void* ptr = NativeMemory.AlignedAlloc(byteSize, 64);
            NativeMemory.Clear(ptr, byteSize);
            _drawables[i] = (IntPtr)ptr;
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _currentDrawableIndex = (_currentDrawableIndex + 1) % _drawables.Length;
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
        AllocateDrawables(width, height);

        IntPtr setDrawableSizeSel = LibObjC.sel_registerName("setDrawableSize:");
        LibObjC.objc_msgSend_CGSize(_metalLayer, setDrawableSizeSel, new LibObjC.CGSize { Width = width, Height = height });
        _currentDrawableIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _drawables.Length; i++)
        {
            if (_drawables[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_drawables[i]);
                _drawables[i] = IntPtr.Zero;
            }
        }

        if (_mtlDevice != IntPtr.Zero)
        {
            IntPtr releaseSel = LibObjC.sel_registerName("release");
            LibObjC.objc_msgSend(_mtlDevice, releaseSel);
            _mtlDevice = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
