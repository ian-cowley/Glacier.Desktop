namespace Glacier.Desktop.UI.Controls;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using SkiaSharp;

/// <summary>
/// Hardware-accelerated desktop button supporting click events, borders, and rounded corners.
/// </summary>
public class DesktopButton : VisualNode
{
    public string Text { get; set; } = "Button";
    public float FontSize { get; set; } = 13f;
    public Color4 TextColor { get; set; } = Color4.White;
    public Color4 BorderColor { get; set; } = Color4.BorderColor;
    public float CornerRadius { get; set; } = 6f;
    public Action? OnClick { get; set; }

    public DesktopButton(string text = "Button", Action? onClick = null)
    {
        Text = text;
        OnClick = onClick;
        BackgroundColor = Color4.PanelBackground;
        Padding = new Thickness(14f, 6f);
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = FontSize,
            IsAntialias = true
        };

        float textW = paint.MeasureText(Text);
        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : textW + Padding.Horizontal + Margin.Horizontal;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : FontSize + Padding.Vertical + Margin.Vertical + 10f;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor.ToSKColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var rect = new SKRoundRect(new SKRect(Bounds.ActualX, Bounds.ActualY, Bounds.ActualX + Bounds.DesiredWidth, Bounds.ActualY + Bounds.DesiredHeight), CornerRadius);
        canvas.DrawRoundRect(rect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor.ToSKColor(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, borderPaint);

        // Draw text
        if (!string.IsNullOrEmpty(Text))
        {
            using var textPaint = new SKPaint
            {
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                TextSize = FontSize,
                Color = TextColor.ToSKColor(),
                IsAntialias = true
            };

            float textW = textPaint.MeasureText(Text);
            float textX = Bounds.ActualX + (Bounds.DesiredWidth - textW) * 0.5f;
            float textY = Bounds.ActualY + (Bounds.DesiredHeight + FontSize * 0.75f) * 0.5f;
            canvas.DrawText(Text, textX, textY, textPaint);
        }
    }

    public void PerformClick()
    {
        OnClick?.Invoke();
    }
}
