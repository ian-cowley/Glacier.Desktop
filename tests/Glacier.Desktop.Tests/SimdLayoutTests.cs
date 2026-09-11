namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Layout;
using Xunit;

public class SimdLayoutTests
{
    [Fact]
    public void ArrangeHorizontalRow_MatchesAnalyticalPrefixSum()
    {
        const int count = 20; // Tests 16-element AVX-512 / 8-element AVX2 + remainder scalar tail
        var boxes = new LayoutBox[count];
        for (int i = 0; i < count; i++)
        {
            boxes[i] = new LayoutBox(desiredWidth: 50f, desiredHeight: 30f);
        }

        const float startX = 10f;
        const float spacing = 5f;

        LayoutKernels.ArrangeHorizontalRow(boxes, startX, spacing);

        float expectedX = startX;
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expectedX, boxes[i].ActualX, 2);
            expectedX += 50f + spacing;
        }
    }

    [Fact]
    public void ArrangeHorizontalRow_ClampsNegativeWidthsToZero()
    {
        var boxes = new LayoutBox[4]
        {
            new(-10f, 20f),
            new(50f, 20f),
            new(-5f, 20f),
            new(30f, 20f)
        };

        LayoutKernels.ArrangeHorizontalRow(boxes, startX: 0f, spacing: 10f);

        // Box 0: starts at 0, effective width 0 -> next starts at 0 + 0 + 10 = 10
        Assert.Equal(0f, boxes[0].ActualX);
        // Box 1: starts at 10, width 50 -> next starts at 10 + 50 + 10 = 70
        Assert.Equal(10f, boxes[1].ActualX);
        // Box 2: starts at 70, effective width 0 -> next starts at 70 + 0 + 10 = 80
        Assert.Equal(70f, boxes[2].ActualX);
        // Box 3: starts at 80
        Assert.Equal(80f, boxes[3].ActualX);
    }
}
