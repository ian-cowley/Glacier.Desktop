namespace Glacier.Desktop.UI.Containers;

using System;
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

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            child.Measure(availableWidth, availableHeight);
            totalW += child.Bounds.DesiredWidth + (i > 0 ? Spacing : 0f);
            maxH = MathF.Max(maxH, child.Bounds.DesiredHeight);
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

        int count = _children.Count;
        if (count == 0) return;

        // Use high-performance SoA layout arrange buffers
        Span<float> widths = stackalloc float[count];
        Span<float> actualXs = stackalloc float[count];
        for (int i = 0; i < count; i++)
        {
            widths[i] = _children[i].Bounds.DesiredWidth;
        }

        float startX = Bounds.ActualX + Padding.Left;
        LayoutKernels.ArrangeHorizontalRowSoA(widths, actualXs, startX, Spacing);

        float curY = Bounds.ActualY + Padding.Top;
        for (int i = 0; i < count; i++)
        {
            var b = _children[i].Bounds;
            b.ActualX = actualXs[i];
            b.ActualY = curY;
            _children[i].Arrange(b);
        }
    }
}
