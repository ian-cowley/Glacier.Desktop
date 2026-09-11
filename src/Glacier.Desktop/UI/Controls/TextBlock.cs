namespace Glacier.Desktop.UI.Controls;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using SkiaSharp;

/// <summary>
/// Text block control for high-performance sub-pixel text rendering.
/// </summary>
public class TextBlock : VisualNode
{
    private string _text;
    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public float FontSize { get; set; } = 14f;
    public Color4 TextColor { get; set; } = Color4.White;
    public bool IsBold { get; set; } = false;

    public TextBlock(string text = "")
    {
        _text = text ?? string.Empty;
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = FontSize,
            IsAntialias = true
        };

        var bounds = new SKRect();
        paint.MeasureText(Text, ref bounds);

        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : bounds.Width + Margin.Horizontal + Padding.Horizontal;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : FontSize * 1.35f + Margin.Vertical + Padding.Vertical;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible || string.IsNullOrEmpty(Text)) return;
        base.Render(canvas);

        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = FontSize,
            Color = TextColor.ToSKColor(),
            IsAntialias = true
        };

        float textX = Bounds.ActualX + Padding.Left;
        float textY = Bounds.ActualY + Padding.Top + FontSize;
        canvas.DrawText(Text, textX, textY, paint);
    }
}
