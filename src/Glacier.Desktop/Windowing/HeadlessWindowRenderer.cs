namespace Glacier.Desktop.Windowing;

using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Raster;

/// <summary>
/// Deterministic headless software window renderer for CI test runners, benchmarks, and snapshot tests.
/// Delegates directly to <see cref="GlacierGraphicsRenderer"/> for zero-duplication CPU rasterization.
/// </summary>
public sealed class HeadlessWindowRenderer : IWindowRenderer
{
    private readonly GlacierGraphicsRenderer _inner;

    public int Width => _inner.Width;
    public int Height => _inner.Height;
    public long RenderedFrameCount => _inner.RenderedFrameCount;
    public LinearFramebuffer Framebuffer => _inner.Framebuffer;
    public IGraphicsCanvas Canvas => _inner.Canvas;

    public HeadlessWindowRenderer(int width = 1280, int height = 800)
    {
        _inner = new GlacierGraphicsRenderer(width, height);
    }

    public void BeginFrame() => _inner.BeginFrame();

    public void RenderTree(VisualNode root, float width, float height) => _inner.RenderTree(root, width, height);

    public void EndFrame() => _inner.EndFrame();

    public byte[] EncodeToPng() => _inner.EncodeToPng();

    public void Dispose() => _inner.Dispose();
}
