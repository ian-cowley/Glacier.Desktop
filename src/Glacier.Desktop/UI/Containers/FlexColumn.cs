namespace Glacier.Desktop.UI.Containers;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;

/// <summary>
/// Vertical column container arranging children sequentially with uniform spacing.
/// </summary>
public class FlexColumn : Panel
{
    public float Spacing { get; set; } = 8f;

    public FlexColumn(float spacing = 8f)
    {
        Spacing = spacing;
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        float maxW = 0f;
        float totalH = 0f;

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            child.Measure(availableWidth, availableHeight);
            maxW = MathF.Max(maxW, child.Bounds.DesiredWidth);
            totalH += child.Bounds.DesiredHeight + (i > 0 ? Spacing : 0f);
        }

        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : maxW + Padding.Horizontal;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : totalH + Padding.Vertical;
    }

    public override void Arrange(in LayoutBox finalBounds)
    {
        Bounds.ActualX = finalBounds.ActualX + Margin.Left;
        Bounds.ActualY = finalBounds.ActualY + Margin.Top;
        Bounds.DesiredWidth = !float.IsNaN(_explicitWidth) && _explicitWidth > 0 ? _explicitWidth : finalBounds.DesiredWidth;
        Bounds.DesiredHeight = !float.IsNaN(_explicitHeight) && _explicitHeight > 0 ? _explicitHeight : finalBounds.DesiredHeight;

        float curX = Bounds.ActualX + Padding.Left;
        float curY = Bounds.ActualY + Padding.Top;
        float innerW = MathF.Max(0f, Bounds.DesiredWidth - Padding.Horizontal);

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            var childBox = new LayoutBox(
                !float.IsNaN(child.Width) && child.Width > 0 ? child.Width : innerW,
                child.Bounds.DesiredHeight,
                curX,
                curY
            );

            child.Arrange(childBox);
            curY += child.Bounds.DesiredHeight + Spacing;
        }
    }
}
