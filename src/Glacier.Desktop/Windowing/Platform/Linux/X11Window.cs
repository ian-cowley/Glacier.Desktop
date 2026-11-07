namespace Glacier.Desktop.Windowing.Platform.Linux;

using System;
using System.Diagnostics;
using Glacier.Desktop.Windowing.Swapchain;

/// <summary>
/// Direct X11 window implementation via libX11.so.6 P/Invoke.
/// </summary>
public sealed class X11Window : IWindow
{
    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _wmDeleteWindow;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public static bool IsSupported => OperatingSystem.IsLinux();

    public IntPtr NativeHandle => _window;
    public IntPtr DisplayHandle => _display;

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            if (_display != IntPtr.Zero && _window != IntPtr.Zero && value.Width > 0 && value.Height > 0)
            {
                LibX11.XResizeWindow(_display, _window, (uint)value.Width, (uint)value.Height);
            }
            Resized?.Invoke(value.Width, value.Height);
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_display != IntPtr.Zero && _window != IntPtr.Zero)
            {
                LibX11.XStoreName(_display, _window, value);
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            if (_display != IntPtr.Zero && _window != IntPtr.Zero)
            {
                if (value)
                {
                    LibX11.XMapWindow(_display, _window);
                }
                else
                {
                    LibX11.XUnmapWindow(_display, _window);
                }
            }
        }
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public X11Window(WindowOptions options)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("X11Window is only supported on Linux.");
        }
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);

        try
        {
            _display = LibX11.XOpenDisplay(null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new PlatformNotSupportedException("libX11.so.6 is not installed on this Linux host.", ex);
        }

        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open X11 display connection (DISPLAY environment variable is unset or X server is unreachable).");
        }

        IntPtr root = LibX11.XDefaultRootWindow(_display);
        _window = LibX11.XCreateSimpleWindow(
            _display,
            root,
            100, 100,
            (uint)Math.Max(1, options.Width), (uint)Math.Max(1, options.Height),
            0,
            UIntPtr.Zero,
            UIntPtr.Zero);

        if (_window == IntPtr.Zero)
        {
            LibX11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create native X11 window handle.");
        }

        LibX11.XStoreName(_display, _window, options.Title);

        IntPtr eventMask = (IntPtr)(
            LibX11.ExposureMask |
            LibX11.KeyPressMask |
            LibX11.KeyReleaseMask |
            LibX11.ButtonPressMask |
            LibX11.ButtonReleaseMask |
            LibX11.PointerMotionMask |
            LibX11.StructureNotifyMask);

        LibX11.XSelectInput(_display, _window, eventMask);

        _wmDeleteWindow = LibX11.XInternAtom(_display, "WM_DELETE_WINDOW", false);
        if (_wmDeleteWindow != IntPtr.Zero)
        {
            LibX11.XSetWMProtocols(_display, _window, ref _wmDeleteWindow, 1);
        }

        IsVisible = options.IsVisible;
    }

    public void PollEvents()
    {
        if (_disposed || _display == IntPtr.Zero || _window == IntPtr.Zero) return;

        ulong nowNs = (ulong)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

        while (LibX11.XPending(_display) > 0)
        {
            LibX11.XNextEvent(_display, out LibX11.XEvent ev);

            switch (ev.type)
            {
                case LibX11.ConfigureNotify:
                    if (ev.xconfigure.width != _size.Width || ev.xconfigure.height != _size.Height)
                    {
                        _size = new WindowSize(ev.xconfigure.width, ev.xconfigure.height);
                        Resized?.Invoke(_size.Width, _size.Height);
                    }
                    break;

                case LibX11.ClientMessage:
                    if (ev.xclient.ptr0 == _wmDeleteWindow)
                    {
                        Closing?.Invoke();
                    }
                    break;

                case LibX11.KeyPress:
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyDown, (int)ev.xkey.keycode, ev.xkey.x, ev.xkey.y, nowNs));
                    break;

                case LibX11.KeyRelease:
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyUp, (int)ev.xkey.keycode, ev.xkey.x, ev.xkey.y, nowNs));
                    break;

                case LibX11.ButtonPress:
                    if (ev.xbutton.button == 4)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseWheel, 0, ev.xbutton.x, 120f, nowNs));
                    }
                    else if (ev.xbutton.button == 5)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseWheel, 0, ev.xbutton.x, -120f, nowNs));
                    }
                    else if (MapX11MouseButton(ev.xbutton.button, out int btnDown))
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, btnDown, ev.xbutton.x, ev.xbutton.y, nowNs));
                    }
                    break;

                case LibX11.ButtonRelease:
                    if (MapX11MouseButton(ev.xbutton.button, out int btnUp))
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, btnUp, ev.xbutton.x, ev.xbutton.y, nowNs));
                    }
                    break;

                case LibX11.MotionNotify:
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseMove, 0, ev.xmotion.x, ev.xmotion.y, nowNs));
                    break;
            }
        }
    }

    private static bool MapX11MouseButton(uint x11Button, out int buttonIndex)
    {
        switch (x11Button)
        {
            case 1: buttonIndex = 0; return true; // Left
            case 2: buttonIndex = 2; return true; // Middle
            case 3: buttonIndex = 1; return true; // Right
            case 8: buttonIndex = 3; return true; // XButton1
            case 9: buttonIndex = 4; return true; // XButton2
            default: buttonIndex = 0; return false;
        }
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

        if (_display != IntPtr.Zero)
        {
            if (_window != IntPtr.Zero)
            {
                LibX11.XDestroyWindow(_display, _window);
                _window = IntPtr.Zero;
            }
            LibX11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
