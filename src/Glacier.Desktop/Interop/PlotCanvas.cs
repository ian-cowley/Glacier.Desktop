namespace Glacier.Desktop.Interop;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Plot.Figures;
using SkiaSharp;

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

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;
        base.Render(canvas);

        canvas.Save();
        canvas.ClipRect(new SKRect(Bounds.ActualX, Bounds.ActualY, Bounds.ActualX + Bounds.DesiredWidth, Bounds.ActualY + Bounds.DesiredHeight));
        canvas.Translate(Bounds.ActualX, Bounds.ActualY);

        Figure.Render(canvas, (int)Bounds.DesiredWidth, (int)Bounds.DesiredHeight);

        canvas.Restore();
    }
}
