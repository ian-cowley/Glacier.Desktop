namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Codecs.Png;
using Glacier.Graphics.Raster;

/// <summary>
/// Deterministic headless software window renderer for CI test runners, benchmarks, and snapshot tests.
/// Pure managed C# .NET 10 execution with zero third-party native binary dependencies.
/// </summary>
public sealed class HeadlessWindowRenderer : IWindowRenderer
{
    private readonly LinearFramebuffer _framebuffer;
    private readonly CpuGraphicsCanvas _canvas;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public long RenderedFrameCount { get; private set; }
    public LinearFramebuffer Framebuffer => _framebuffer;
    public IGraphicsCanvas Canvas => _canvas;

    public HeadlessWindowRenderer(int width = 1280, int height = 800)
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
        root.Render(_canvas);
        RenderedFrameCount++;
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
