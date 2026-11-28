namespace Glacier.Desktop.Windowing.Platform.Win32;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
using Glacier.Desktop.Grids;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using Glacier.Desktop.UI.Controls;
using Glacier.Desktop.Windowing.Swapchain;
using Glacier.Graphics;
using Glacier.Graphics.Raster;

/// <summary>
/// Unified high-performance Win32 window implementation (<see cref="IWindow"/>) utilizing direct
/// <c>[LibraryImport]</c> P/Invoke to User32, Dwmapi, and Gdi32, with lazy UI visual-tree hosting
/// and SIMD-accelerated RGBA-to-BGRA GDI blitting.
/// </summary>
public sealed unsafe class Win32Window : IWindow
{
    private static bool s_dpiAwarenessInitialized;
    private readonly string _className;
    private readonly User32.WndProc _wndProcDelegate;
    private readonly uint _creatorThreadId;
    private IntPtr _hInstance;
    private IntPtr _hWnd;
    private bool _classRegistered;
    private bool _disposed;
    private bool _isVisible;
    private WindowSize _size;
    private string _title;
    private readonly bool _rawInputEnabled;

    private readonly IntPtr _hCursorArrow;
    private readonly IntPtr _hCursorHand;

    // Lazy UI hosting & GDI framebuffer state (null when used purely as IWindow + ISwapchain)
    private LinearFramebuffer? _framebuffer;
    private CpuGraphicsCanvas? _canvas;
    private byte* _bgraScratch;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private Gdi32.BITMAPINFO _bmi;
    private bool _isAlive = true;
    private bool _inRunLoop;

    private DesktopButton? _hoveredButton;
    private VirtualDataGrid? _activeGrid;
    private bool _isLButtonDown;

    public IntPtr NativeHandle => _hWnd;
    public int Width => _size.Width;
    public int Height => _size.Height;

