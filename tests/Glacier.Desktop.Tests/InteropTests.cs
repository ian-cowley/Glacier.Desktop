namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Interop;
using Glacier.Desktop.Windowing;
using Glacier.Plot.Figures;
using Xunit;

public class InteropTests
{
    [Fact]
    public void PlotCanvas_RendersDesktopFigure()
    {
        using var renderer = new HeadlessWindowRenderer(800, 500);

        var figure = new Figure { Title = "Desktop Performance" };
        var yData = new float[] { 50f, 65f, 55f, 90f, 85f, 100f };
        figure.PlotSignal(yData, label: "Throughput (kFPS)");

        var canvas = new PlotCanvas(figure)
        {
            Width = 780f,
            Height = 460f
        };

        renderer.BeginFrame();
        renderer.RenderTree(canvas, 800f, 500f);
        renderer.EndFrame();

        Assert.Equal(1, renderer.RenderedFrameCount);
    }
}
