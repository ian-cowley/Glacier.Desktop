namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using Glacier.Desktop.UI.Controls;
using Xunit;

public class VisualTreeAndContainerTests
{
    [Fact]
    public void FlexRow_ArrangesChildrenWithSIMD()
    {
        var row = new FlexRow(spacing: 10f)
        {
            new TextBlock("A") { Width = 60f, Height = 25f },
            new TextBlock("B") { Width = 80f, Height = 25f },
            new TextBlock("C") { Width = 100f, Height = 25f }
        };

        row.Measure(1000f, 600f);
        row.Arrange(new LayoutBox(1000f, 600f, 0f, 0f));

        Assert.Equal(3, row.Children.Count);
        Assert.Equal(0f, row.Children[0].ActualX);
        Assert.Equal(70f, row.Children[1].ActualX); // 60 + 10
        Assert.Equal(160f, row.Children[2].ActualX); // 70 + 80 + 10
    }

    [Fact]
    public void FlexColumn_ArrangesChildrenVertically()
    {
        var col = new FlexColumn(spacing: 8f)
        {
            new TextBlock("Row 1") { Width = 200f, Height = 30f },
            new TextBlock("Row 2") { Width = 200f, Height = 40f }
        };

        col.Measure(500f, 500f);
        col.Arrange(new LayoutBox(500f, 500f, 10f, 20f));

        Assert.Equal(2, col.Children.Count);
        Assert.Equal(20f, col.Children[0].ActualY);
        Assert.Equal(58f, col.Children[1].ActualY); // 20 + 30 + 8
    }

    [Fact]
    public void DockPanel_DocksChildrenCorrectly()
    {
        var dock = new DockPanel();
        var top = new TextBlock("Top") { Height = 40f };
        var bottom = new TextBlock("Bottom") { Height = 30f };
        var fill = new Panel();

        dock.Add(top, DockPosition.Top);
        dock.Add(bottom, DockPosition.Bottom);
        dock.Add(fill, DockPosition.Fill);

        dock.Measure(800f, 600f);
        dock.Arrange(new LayoutBox(800f, 600f, 0f, 0f));

        Assert.Equal(0f, top.ActualY);
        Assert.Equal(40f, top.Bounds.DesiredHeight);

        Assert.Equal(570f, bottom.ActualY); // 600 - 30
        Assert.Equal(30f, bottom.Bounds.DesiredHeight);

        Assert.Equal(40f, fill.ActualY); // Top offset
        Assert.Equal(530f, fill.Bounds.DesiredHeight); // 600 - 40 - 30
    }
}
