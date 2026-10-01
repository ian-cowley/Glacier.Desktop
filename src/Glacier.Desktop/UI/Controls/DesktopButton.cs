namespace Glacier.Desktop.UI.Controls;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Graphics;
using Glacier.Graphics.Text;
using Glacier.Graphics.Vector;

/// <summary>
/// Hardware-accelerated desktop button supporting click events, borders, and rounded corners.
/// </summary>
public class DesktopButton : VisualNode
{
    public string Text { get; set; } = "Button";
    public float FontSize { get; set; } = 13f;
    public Color4 TextColor { get; set; } = Color4.White;
    public Color4 BorderColor { get; set; } = Color4.BorderColor;
    public Color4 HoverBackgroundColor { get; set; } = new Color4(45, 55, 75, 255);
    public Color4 HoverBorderColor { get; set; } = Color4.GlacierBlue;
    public Color4 PressedBackgroundColor { get; set; } = new Color4(30, 70, 110, 255);
    public float CornerRadius { get; set; } = 6f;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
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
        var font = new Font(FontSize, bold: true);
        float textW = font.MeasureText(Text.AsSpan());
        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : textW + Padding.Horizontal + Margin.Horizontal;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : FontSize + Padding.Vertical + Margin.Vertical + 10f;
    }

    public override void Render(IGraphicsCanvas canvas)
    {
        if (!IsVisible) return;

        Color4 currentBg = IsPressed ? PressedBackgroundColor : (IsHovered ? HoverBackgroundColor : BackgroundColor);
        Color4 currentBorder = (IsHovered || IsPressed) ? HoverBorderColor : BorderColor;

        var rectPath = new VectorPath();
        rectPath.AddRect(Bounds.ActualX, Bounds.ActualY, Bounds.DesiredWidth, Bounds.DesiredHeight);

        // Draw background
        canvas.FillPath(rectPath, new Paint(currentBg.ToRgba32(), PaintStyle.Fill));

        // Draw border
        float strokeWidth = IsHovered ? 1.5f : 1f;
        canvas.DrawPath(rectPath, new Paint(currentBorder.ToRgba32(), PaintStyle.Stroke, strokeWidth));

        // Draw text
        if (!string.IsNullOrEmpty(Text))
        {
            var font = new Font(FontSize, bold: true);
            var textPaint = new Paint(TextColor.ToRgba32(), PaintStyle.Fill);

            float textW = font.MeasureText(Text.AsSpan());
            float textX = Bounds.ActualX + (Bounds.DesiredWidth - textW) * 0.5f;
            float textY = Bounds.ActualY + (Bounds.DesiredHeight + FontSize * 0.75f) * 0.5f;
            canvas.DrawText(Text.AsSpan(), textX, textY, font, textPaint);
        }
    }

    public void PerformClick()
    {
        OnClick?.Invoke();
    }
}
