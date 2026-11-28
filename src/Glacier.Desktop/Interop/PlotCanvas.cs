namespace Glacier.Desktop.Interop;

using System;
using System.Threading;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Raster;
using Glacier.Plot.Figures;

/// <summary>
/// Software/CPU-rendered desktop visual node hosting a <see cref="Glacier.Plot.Figures.Figure"/>.
/// Caches its internal framebuffer and <see cref="CpuGraphicsCanvas"/> (including the 4 MiB <c>GlyphAtlas</c>)
/// across frames until dimensions change, and implements <see cref="IDisposable"/> for deterministic cleanup.
/// </summary>
public sealed class PlotCanvas : VisualNode, IDisposable
{
    private readonly Lock _renderLock = new();
    private LinearFramebuffer? _framebuffer;
    private CpuGraphicsCanvas? _figureCanvas;
    private int _cachedWidth;
    private int _cachedHeight;
    private bool _disposed;

    public Figure Figure { get; set; }

    public PlotCanvas(Figure? figure = null)
    {
        Figure = figure ?? new Figure();
        BackgroundColor = Color4.PanelBackground;
        Padding = new Thickness(8f);
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        base.Measure(availableWidth, availableHeight);
        float w = !float.IsNaN(Width) && Width > 0 ? Width : availableWidth;
        float h = !float.IsNaN(Height) && Height > 0 ? Height : availableHeight;
        Bounds.DesiredWidth = w;
        Bounds.DesiredHeight = h;
    }

    public override void Render(IGraphicsCanvas canvas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsVisible) return;
        base.Render(canvas);

        lock (_renderLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            int w = Math.Max(1, (int)MathF.Max(1f, Bounds.DesiredWidth));
            int h = Math.Max(1, (int)MathF.Max(1f, Bounds.DesiredHeight));

            if (_framebuffer is null || _figureCanvas is null || _cachedWidth != w || _cachedHeight != h)
            {
                _figureCanvas?.Dispose();
                _figureCanvas = null;
                _framebuffer?.Dispose();
                _framebuffer = null;

                _framebuffer = new LinearFramebuffer(w, h);
                try
                {
                    _figureCanvas = new CpuGraphicsCanvas(_framebuffer);
                }
                catch
                {
                    _framebuffer.Dispose();
                    _framebuffer = null;
                    throw;
                }

                _cachedWidth = w;
                _cachedHeight = h;
            }

            Figure.Render(_figureCanvas, w, h);

            canvas.DrawImage(_framebuffer.AsReadOnlySpan2D(), Bounds.ActualX, Bounds.ActualY, Bounds.DesiredWidth, Bounds.DesiredHeight);
        }
    }

    public void Dispose()
    {
        lock (_renderLock)
        {
            if (_disposed) return;
            _disposed = true;

            _figureCanvas?.Dispose();
            _figureCanvas = null;
            _framebuffer?.Dispose();
            _framebuffer = null;
            _cachedWidth = 0;
            _cachedHeight = 0;
        }
    }
}