    public VisualNode? RootVisual { get; set; }
    public Action<int>? ScrollCallback { get; set; }
    public Action<int>? KeyDownCallback { get; set; }
    public Action<float, float>? MouseDownCallback { get; set; }

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.RECT rect = new() { left = 0, top = 0, right = value.Width, bottom = value.Height };
                User32.AdjustWindowRectEx(ref rect, User32.WS_OVERLAPPEDWINDOW, false, 0);
                User32.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, rect.Width, rect.Height, 0x0002 | 0x0004); // SWP_NOMOVE | SWP_NOZORDER
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.SetWindowTextW(_hWnd, value);
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.ShowWindow(_hWnd, value ? User32.SW_SHOWNORMAL : User32.SW_HIDE);
            }
        }
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public Win32Window(string title, int width, int height, VisualNode? rootVisual = null)
        : this(new WindowOptions
        {
            Title = title ?? "Glacier Desktop",
            Width = width,
            Height = height,
            IsVisible = true,
            EnableRawInput = false
        })
    {
        RootVisual = rootVisual;
        if (_hWnd != IntPtr.Zero)
        {
            User32.UpdateWindow(_hWnd);
        }
    }

    public Win32Window(WindowOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32Window is only supported on Windows.");
        }
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Height);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _rawInputEnabled = options.EnableRawInput;
        _creatorThreadId = Kernel32.GetCurrentThreadId();

        InitializeDpiAwareness(options.PerMonitorDpiAware);

        _hCursorArrow = User32.LoadCursorW(IntPtr.Zero, User32.IDC_ARROW);
        _hCursorHand = User32.LoadCursorW(IntPtr.Zero, User32.IDC_HAND);

        _className = $"Glacier_Win32_{Guid.NewGuid():N}";
        _hInstance = Kernel32.GetModuleHandleW(null);
        _wndProcDelegate = ProcessMessage;

        fixed (char* pClassName = _className)
        {
            User32.WNDCLASSEXW wc = new()
            {
                cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEXW>(),
                style = 0x0020 | 0x0002 | 0x0001, // CS_OWNDC | CS_HREDRAW | CS_VREDRAW
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = _hInstance,
                lpszClassName = pClassName,
                hCursor = _hCursorArrow
            };

            if (User32.RegisterClassExW(ref wc) == 0)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register Win32 window class '{_className}'. Error code: {err}");
            }
        }
        _classRegistered = true;

        uint style = User32.WS_OVERLAPPEDWINDOW;
        if (!options.Resizable)
        {
            style &= ~((uint)User32.WS_THICKFRAME | (uint)User32.WS_MAXIMIZEBOX);
        }

        User32.RECT rect = new() { left = 0, top = 0, right = options.Width, bottom = options.Height };
        User32.AdjustWindowRectEx(ref rect, style, false, User32.WS_EX_APPWINDOW);

        _hWnd = User32.CreateWindowExW(
            User32.WS_EX_APPWINDOW,
            _className,
            options.Title,
            style,
            100,
            100,
            rect.Width,
            rect.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            User32.UnregisterClassW(_className, _hInstance);
            _classRegistered = false;
            throw new InvalidOperationException($"Failed to create native Win32 window. Error code: {err}");
        }

        if (options.DarkMode)
        {
            int useDarkMode = 1;
            Dwmapi.DwmSetWindowAttribute(_hWnd, Dwmapi.DWMWA_USE_IMMERSIVE_DARK_MODE, &useDarkMode, sizeof(int));
        }

        if (options.EnableRawInput)
        {
            Win32RawInput.Register(_hWnd);
        }

        IsVisible = options.IsVisible;
    }

    public VisualNode? ActivePopup
    {
        get
        {
            if (RootVisual is Panel panel)
            {
                for (int i = 0; i < panel.Children.Count; i++)
                {
                    if (panel.Children[i] is MenuBar mb && mb.ActiveDropDown != null)
                        return mb.ActiveDropDown;
                }
            }
            return null;
        }
    }

    public void CloseActiveMenus()
    {
        if (RootVisual is Panel panel)
        {
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is MenuBar mb)
                    mb.CloseMenu();
            }
        }
    }

    public void Close()
    {
        _isAlive = false;
        if (_inRunLoop)
        {
            User32.PostQuitMessage(0);
        }
    }

    public void SetTitle(string title)
    {
        Title = title;
    }

    private void EnsureRenderSurface(int width, int height)
    {
        int w = Math.Max(1, width);
        int h = Math.Max(1, height);
        if (_framebuffer != null && _canvas != null && _bgraScratch != null && _surfaceWidth == w && _surfaceHeight == h)
        {
            return;
        }

        _canvas?.Dispose();
        _canvas = null;
        _framebuffer?.Dispose();
        _framebuffer = null;

        if (_bgraScratch != null)
        {
            NativeMemory.AlignedFree(_bgraScratch);
            _bgraScratch = null;
        }

        _framebuffer = new LinearFramebuffer(w, h);
        try
        {
            _canvas = new CpuGraphicsCanvas(_framebuffer);
        }
        catch
        {
            _framebuffer.Dispose();
            _framebuffer = null;
            throw;
        }

        nuint byteSize = checked((nuint)w * (nuint)h * 4u);
        _bgraScratch = (byte*)NativeMemory.AlignedAlloc(byteSize, 64);
        NativeMemory.Clear(_bgraScratch, byteSize);

        _bmi = new Gdi32.BITMAPINFO
        {
            bmiHeader = new Gdi32.BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(Gdi32.BITMAPINFOHEADER),
                biWidth = w,
                biHeight = -h, // Top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Gdi32.BI_RGB
            }
        };

        _surfaceWidth = w;
        _surfaceHeight = h;
    }

    public void RenderFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (RootVisual == null) return;

        int w = Math.Max(1, _size.Width);
        int h = Math.Max(1, _size.Height);
        EnsureRenderSurface(w, h);

        _canvas!.Clear(Rgba32.White);
        RootVisual.Measure(w, h);
        RootVisual.Arrange(new LayoutBox(w, h, 0f, 0f));
        RootVisual.Render(_canvas);

        var popup = ActivePopup;
        if (popup != null)
        {
            popup.Render(_canvas);
        }

        _canvas.Flush();

        if (_hWnd != IntPtr.Zero)
        {
            IntPtr hdc = User32.GetDC(_hWnd);
            if (hdc != IntPtr.Zero)
            {
                try
                {
                    BlitFramebufferToDc(hdc);
                }
                finally
                {
                    User32.ReleaseDC(_hWnd, hdc);
                }
            }
        }
    }

    /// <summary>
    /// Converts 32-bit RGBA pixels from <see cref="LinearFramebuffer"/> into 32-bit BGRA order required by Win32 GDI <c>BI_RGB</c> bitmaps.
    /// </summary>
    public static void ConvertRgbaToBgra(ReadOnlySpan<byte> rgbaSrc, Span<byte> bgraDst)
    {
        if (rgbaSrc.Length % 4 != 0)
        {
            throw new ArgumentException("Source buffer length must be a multiple of 4 bytes.", nameof(rgbaSrc));
        }
        if (bgraDst.Length < rgbaSrc.Length)
        {
            throw new ArgumentException("Destination buffer is smaller than source buffer.", nameof(bgraDst));
        }

        int i = 0;
        if (Vector128.IsHardwareAccelerated && rgbaSrc.Length >= 16)
        {
            Vector128<byte> mask = Vector128.Create(
                (byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);

            for (; i <= rgbaSrc.Length - 16; i += 16)
            {
                Vector128<byte> v = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(rgbaSrc.Slice(i, 16)));
                Vector128<byte> shuffled = Vector128.Shuffle(v, mask);
                shuffled.StoreUnsafe(ref MemoryMarshal.GetReference(bgraDst.Slice(i, 16)));
            }
        }

        for (; i <= rgbaSrc.Length - 4; i += 4)
        {
            byte r = rgbaSrc[i];
            byte g = rgbaSrc[i + 1];
            byte b = rgbaSrc[i + 2];
            byte a = rgbaSrc[i + 3];
            bgraDst[i] = b;
            bgraDst[i + 1] = g;
            bgraDst[i + 2] = r;
            bgraDst[i + 3] = a;
        }
    }

    private void BlitFramebufferToDc(IntPtr hdc)
    {
        if (_framebuffer == null || _bgraScratch == null) return;

        ReadOnlySpan<byte> rgba = _framebuffer.AsByteSpan();
        var bgra = new Span<byte>(_bgraScratch, rgba.Length);
        ConvertRgbaToBgra(rgba, bgra);

        fixed (Gdi32.BITMAPINFO* pBmi = &_bmi)
        {
            Gdi32.SetDIBitsToDevice(
                hdc, 0, 0, (uint)_surfaceWidth, (uint)_surfaceHeight,
                0, 0, 0, (uint)_surfaceHeight,
                _bgraScratch, pBmi, Gdi32.DIB_RGB_COLORS);
        }
    }

    public void RunLoop(Action<float>? onStep = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();
        double lastSec = 0.0;
        _inRunLoop = true;

        try
        {
            while (_isAlive && !_disposed)
            {
                while (User32.PeekMessageW(out User32.MSG msg, IntPtr.Zero, 0, 0, User32.PM_REMOVE))
                {
                    if (msg.message == User32.WM_QUIT)
                    {
                        _isAlive = false;
                        break;
                    }
                    User32.TranslateMessage(ref msg);
                    User32.DispatchMessageW(ref msg);
                }

                if (!_isAlive || _disposed) break;

                double nowSec = sw.Elapsed.TotalSeconds;
                float dt = (float)(nowSec - lastSec);
                lastSec = nowSec;

                onStep?.Invoke(dt);
                RenderFrame();

                Thread.Sleep(8); // Cap to ~120 FPS
            }
        }
        finally
        {
            _inRunLoop = false;
        }
    }

    private static void InitializeDpiAwareness(bool perMonitorDpi)
    {
        if (s_dpiAwarenessInitialized || !perMonitorDpi) return;
        try
        {
            User32.SetProcessDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Fallback gracefully on older OS revisions or if process DPI awareness was already locked
        }
        s_dpiAwarenessInitialized = true;
    }

    public void PollEvents()
    {
        if (_disposed || _hWnd == IntPtr.Zero) return;

        while (User32.PeekMessageW(out User32.MSG msg, _hWnd, 0, 0, User32.PM_REMOVE))
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessageW(ref msg);
            if (_hWnd == IntPtr.Zero)
            {
                break;
            }
        }
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    private IntPtr ProcessMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        ulong nowNs = (ulong)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

        switch (msg)
        {
            case User32.WM_ERASEBKGND:
                if (RootVisual != null || _framebuffer != null)
                {
                    return (IntPtr)1;
                }
                break;

            case User32.WM_PAINT:
                if (_framebuffer != null && _bgraScratch != null)
                {
                    User32.BeginPaint(hWnd, out User32.PAINTSTRUCT ps);
                    if (ps.hdc != IntPtr.Zero)
                    {
                        BlitFramebufferToDc(ps.hdc);
                    }
                    User32.EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                }
                break;

            case User32.WM_SETCURSOR:
                if (_hoveredButton != null)
                {
                    User32.SetCursor(_hCursorHand);
                    return (IntPtr)1;
                }
                break;

            case User32.WM_SIZE:
                int width = (int)(lParam.ToInt64() & 0xFFFF);
                int height = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                _size = new WindowSize(width, height);
                Resized?.Invoke(width, height);
                return IntPtr.Zero;

            case User32.WM_DPICHANGED:
                if (lParam != IntPtr.Zero)
                {
                    User32.RECT* suggested = (User32.RECT*)lParam;
                    User32.SetWindowPos(
                        hWnd,
                        IntPtr.Zero,
                        suggested->left,
                        suggested->top,
                        suggested->Width,
                        suggested->Height,
                        0x0004 | 0x0010); // SWP_NOZORDER | SWP_NOACTIVATE
                }
                return IntPtr.Zero;

            case User32.WM_CLOSE:
                _isAlive = false;
                Closing?.Invoke();
                User32.DestroyWindow(hWnd);
                return IntPtr.Zero;

            case User32.WM_DESTROY:
                _hWnd = IntPtr.Zero;
                _isAlive = false;
                if (_inRunLoop)
                {
                    User32.PostQuitMessage(0);
                }
                return IntPtr.Zero;

            case User32.WM_INPUT:
                if (_rawInputEnabled)
                {
                    Win32RawInput.Process(hWnd, lParam, InputReceived, nowNs);
                }
                break;

            case User32.WM_KEYDOWN:
                int vk = (int)wParam.ToInt64();
                if (!_rawInputEnabled)
                {
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyDown, vk, 0, 0, nowNs));
                }

                if (RootVisual != null || KeyDownCallback != null)
                {
                    if (vk == 0x1B) // Escape
                    {
                        if (ActivePopup != null)
                        {
                            CloseActiveMenus();
                            RenderFrame();
                            return IntPtr.Zero;
                        }

                        if (_inRunLoop)
                        {
                            _isAlive = false;
                            User32.PostQuitMessage(0);
                            return IntPtr.Zero;
                        }
                    }

                    if (KeyDownCallback != null)
                    {
                        KeyDownCallback.Invoke(vk);
                        RenderFrame();
                        return IntPtr.Zero;
                    }
                }
                break;

            case User32.WM_KEYUP:
                if (!_rawInputEnabled)
                {
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyUp, (int)wParam, 0, 0, nowNs));
                }
                break;

            case User32.WM_MOUSEMOVE:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (!_rawInputEnabled)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseMove, 0, mx, my, nowNs));
                    }

                    if (RootVisual != null)
                    {
                        HandleUiMouseMove(mx, my);
                        return IntPtr.Zero;
                    }
                }
                break;

            case User32.WM_LBUTTONDOWN:
                {
                    int lx = (short)(lParam.ToInt64() & 0xFFFF);
                    int ly = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (!_rawInputEnabled)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, 0, lx, ly, nowNs));
                    }

                    if (RootVisual != null || MouseDownCallback != null)
                    {
                        HandleUiMouseDown(lx, ly);
                        return IntPtr.Zero;
                    }
                }
                break;

            case User32.WM_LBUTTONUP:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (!_rawInputEnabled)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, 0, mx, my, nowNs));
                    }

                    if (RootVisual != null)
                    {
                        _isLButtonDown = false;
                        if (_hoveredButton != null)
                        {
                            _hoveredButton.IsPressed = false;
                            RenderFrame();
                        }
                        if (_activeGrid != null)
                        {
                            _activeGrid.HandleMouseUp();
                            _activeGrid = null;
                        }
                        return IntPtr.Zero;
                    }
                }
                break;

            case User32.WM_RBUTTONDOWN:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, 1, mx, my, nowNs));
                }
                break;

            case User32.WM_RBUTTONUP:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, 1, mx, my, nowNs));
                }
                break;

            case User32.WM_MBUTTONDOWN:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, 2, mx, my, nowNs));
                }
                break;

            case User32.WM_MBUTTONUP:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, 2, mx, my, nowNs));
                }
                break;

            case User32.WM_XBUTTONDOWN:
                if (!_rawInputEnabled)
                {
                    int xButton = ((int)(wParam.ToInt64() >> 16) & 0xFFFF) == 2 ? 4 : 3;
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, xButton, mx, my, nowNs));
                }
                break;

            case User32.WM_XBUTTONUP:
                if (!_rawInputEnabled)
                {
                    int xButton = ((int)(wParam.ToInt64() >> 16) & 0xFFFF) == 2 ? 4 : 3;
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, xButton, mx, my, nowNs));
                }
                break;

            case User32.WM_MOUSEWHEEL:
                {
                    short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (!_rawInputEnabled)
                    {
                        InputReceived?.Invoke(new InputEvent(InputEventType.MouseWheel, 0, 0, delta, nowNs));
                    }

                    if (ScrollCallback != null)
                    {
                        ScrollCallback.Invoke(delta);
                        RenderFrame();
                        return IntPtr.Zero;
                    }
                }
                break;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void HandleUiMouseMove(int mx, int my)
    {
        if (ActivePopup is MenuDropDown activeDrop)
        {
            var hitItem = activeDrop.HitTest(mx, my);
            bool dropHoverChanged = false;
            foreach (var itm in activeDrop.Items)
            {
                bool isHov = itm == hitItem && !itm.IsSeparator;
                if (itm.IsHovered != isHov)
                {
                    itm.IsHovered = isHov;
                    dropHoverChanged = true;
                }
            }
            if (dropHoverChanged)
            {
                RenderFrame();
            }

            if (hitItem is MenuItem m && !m.IsSeparator)
            {
                User32.SetCursor(_hCursorHand);
                return;
            }

            if (RootVisual != null)
            {
                var hitRoot = RootVisual.HitTest(mx, my);
                if (hitRoot is MenuItem topMi && topMi.IsTopLevel && topMi.MenuBar != null && topMi.MenuBar.OpenMenu != topMi)
                {
                    topMi.MenuBar.ToggleMenu(topMi);
                    RenderFrame();
                    return;
                }
            }
        }

        if (_activeGrid != null && _isLButtonDown && _activeGrid.HandleMouseMove(mx, my, true))
        {
            RenderFrame();
        }

        if (RootVisual != null)
        {
            var hit = RootVisual.HitTest(mx, my);
            if (hit is DesktopButton btn)
            {
                User32.SetCursor(_hCursorHand);
                if (_hoveredButton != btn)
                {
                    if (_hoveredButton != null) _hoveredButton.IsHovered = false;
                    _hoveredButton = btn;
                    btn.IsHovered = true;
                    RenderFrame();
                }
            }
            else if (hit is MenuItem mi && mi.IsTopLevel)
            {
                User32.SetCursor(_hCursorHand);
                if (_hoveredButton != null)
                {
                    _hoveredButton.IsHovered = false;
                    _hoveredButton = null;
                    RenderFrame();
                }
            }
            else
            {
                User32.SetCursor(_hCursorArrow);
                if (_hoveredButton != null)
                {
                    _hoveredButton.IsHovered = false;
                    _hoveredButton = null;
                    RenderFrame();
                }
            }
        }
    }

    private void HandleUiMouseDown(int lx, int ly)
    {
        _isLButtonDown = true;
        MouseDownCallback?.Invoke(lx, ly);

        var curPopup = ActivePopup;
        if (curPopup != null)
        {
            var hitDrop = curPopup.HitTest(lx, ly);
            if (hitDrop is MenuItem dropItem && !dropItem.IsSeparator)
            {
                dropItem.PerformClick();
                CloseActiveMenus();
                RenderFrame();
                return;
            }

            var topHit = RootVisual?.HitTest(lx, ly);
            if (topHit is MenuItem topMi && topMi.IsTopLevel)
            {
                topMi.PerformClick();
                RenderFrame();
                return;
            }

            CloseActiveMenus();
            RenderFrame();
        }

        if (RootVisual != null)
        {
            var hit = RootVisual.HitTest(lx, ly);
            if (hit is DesktopButton lBtn)
            {
                lBtn.IsPressed = true;
                lBtn.PerformClick();
                RenderFrame();
            }
            else if (hit is MenuItem item)
            {
                item.PerformClick();
                RenderFrame();
            }
            else if (hit is VirtualDataGrid g)
            {
                _activeGrid = g;
                if (g.HandleMouseDown(lx, ly))
                {
                    RenderFrame();
                }
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        _isAlive = false;

        bool destroyedOnCreatorThread = true;
        IntPtr hwnd = _hWnd;
        if (hwnd != IntPtr.Zero)
        {
            if (Kernel32.GetCurrentThreadId() == _creatorThreadId)
            {
                // Win32 DestroyWindow enforces strict creator-thread affinity.
                User32.DestroyWindow(hwnd);
                _hWnd = IntPtr.Zero;
            }
            else
            {
                // Cross-thread disposal or GC finalizer thread: post WM_CLOSE asynchronously to the owning thread.
                destroyedOnCreatorThread = false;
                User32.PostMessageW(hwnd, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                _hWnd = IntPtr.Zero;
            }
        }

        if (_bgraScratch != null)
        {
            NativeMemory.AlignedFree(_bgraScratch);
            _bgraScratch = null;
        }

        if (disposing)
        {
            _canvas?.Dispose();
            _canvas = null;
            _framebuffer?.Dispose();
            _framebuffer = null;

            if (destroyedOnCreatorThread && _classRegistered && !string.IsNullOrEmpty(_className) && _hInstance != IntPtr.Zero)
            {
                User32.UnregisterClassW(_className, _hInstance);
                _classRegistered = false;
            }
        }
    }

    ~Win32Window()
    {
        Dispose(disposing: false);
    }
}
