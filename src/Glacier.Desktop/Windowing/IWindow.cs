namespace Glacier.Desktop.Windowing;

using System;

/// <summary>
/// Defines the contract for an operating system window.
/// </summary>
public interface IWindow : IDisposable
{
    /// <summary>
    /// Gets the native OS window handle (HWND on Windows, Window/XID on X11, wl_surface* on Wayland, NSWindow* on macOS, ANativeWindow* on Android, UIWindow* on iOS).
    /// </summary>
    IntPtr NativeHandle { get; }

    /// <summary>
    /// Gets or sets the size of the client area in pixels.
    /// </summary>
    WindowSize Size { get; set; }

    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window is visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Pumps and dispatches all pending OS messages and hardware input events without blocking.
    /// </summary>
    void PollEvents();

    /// <summary>
    /// Creates a hardware swapchain bound to this window surface.
    /// </summary>
    /// <param name="desc">Swapchain configuration descriptor.</param>
    /// <returns>A hardware presentation swapchain instance.</returns>
    ISwapchain CreateSwapchain(SwapchainDescription desc);

    /// <summary>
    /// Occurs when the window client area is resized.
    /// </summary>
    event Action<int, int>? Resized;

    /// <summary>
    /// Occurs when hardware input (keyboard, mouse, touch, raw input) is received.
    /// </summary>
    event Action<InputEvent>? InputReceived;

    /// <summary>
    /// Occurs when the window is being closed.
    /// </summary>
    event Action? Closing;
}
