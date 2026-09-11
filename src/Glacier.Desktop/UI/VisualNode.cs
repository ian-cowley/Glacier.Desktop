namespace Glacier.Desktop.UI;

using System;
using System.Collections.Generic;
using Glacier.Desktop.Layout;
using SkiaSharp;

/// <summary>
/// Retained visual element in the Glacier.Desktop UI visual tree.
/// </summary>
public abstract class VisualNode
{
    public LayoutBox Bounds;
    public Thickness Margin { get; set; } = Thickness.Zero;
    public Thickness Padding { get; set; } = Thickness.Zero;
    public Color4 BackgroundColor { get; set; } = Color4.Transparent;
    public bool IsVisible { get; set; } = true;
    public VisualNode? Parent { get; internal set; }

    protected float _explicitWidth = float.NaN;
    protected float _explicitHeight = float.NaN;

    public float Width
    {
        get => _explicitWidth;
        set
        {
            _explicitWidth = value;
            Bounds.DesiredWidth = value;
        }
    }

    public float Height
    {
        get => _explicitHeight;
        set
        {
            _explicitHeight = value;
            Bounds.DesiredHeight = value;
        }
    }

    public float ActualX => Bounds.ActualX;
    public float ActualY => Bounds.ActualY;

    public virtual void Measure(float availableWidth, float availableHeight)
    {
        float w = !float.IsNaN(_explicitWidth) && _explicitWidth > 0 ? _explicitWidth : availableWidth - Margin.Horizontal;
        float h = !float.IsNaN(_explicitHeight) && _explicitHeight > 0 ? _explicitHeight : availableHeight - Margin.Vertical;
        Bounds.DesiredWidth = MathF.Max(0f, w);
        Bounds.DesiredHeight = MathF.Max(0f, h);
    }

    public virtual void Arrange(in LayoutBox finalBounds)
    {
        Bounds.ActualX = finalBounds.ActualX + Margin.Left;
        Bounds.ActualY = finalBounds.ActualY + Margin.Top;
        Bounds.DesiredWidth = !float.IsNaN(_explicitWidth) && _explicitWidth > 0 ? _explicitWidth : finalBounds.DesiredWidth;
        Bounds.DesiredHeight = !float.IsNaN(_explicitHeight) && _explicitHeight > 0 ? _explicitHeight : finalBounds.DesiredHeight;
    }

    public virtual void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        if (BackgroundColor.A > 0)
        {
            using var paint = new SKPaint { Color = BackgroundColor.ToSKColor(), Style = SKPaintStyle.Fill };
            canvas.DrawRect(Bounds.ActualX, Bounds.ActualY, Bounds.DesiredWidth, Bounds.DesiredHeight, paint);
        }
    }

    public virtual VisualNode? HitTest(float px, float py)
    {
        if (!IsVisible) return null;
        if (px >= Bounds.ActualX && px <= Bounds.ActualX + Bounds.DesiredWidth &&
            py >= Bounds.ActualY && py <= Bounds.ActualY + Bounds.DesiredHeight)
        {
            return this;
        }
        return null;
    }
}
