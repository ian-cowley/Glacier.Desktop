namespace Glacier.Desktop.UI.Controls;

using System;
using System.Collections.Generic;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using SkiaSharp;

/// <summary>
/// Top-level desktop menu bar.
/// </summary>
public class MenuBar : FlexRow
{
    public MenuBar() : base(spacing: 4f)
    {
        BackgroundColor = Color4.HeaderBackground;
        Padding = new Thickness(8f, 4f);
        Height = 28f;
    }
}

/// <summary>
/// Menu item entry in a desktop menu bar or drop-down.
/// </summary>
public class MenuItem : VisualNode
{
    public string Header { get; set; } = string.Empty;
    public Action? OnClick { get; set; }

    public MenuItem(string header, Action? onClick = null)
    {
        Header = header;
        OnClick = onClick;
        Padding = new Thickness(8f, 2f);
    }

    public void PerformClick() => OnClick?.Invoke();

    public override void Measure(float availableWidth, float availableHeight)
    {
        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 12f,
            IsAntialias = true
        };

        float w = paint.MeasureText(Header);
        Bounds.DesiredWidth = w + Padding.Horizontal;
        Bounds.DesiredHeight = 22f;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible || string.IsNullOrEmpty(Header)) return;
        base.Render(canvas);

        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 12f,
            Color = Color4.White.ToSKColor(),
            IsAntialias = true
        };

        canvas.DrawText(Header, Bounds.ActualX + Padding.Left, Bounds.ActualY + 16f, paint);
    }
}
