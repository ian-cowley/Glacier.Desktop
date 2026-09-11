namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using SkiaSharp;

/// <summary>
/// Deterministic headless software window renderer for CI test runners, benchmarks, and snapshot tests.
/// </summary>
public sealed class HeadlessWindowRenderer : IWindowRenderer
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public long RenderedFrameCount { get; private set; }

    public HeadlessWindowRenderer(int width = 1280, int height = 800)
    {
        Width = width;
        Height = height;
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _canvas = new SKCanvas(_bitmap);
    }

    public void BeginFrame()
    {
        _canvas.Clear(SKColors.Transparent);
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
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _canvas.Dispose();
            _bitmap.Dispose();
            _disposed = true;
        }
    }
}
