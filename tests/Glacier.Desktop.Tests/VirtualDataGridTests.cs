namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Grids;
using Glacier.Desktop.Interop;
using Glacier.Desktop.Windowing;
using Glacier.Polaris;
using Glacier.Polaris.Data;
using Xunit;

public class VirtualDataGridTests
{
    [Fact]
    public void VirtualDataGrid_RendersOnlyVisibleRows()
    {
        using var renderer = new HeadlessWindowRenderer(800, 600);

        const int totalRows = 100000;
        var df = PolarisGridBridge.CreateSyntheticBenchmarkFrame(totalRows);

        var grid = new VirtualDataGrid
        {
            SourceDataFrame = df,
            Width = 800f,
            Height = 600f,
            RowHeight = 25f,
            HeaderHeight = 30f
        };

        renderer.BeginFrame();
        renderer.RenderTree(grid, 800f, 600f);
        renderer.EndFrame();

        Assert.Equal(totalRows, grid.TotalRowCount);
        // Visible row count for (600 - 30) / 25 ~ 23 rows + 1
        Assert.True(grid.VisibleRowCount < 30);
        Assert.Equal(1, renderer.RenderedFrameCount);
    }

    [Fact]
    public void VirtualDataGrid_ScrollToRow_UpdatesOffset()
    {
        var df = PolarisGridBridge.CreateSyntheticBenchmarkFrame(1000);
        var grid = new VirtualDataGrid
        {
            SourceDataFrame = df,
            RowHeight = 20f
        };

        grid.ScrollToRow(50);
        Assert.Equal(1000f, grid.ScrollOffsetY); // 50 * 20
    }

    [Fact]
    public void VirtualDataGrid_HandleMouseDown_SelectsRow()
    {
        var df = PolarisGridBridge.CreateSyntheticBenchmarkFrame(1000);
        var grid = new VirtualDataGrid
        {
            SourceDataFrame = df,
            Width = 800f,
            Height = 600f,
            RowHeight = 25f,
            HeaderHeight = 30f
        };
        grid.Measure(800f, 600f);
        grid.Arrange(new Layout.LayoutBox(800f, 600f, 0f, 0f));

        long selected = -1;
        grid.OnRowSelected = r => selected = r;

        // Click at X=100, Y=80 (which is inside row #2: Y from 30 + 2*25 = 80 to 105)
        bool handled = grid.HandleMouseDown(100f, 85f);

        Assert.True(handled);
        Assert.Equal(2, grid.SelectedRowIndex);
        Assert.Equal(2, selected);
    }

    [Fact]
    public void VirtualDataGrid_ScrollBy_ClampsWithinBounds()
    {
        var df = PolarisGridBridge.CreateSyntheticBenchmarkFrame(100);
        var grid = new VirtualDataGrid
        {
            SourceDataFrame = df,
            Width = 800f,
            Height = 600f,
            RowHeight = 25f,
            HeaderHeight = 30f
        };
        grid.Measure(800f, 600f);
        grid.Arrange(new Layout.LayoutBox(800f, 600f, 0f, 0f));

        grid.ScrollBy(-100f);
        Assert.Equal(0f, grid.ScrollOffsetY); // Cannot scroll above 0

        grid.ScrollBy(99999f);
        Assert.Equal(grid.MaxScrollOffsetY, grid.ScrollOffsetY); // Cannot exceed max
    }

    [Fact]
    public void VirtualDataGrid_RendersMultipleDataTypes_Successfully()
    {
        using var renderer = new HeadlessWindowRenderer(800, 600);

        var intSeries = new Int32Series("IntCol", 10);
        var floatSeries = new Float32Series("FloatCol", 10);
        var catSeries = CategoricalSeries.FromStrings("CatCol", ["Alpha", "Beta", "Gamma", "Alpha", "Beta", "Gamma", "Alpha", "Beta", "Gamma", "Alpha"]);

        for (int i = 0; i < 10; i++)
        {
            intSeries[i] = i * 10;
            floatSeries[i] = i * 1.25f;
        }

        var df = new DataFrame([intSeries, floatSeries, catSeries]);
        using var grid = new VirtualDataGrid
        {
            SourceDataFrame = df,
            Width = 800f,
            Height = 600f,
            RowHeight = 25f,
            HeaderHeight = 30f
        };

        renderer.BeginFrame();
        renderer.RenderTree(grid, 800f, 600f);
        renderer.EndFrame();

        Assert.Equal(10, grid.TotalRowCount);
        Assert.Equal(1, renderer.RenderedFrameCount);
    }
}
