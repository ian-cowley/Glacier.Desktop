namespace Glacier.Desktop.Windowing;

using System;

/// <summary>
/// Defines the contract for a hardware presentation swapchain (Direct3D 12 Flip Model, Vulkan WSI, Apple Metal).
/// </summary>
public interface ISwapchain : IDisposable
{
    /// <summary>
    /// Gets the current backbuffer width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the current backbuffer height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the pointer to the native render target / backbuffer surface.
    /// </summary>
    IntPtr CurrentBackBuffer { get; }

    /// <summary>
    /// Presents the rendered backbuffer to the display.
    /// </summary>
    /// <param name="vsync">True to synchronize presentation with the vertical blank interval; false for immediate unconstrained presentation (tearing).</param>
    void Present(bool vsync = true);

    /// <summary>
    /// Resizes the swapchain backbuffers to match new window dimensions.
    /// </summary>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    void Resize(int width, int height);
}
