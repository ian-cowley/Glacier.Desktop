namespace Glacier.Desktop.UI.Containers;

using System;
using System.Buffers;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;

/// <summary>
/// SIMD-accelerated vertical column container arranging visible children sequentially with uniform spacing.
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
        int visibleCount = 0;

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            child.Measure(availableWidth, availableHeight);
            maxW = MathF.Max(maxW, child.Bounds.DesiredWidth);
            if (visibleCount > 0) totalH += Spacing;
            totalH += child.Bounds.DesiredHeight;
            visibleCount++;
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

        int visibleCount = 0;
        for (int i = 0; i < _children.Count; i++)
        {
            if (_children[i].IsVisible) visibleCount++;
        }
        if (visibleCount == 0) return;

        float curX = Bounds.ActualX + Padding.Left;
        float startY = Bounds.ActualY + Padding.Top;
        float innerW = MathF.Max(0f, Bounds.DesiredWidth - Padding.Horizontal);

        float[]? rentedHeights = null;
        float[]? rentedActualYs = null;
        try
        {
            Span<float> heights = visibleCount <= 128
                ? stackalloc float[visibleCount]
                : (rentedHeights = ArrayPool<float>.Shared.Rent(visibleCount)).AsSpan(0, visibleCount);
            Span<float> actualYs = visibleCount <= 128
                ? stackalloc float[visibleCount]
                : (rentedActualYs = ArrayPool<float>.Shared.Rent(visibleCount)).AsSpan(0, visibleCount);

            int vIdx = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (!child.IsVisible) continue;
                heights[vIdx++] = child.Bounds.DesiredHeight;
            }

            LayoutKernels.ArrangeHorizontalRowSoA(heights, actualYs, startY, Spacing);

            vIdx = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (!child.IsVisible) continue;

                var childBox = new LayoutBox(
                    !float.IsNaN(child.Width) && child.Width > 0 ? child.Width : innerW,
                    child.Bounds.DesiredHeight,
                    curX,
                    actualYs[vIdx++]
                );

                child.Arrange(childBox);
            }
        }
        finally
        {
            if (rentedHeights is not null) ArrayPool<float>.Shared.Return(rentedHeights);
            if (rentedActualYs is not null) ArrayPool<float>.Shared.Return(rentedActualYs);
        }
    }
}
