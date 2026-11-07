namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Codecs.Png;
using Glacier.Graphics.Raster;
using Glacier.Graphics.Vector;
using Glacier.Desktop.Windowing.Platform;

/// <summary>
/// Hardware/software window renderer backed by Glacier.Graphics and Glacier.Windowing.
/// Pure managed C# .NET 10 execution with zero third-party native binary dependencies.
/// </summary>
public sealed unsafe class GlacierGraphicsRenderer : IWindowRenderer
{
    private readonly LinearFramebuffer _framebuffer;
    private readonly CpuGraphicsCanvas _canvas;
    private readonly ISwapchain? _swapchain;
    private readonly bool _ownsSwapchain;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public long RenderedFrameCount { get; private set; }
    public LinearFramebuffer Framebuffer => _framebuffer;
    public IGraphicsCanvas Canvas => _canvas;
    public ISwapchain? Swapchain => _swapchain;

    public GlacierGraphicsRenderer(int width = 1280, int height = 800, ISwapchain? swapchain = null, bool ownsSwapchain = false)
    {
        Width = width;
        Height = height;
        _framebuffer = new LinearFramebuffer(width, height);
        _canvas = new CpuGraphicsCanvas(_framebuffer);
        _swapchain = swapchain;
        _ownsSwapchain = ownsSwapchain;
    }

    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _canvas.Clear(Rgba32.Transparent);
    }

    public void RenderTree(VisualNode root, float width, float height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        root.Measure(width, height);
        root.Arrange(new LayoutBox(width, height, 0f, 0f));
        root.Render(_canvas);
        RenderedFrameCount++;
    }

    public void EndFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _canvas.Flush();

        if (_swapchain != null && _swapchain.CurrentBackBuffer != IntPtr.Zero && _swapchain.Width == Width && _swapchain.Height == Height)
        {
            ReadOnlySpan<byte> src = _framebuffer.AsByteSpan();
            var dst = new Span<byte>((void*)_swapchain.CurrentBackBuffer, src.Length);
            src.CopyTo(dst);
            _swapchain.Present();
        }
    }

    public byte[] EncodeToPng()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return PngEncoder.Encode(_framebuffer);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsSwapchain)
            {
                _swapchain?.Dispose();
            }
            _canvas.Dispose();
            _framebuffer.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Window factory bridging Glacier.Desktop UI with Glacier.Windowing platform windows.
/// </summary>
public static class GlacierDesktopWindowFactory
{
    public static IWindow CreateManagedWindow(string title = "Glacier Desktop", int width = 1280, int height = 800)
    {
        var options = new WindowOptions
        {
            Title = title,
            Width = width,
            Height = height,
            Resizable = true,
            IsVisible = true
        };
        return WindowFactory.CreateWindow(options);
    }

    public static IWindow CreateHeadlessWindow(int width = 1280, int height = 800)
    {
        var options = new WindowOptions
        {
            Title = "Glacier Desktop Headless",
            Width = width,
            Height = height,
            Resizable = false,
            IsVisible = false
        };
        return WindowFactory.CreateHeadless(options);
    }
}
