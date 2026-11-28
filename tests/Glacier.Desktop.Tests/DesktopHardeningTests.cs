namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI.Containers;
using Xunit;

public sealed class DesktopHardeningTests
{
    [Fact]
    public void FlexRow_SkipsInvisibleChildren_AndHandlesLargeChildCountWithoutStackOverflow()
    {
        var row = new FlexRow(spacing: 10f);
        var hiddenFirst = new Panel { Width = 50f, Height = 20f, IsVisible = false };
        var visibleSecond = new Panel { Width = 30f, Height = 20f, IsVisible = true };
        var visibleThird = new Panel { Width = 40f, Height = 25f, IsVisible = true };

        row.Add(hiddenFirst);
        row.Add(visibleSecond);
        row.Add(visibleThird);

        row.Measure(500f, 100f);
        Assert.Equal(30f + 10f + 40f, row.Bounds.DesiredWidth, 3);
        Assert.Equal(25f, row.Bounds.DesiredHeight, 3);

        row.Arrange(new LayoutBox(500f, 100f, 0f, 0f));
        Assert.Equal(0f, visibleSecond.Bounds.ActualX, 3);
        Assert.Equal(40f, visibleThird.Bounds.ActualX, 3);

        // >128 children triggers the ArrayPool fallback instead of stackalloc
        var largeRow = new FlexRow(spacing: 2f);
        var items = new Panel[1000];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Panel { Width = 10f, Height = 10f };
            largeRow.Add(items[i]);
        }

        largeRow.Measure(20000f, 100f);
        largeRow.Arrange(new LayoutBox(20000f, 100f, 5f, 12f));

        Assert.Equal(5f, items[0].Bounds.ActualX, 3);
        Assert.Equal(5f + 500f * 12f, items[500].Bounds.ActualX, 2);
        Assert.Equal(5f + 999f * 12f, items[999].Bounds.ActualX, 2);
    }

    [Fact]
    public void FlexColumn_SkipsInvisibleChildren_AndArranges500ChildrenWithSimd()
    {
        var col = new FlexColumn(spacing: 8f);
        var hiddenFirst = new Panel { Width = 20f, Height = 40f, IsVisible = false };
        var visibleSecond = new Panel { Width = 25f, Height = 15f, IsVisible = true };
        var visibleThird = new Panel { Width = 30f, Height = 25f, IsVisible = true };

        col.Add(hiddenFirst);
        col.Add(visibleSecond);
        col.Add(visibleThird);

        col.Measure(200f, 500f);
        Assert.Equal(15f + 8f + 25f, col.Bounds.DesiredHeight, 3);
        Assert.Equal(30f, col.Bounds.DesiredWidth, 3);

        col.Arrange(new LayoutBox(200f, 500f, 10f, 20f));
        Assert.Equal(20f, visibleSecond.Bounds.ActualY, 3);
        Assert.Equal(20f + 15f + 8f, visibleThird.Bounds.ActualY, 3);

        var largeCol = new FlexColumn(spacing: 4f);
        var items = new Panel[500];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Panel { Width = 50f, Height = 12f };
            largeCol.Add(items[i]);
        }

        largeCol.Measure(200f, 10000f);
        largeCol.Arrange(new LayoutBox(200f, 10000f, 0f, 3f));

        Assert.Equal(3f, items[0].Bounds.ActualY, 3);
        Assert.Equal(3f + 499f * 16f, items[499].Bounds.ActualY, 2);
    }

    [Fact]
    public void DockPanel_ClampsNonNegativeDimensions_AndReleasesRemovedChildren()
    {
        var dock = new DockPanel();
        var top = new Panel { Width = 100f, Height = 30f };
        var bottom = new Panel { Width = 100f, Height = 30f };
        var fill = new Panel();

        dock.Add(top, DockPosition.Top);
        dock.Add(bottom, DockPosition.Bottom);
        dock.Add(fill, DockPosition.Fill);

        dock.Measure(40f, 40f);
        dock.Arrange(new LayoutBox(40f, 40f, 0f, 0f));

        Assert.True(top.Bounds.DesiredHeight >= 0f);
        Assert.True(bottom.Bounds.DesiredHeight >= 0f);
        Assert.True(fill.Bounds.DesiredWidth >= 0f);
        Assert.True(fill.Bounds.DesiredHeight >= 0f);
        Assert.Equal(0f, fill.Bounds.DesiredHeight, 3);

        Assert.True(dock.Remove(top));
        Assert.Null(top.Parent);
        dock.Clear();
        Assert.Empty(dock.Children);
    }
}
