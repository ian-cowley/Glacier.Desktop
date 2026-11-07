namespace Glacier.Desktop.Windowing.Platform;

using System;

/// <summary>
/// Identifies the native platform windowing implementation.
/// </summary>
public enum PlatformKind
{
    Windows,
    LinuxX11,
    LinuxWayland,
    MacOS,
    Android,
    IOS,
    Headless
}

/// <summary>
/// Factory for creating native or headless OS windows across supported platforms.
/// </summary>
public static class WindowFactory
{
    /// <summary>
    /// Detects current host operating system platform.
    /// </summary>
    public static PlatformKind CurrentPlatform
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return PlatformKind.Windows;
            }
            if (OperatingSystem.IsMacOS())
            {
                return PlatformKind.MacOS;
            }
            if (OperatingSystem.IsAndroid())
            {
                return PlatformKind.Android;
            }
            if (OperatingSystem.IsIOS())
            {
                return PlatformKind.IOS;
            }
            if (OperatingSystem.IsLinux())
            {
                string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                if (!string.IsNullOrEmpty(waylandDisplay))
                {
                    return PlatformKind.LinuxWayland;
                }
                return PlatformKind.LinuxX11;
            }
            return PlatformKind.Headless;
        }
    }

    /// <summary>
    /// Creates a native OS window matching the host environment, or falls back to <see cref="Headless.HeadlessWindow"/>
    /// in non-graphical environments (such as headless Linux/Windows CI runners or SSH sessions).
    /// </summary>
    /// <param name="options">Window configuration options.</param>
    /// <returns>A concrete <see cref="IWindow"/> instance.</returns>
    public static IWindow CreateWindow(WindowOptions? options = null)
    {
        options ??= new WindowOptions();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new Win32.Win32Window(options);
            }
            catch
            {
                return new Headless.HeadlessWindow(options);
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                return new Mac.CocoaWindow(options);
            }
            catch
            {
                return new Headless.HeadlessWindow(options);
            }
        }

        if (OperatingSystem.IsAndroid())
        {
            return new Mobile.AndroidNativeWindow(options);
        }

        if (OperatingSystem.IsIOS())
        {
            return new Mobile.IosUiWindow(options);
        }

        if (OperatingSystem.IsLinux())
        {
            string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            if (!string.IsNullOrEmpty(wayland))
            {
                try
                {
                    return new Linux.WaylandWindow(options);
                }
                catch
                {
                    // Fall through to X11 or Headless
                }
            }

            try
            {
                return new Linux.X11Window(options);
            }
            catch
            {
                return new Headless.HeadlessWindow(options);
            }
        }

        return new Headless.HeadlessWindow(options);
    }

    /// <summary>
    /// Creates a headless / virtual window for unit testing and CI pipelines.
    /// </summary>
    /// <param name="options">Window configuration options.</param>
    /// <returns>A <see cref="Headless.HeadlessWindow"/> instance.</returns>
    public static IWindow CreateHeadless(WindowOptions? options = null)
    {
        return new Headless.HeadlessWindow(options ?? new WindowOptions());
    }
}
