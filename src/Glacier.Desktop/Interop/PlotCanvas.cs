namespace Glacier.Desktop.Interop;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Raster;
using Glacier.Graphics.Vector;
using Glacier.Plot.Figures;

/// <summary>
/// Hardware-accelerated desktop visual node hosting a Glacier.Plot Figure.
/// </summary>
public sealed class PlotCanvas : VisualNode
{
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
        if (!IsVisible) return;
        base.Render(canvas);

        int w = (int)MathF.Max(1f, Bounds.DesiredWidth);
        int h = (int)MathF.Max(1f, Bounds.DesiredHeight);

        using var fb = new LinearFramebuffer(w, h);
        using var figureCanvas = new CpuGraphicsCanvas(fb);
        Figure.Render(figureCanvas, w, h);

        canvas.DrawImage(fb.AsReadOnlySpan2D(), Bounds.ActualX, Bounds.ActualY, Bounds.DesiredWidth, Bounds.DesiredHeight);
    }
}
