namespace Glacier.Desktop.Windowing.Platform.Mac;

using System;
using System.Runtime.InteropServices;
using Glacier.Desktop.Windowing.Swapchain;

/// <summary>
/// Native macOS Cocoa window implementation utilizing pure C# Objective-C runtime dispatch via libobjc.A.dylib.
/// </summary>
public sealed class CocoaWindow : IWindow
{
    private IntPtr _nsWindow;
    private IntPtr _metalLayer;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public static bool IsSupported => OperatingSystem.IsMacOS();

    public IntPtr NativeHandle => _nsWindow;
    public IntPtr MetalLayer => _metalLayer;

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            Resized?.Invoke(value.Width, value.Height);
        }
    }

    public string Title
    {
        get => _title;
        set => _title = value;
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public CocoaWindow(WindowOptions options)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("CocoaWindow is only supported on macOS.");
        }
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;

        InitializeCocoa(options);
    }

    private void InitializeCocoa(WindowOptions options)
    {
        try
        {
            LibObjC.EnsureFrameworksLoaded();

            IntPtr nsAppClass = LibObjC.objc_getClass("NSApplication");
            if (nsAppClass == IntPtr.Zero)
            {
                throw new InvalidOperationException("NSApplication Objective-C class could not be loaded.");
            }

            IntPtr sharedAppSel = LibObjC.sel_registerName("sharedApplication");
            IntPtr app = LibObjC.objc_msgSend(nsAppClass, sharedAppSel);
            if (app == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to initialize NSApplication.sharedApplication.");
            }

            IntPtr setActivationPolicySel = LibObjC.sel_registerName("setActivationPolicy:");
            LibObjC.objc_msgSend(app, setActivationPolicySel, IntPtr.Zero);

            IntPtr nsWindowClass = LibObjC.objc_getClass("NSWindow");
            IntPtr allocSel = LibObjC.sel_registerName("alloc");
            IntPtr initSel = LibObjC.sel_registerName("initWithContentRect:styleMask:backing:defer:");

            // NSWindowStyleMaskTitled (1) | Closable (2) | Miniaturizable (4) | Resizable (8)
            nuint styleMask = options.Resizable ? 15u : 7u;
            var rect = new LibObjC.NSRect
            {
                Origin = new LibObjC.CGPoint { X = 100, Y = 100 },
                Size = new LibObjC.CGSize { Width = Math.Max(1, options.Width), Height = Math.Max(1, options.Height) }
            };

            IntPtr allocated = LibObjC.objc_msgSend(nsWindowClass, allocSel);
            _nsWindow = LibObjC.objc_msgSend_InitWindow(allocated, initSel, rect, styleMask, 2u, 0);
            if (_nsWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("NSWindow initialization returned nil (display server may be unavailable).");
            }

            IntPtr caMetalLayerClass = LibObjC.objc_getClass("CAMetalLayer");
            if (caMetalLayerClass != IntPtr.Zero)
            {
                IntPtr layerSel = LibObjC.sel_registerName("layer");
                _metalLayer = LibObjC.objc_msgSend(caMetalLayerClass, layerSel);
            }

            if (options.IsVisible)
            {
                IntPtr makeKeySel = LibObjC.sel_registerName("makeKeyAndOrderFront:");
                LibObjC.objc_msgSend(_nsWindow, makeKeySel, IntPtr.Zero);
            }
        }
        catch (Exception ex) when (ex is not PlatformNotSupportedException and not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to initialize native macOS Cocoa window.", ex);
        }
    }

    public void PollEvents()
    {
        if (_disposed || _nsWindow == IntPtr.Zero) return;
    }

    /// <summary>
    /// Dispatches an input event directly into the window input listeners.
    /// </summary>
    public void DispatchInput(in InputEvent evt)
    {
        InputReceived?.Invoke(evt);
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Closing?.Invoke();
        if (_nsWindow != IntPtr.Zero && OperatingSystem.IsMacOS())
        {
            try
            {
                IntPtr closeSel = LibObjC.sel_registerName("close");
                LibObjC.objc_msgSend(_nsWindow, closeSel);
            }
            catch
            {
            }
        }
        _nsWindow = IntPtr.Zero;
        _metalLayer = IntPtr.Zero;

        GC.SuppressFinalize(this);
    }
}
