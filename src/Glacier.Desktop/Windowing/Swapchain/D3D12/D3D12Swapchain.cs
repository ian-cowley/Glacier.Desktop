namespace Glacier.Desktop.Windowing.Swapchain.D3D12;

using System;
using System.Runtime.InteropServices;
using Glacier.Desktop.Windowing.Platform.Win32;

/// <summary>
/// Windows Direct3D 12 / DXGI presentation swapchain probing hardware DXGI 1.5 tearing capabilities,
/// managing real 64-byte-aligned staging backbuffers, Win32 waitable frame latency synchronization,
/// and directHWND presentation when no external <c>ID3D12CommandQueue</c> is bound.
/// </summary>
public sealed unsafe class D3D12Swapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private readonly IntPtr _hwnd;
    private int _width;
    private int _height;
    private int _currentBufferIndex;
    private readonly IntPtr[] _backBuffers;
    private IntPtr _bgraScratch;
    private nuint _bufferByteSize;
    private bool _tearingSupported;
    private bool _hardwareDeviceAvailable;
    private IntPtr _waitableObject;
    private bool _disposed;
    private ulong _presentCount;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _backBuffers.Length;
    public nuint BufferByteSize => _bufferByteSize;
    public bool IsHdrEnabled => _description.EnableHdr;
    public bool IsTearingSupported => _tearingSupported;
    public bool IsHardwareDeviceAvailable => _hardwareDeviceAvailable;
    public IntPtr WaitableObject => _waitableObject;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _backBuffers[_currentBufferIndex];
        }
    }

    public D3D12Swapchain(IntPtr hwnd, SwapchainDescription desc)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("D3D12Swapchain is only supported on Windows.");
        }
        if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd))
        {
            throw new ArgumentException("A valid native Win32 HWND is required to create a D3D12Swapchain.", nameof(hwnd));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _hwnd = hwnd;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;

        InitializeDxgiAndD3D12();

        int bufferCount = Math.Max(2, desc.BufferCount);
        _backBuffers = new IntPtr[bufferCount];
        AllocateBackBuffers(_width, _height);

        if (_description.LowLatencyWaitable)
        {
            _waitableObject = Kernel32.CreateEventW(IntPtr.Zero, bManualReset: false, bInitialState: true, lpName: null);
            if (_waitableObject == IntPtr.Zero)
            {
                FreeBuffers();
                throw new InvalidOperationException($"Failed to create Win32 frame-latency waitable event (error {Marshal.GetLastWin32Error()}).");
            }
        }
    }

    private void InitializeDxgiAndD3D12()
    {
        int hrFactory = DxgiInterop.CreateDXGIFactory2(0, in DxgiInterop.IID_IDXGIFactory5, out IntPtr factory5);
        if (hrFactory < 0 || factory5 == IntPtr.Zero)
        {
            hrFactory = DxgiInterop.CreateDXGIFactory2(0, in DxgiInterop.IID_IDXGIFactory4, out IntPtr factory4);
            if (hrFactory < 0 || factory4 == IntPtr.Zero)
            {
                throw new InvalidOperationException($"DXGI 1.4+ factory creation failed with HRESULT 0x{hrFactory:X8}.");
            }
            Marshal.Release(factory4);
        }
        else
        {
            try
            {
                // IDXGIFactory5::CheckFeatureSupport is vtable slot 28
                void** vtbl = *(void***)factory5;
                var checkFeatureSupport = (delegate* unmanaged[Stdcall]<IntPtr, uint, void*, uint, int>)vtbl[28];
                int allowTearing = 0;
                int hrTear = checkFeatureSupport(factory5, DxgiInterop.DXGI_FEATURE_PRESENT_ALLOW_TEARING, &allowTearing, sizeof(int));
                _tearingSupported = hrTear >= 0 && allowTearing != 0;
            }
            finally
            {
                Marshal.Release(factory5);
            }
        }

        int hrDevice = D3D12Interop.D3D12CreateDevice(
            IntPtr.Zero,
            D3D12Interop.D3D_FEATURE_LEVEL_11_0,
            in DxgiInterop.IID_ID3D12Device,
            out IntPtr d3d12Device);

        if (hrDevice >= 0 && d3d12Device != IntPtr.Zero)
        {
            _hardwareDeviceAvailable = true;
            Marshal.Release(d3d12Device);
        }
        else
        {
            throw new InvalidOperationException($"Direct3D 12 hardware device creation failed with HRESULT 0x{hrDevice:X8}.");
        }
    }

    private void AllocateBackBuffers(int width, int height)
    {
        nuint bytesPerPixel = _description.EnableHdr ? 8u : 4u;
        nuint byteSize = checked((nuint)width * (nuint)height * bytesPerPixel);
        _bufferByteSize = byteSize;

        for (int i = 0; i < _backBuffers.Length; i++)
        {
            if (_backBuffers[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_backBuffers[i]);
            }
            void* ptr = NativeMemory.AlignedAlloc(byteSize, 64);
            NativeMemory.Clear(ptr, byteSize);
            _backBuffers[i] = (IntPtr)ptr;
        }

        if (_bgraScratch != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_bgraScratch);
        }
        nuint sdrBytes = checked((nuint)width * (nuint)height * 4u);
        _bgraScratch = (IntPtr)NativeMemory.AlignedAlloc(sdrBytes, 64);
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_description.EnableHdr && _bgraScratch != IntPtr.Zero)
        {
            Gdi32.PresentRgbaToHwnd(_hwnd, (void*)_backBuffers[_currentBufferIndex], (void*)_bgraScratch, _width, _height);
        }

        if (vsync)
        {
            Dwmapi.DwmFlush();
        }

        _currentBufferIndex = (_currentBufferIndex + 1) % _backBuffers.Length;
        _presentCount++;

        if (_waitableObject != IntPtr.Zero)
        {
            Kernel32.SetEvent(_waitableObject);
        }
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
        AllocateBackBuffers(width, height);
        _currentBufferIndex = 0;
    }

    private void FreeBuffers()
    {
        for (int i = 0; i < _backBuffers.Length; i++)
        {
            if (_backBuffers[i] != IntPtr.Zero)
            {
                NativeMemory.AlignedFree((void*)_backBuffers[i]);
                _backBuffers[i] = IntPtr.Zero;
            }
        }

        if (_bgraScratch != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_bgraScratch);
            _bgraScratch = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FreeBuffers();

        if (_waitableObject != IntPtr.Zero)
        {
            Kernel32.CloseHandle(_waitableObject);
            _waitableObject = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
