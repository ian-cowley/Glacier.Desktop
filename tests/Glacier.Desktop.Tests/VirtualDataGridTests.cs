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
}
