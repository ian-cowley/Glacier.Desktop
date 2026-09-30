namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Codecs.Png;
using Glacier.Graphics.Raster;
using Glacier.Graphics.Vector;
using Glacier.Windowing;
using Glacier.Windowing.Platform;

/// <summary>
/// Hardware/software window renderer backed by Glacier.Graphics and Glacier.Windowing.
/// Pure managed C# .NET 10 execution with zero third-party native binary dependencies.
/// </summary>
public sealed class GlacierGraphicsRenderer : IWindowRenderer
{
    private readonly LinearFramebuffer _framebuffer;
    private readonly CpuGraphicsCanvas _canvas;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public long RenderedFrameCount { get; private set; }
    public LinearFramebuffer Framebuffer => _framebuffer;
    public IGraphicsCanvas Canvas => _canvas;

    public GlacierGraphicsRenderer(int width = 1280, int height = 800)
    {
        Width = width;
        Height = height;
        _framebuffer = new LinearFramebuffer(width, height);
        _canvas = new CpuGraphicsCanvas(_framebuffer);
    }

    public void BeginFrame()
    {
        _canvas.Clear(Rgba32.Transparent);
    }

    public void RenderTree(VisualNode root, float width, float height)
    {
        root.Measure(width, height);
        root.Arrange(new LayoutBox(width, height, 0f, 0f));
        RenderVisualNode(root);
        RenderedFrameCount++;
    }

    private void RenderVisualNode(VisualNode node)
    {
        if (!node.IsVisible) return;

        if (node.BackgroundColor.A > 0)
        {
            var path = new VectorPath();
            path.AddRect(node.Bounds.ActualX, node.Bounds.ActualY, node.Bounds.DesiredWidth, node.Bounds.DesiredHeight);
            _canvas.FillPath(path, new Paint(new Rgba32(node.BackgroundColor.R, node.BackgroundColor.G, node.BackgroundColor.B, node.BackgroundColor.A), PaintStyle.Fill));
        }

        if (node is Glacier.Desktop.UI.Containers.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                RenderVisualNode(child);
            }
        }
    }

    public void EndFrame()
    {
        _canvas.Flush();
    }

    public byte[] EncodeToPng()
    {
        return PngEncoder.Encode(_framebuffer);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
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
