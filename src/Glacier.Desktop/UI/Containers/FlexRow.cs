namespace Glacier.Desktop.UI.Containers;

using System;
using System.Buffers;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;

/// <summary>
/// SIMD-accelerated horizontal row layout executing AVX-512/AVX2 parallel prefix scans.
/// </summary>
public class FlexRow : Panel
{
    public float Spacing { get; set; } = 8f;

    public FlexRow(float spacing = 8f)
    {
        Spacing = spacing;
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        float totalW = 0f;
        float maxH = 0f;
        int visibleCount = 0;

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            child.Measure(availableWidth, availableHeight);
            if (visibleCount > 0) totalW += Spacing;
            totalW += child.Bounds.DesiredWidth;
            maxH = MathF.Max(maxH, child.Bounds.DesiredHeight);
            visibleCount++;
        }

        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : totalW + Padding.Horizontal;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : maxH + Padding.Vertical;
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

        float[]? rentedWidths = null;
        float[]? rentedActualXs = null;
        try
        {
            Span<float> widths = visibleCount <= 128
                ? stackalloc float[visibleCount]
                : (rentedWidths = ArrayPool<float>.Shared.Rent(visibleCount)).AsSpan(0, visibleCount);
            Span<float> actualXs = visibleCount <= 128
                ? stackalloc float[visibleCount]
                : (rentedActualXs = ArrayPool<float>.Shared.Rent(visibleCount)).AsSpan(0, visibleCount);

            int vIdx = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (!child.IsVisible) continue;
                widths[vIdx++] = child.Bounds.DesiredWidth;
            }

            float startX = Bounds.ActualX + Padding.Left;
            LayoutKernels.ArrangeHorizontalRowSoA(widths, actualXs, startX, Spacing);

            float curY = Bounds.ActualY + Padding.Top;
            vIdx = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (!child.IsVisible) continue;
                var b = child.Bounds;
                b.ActualX = actualXs[vIdx++];
                b.ActualY = curY;
                child.Arrange(b);
            }
        }
        finally
        {
            if (rentedWidths is not null) ArrayPool<float>.Shared.Return(rentedWidths);
            if (rentedActualXs is not null) ArrayPool<float>.Shared.Return(rentedActualXs);
        }
    }
}
