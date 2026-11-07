namespace Glacier.Desktop.Windowing;

/// <summary>
/// Represents the width and height dimensions of a window client area in pixels.
/// </summary>
/// <param name="Width">The horizontal extent in pixels.</param>
/// <param name="Height">The vertical extent in pixels.</param>
public readonly record struct WindowSize(int Width, int Height)
{
    /// <summary>
    /// Gets an empty size (0, 0).
    /// </summary>
    public static readonly WindowSize Empty = new(0, 0);

    /// <summary>
    /// Gets the aspect ratio (width / height).
    /// </summary>
    public float AspectRatio => Height > 0 ? (float)Width / Height : 1.0f;
}

/// <summary>
/// Configuration descriptor for hardware swapchain creation.
/// </summary>
/// <param name="Width">Backbuffer width in pixels.</param>
/// <param name="Height">Backbuffer height in pixels.</param>
/// <param name="BufferCount">Number of backbuffers (2 for double buffering, 3 for triple buffering).</param>
/// <param name="EnableHdr">Whether to request HDR10 / wide-color-gamut format.</param>
/// <param name="LowLatencyWaitable">Whether to enable low-latency waitable object presentation synchronization.</param>
public readonly record struct SwapchainDescription(
    int Width,
    int Height,
    int BufferCount = 2,
    bool EnableHdr = false,
    bool LowLatencyWaitable = true);

/// <summary>
/// Hardware input event representing keyboard, mouse, or touch interaction.
/// </summary>
/// <param name="Type">The category of the input event.</param>
/// <param name="KeyOrButton">Virtual key code, mouse button index, or touch pointer ID.</param>
/// <param name="X">Normalized or absolute horizontal position, or relative horizontal delta for <see cref="InputEventType.MouseDelta"/>.</param>
/// <param name="Y">Normalized or absolute vertical position, scroll wheel delta, or relative vertical delta for <see cref="InputEventType.MouseDelta"/>.</param>
/// <param name="TimestampNs">Nanosecond precision hardware timestamp.</param>
public readonly record struct InputEvent(
    InputEventType Type,
    int KeyOrButton,
    float X,
    float Y,
    ulong TimestampNs);

/// <summary>
/// Categorization of hardware input events.
/// </summary>
public enum InputEventType : byte
{
    KeyDown = 0,
    KeyUp = 1,
    MouseMove = 2,
    MouseDown = 3,
    MouseUp = 4,
    MouseWheel = 5,
    TouchStart = 6,
    TouchMove = 7,
    TouchEnd = 8,
    MouseDelta = 9
}

/// <summary>
/// Configuration options for window creation.
/// </summary>
public sealed class WindowOptions
{
    /// <summary>
    /// Window title bar text.
    /// </summary>
    public string Title { get; set; } = "Glacier Application";

    /// <summary>
    /// Initial client width in pixels.
    /// </summary>
    public int Width { get; set; } = 1280;

    /// <summary>
    /// Initial client height in pixels.
    /// </summary>
    public int Height { get; set; } = 720;

    /// <summary>
    /// Initial visibility state.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Whether user can resize the window.
    /// </summary>
    public bool Resizable { get; set; } = true;

    /// <summary>
    /// Whether to enforce immersive dark mode on supported operating systems (Windows 10/11, macOS).
    /// </summary>
    public bool DarkMode { get; set; } = true;

    /// <summary>
    /// Whether to enable high-frequency raw input (WM_INPUT bypass on Windows, XInput2 on Linux).
    /// </summary>
    public bool EnableRawInput { get; set; } = true;

    /// <summary>
    /// Whether to enable per-monitor V2 DPI awareness.
    /// </summary>
    public bool PerMonitorDpiAware { get; set; } = true;

    /// <summary>
    /// Preferred swapchain backend.
    /// </summary>
    public SwapchainBackend PreferredBackend { get; set; } = SwapchainBackend.Auto;
}

/// <summary>
/// Supported graphics presentation backends.
/// </summary>
public enum SwapchainBackend
{
    Auto,
    Direct3D12,
    Vulkan,
    Metal,
    Software
}
