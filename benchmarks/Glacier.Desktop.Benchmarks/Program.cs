namespace Glacier.Desktop.Benchmarks;

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Glacier.Desktop.Grids;
using Glacier.Desktop.Interop;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using Glacier.Desktop.UI.Controls;
using Glacier.Desktop.Windowing;
using Glacier.Polaris;
using SkiaSharp;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<DesktopBenchmarks>();
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class DesktopBenchmarks
{
    private const int BoxCount = 10000;
    private LayoutBox[] _boxes = null!;
    private LayoutBox[] _scalarBoxes = null!;

    private DataFrame _polarisFrame = null!;
    private VirtualDataGrid _dataGrid = null!;
    private SKSurface _surface = null!;
    private SKCanvas _canvas = null!;

    private GlacierWindow _window = null!;

    [GlobalSetup]
    public void Setup()
    {
        _boxes = new LayoutBox[BoxCount];
        _scalarBoxes = new LayoutBox[BoxCount];
        for (int i = 0; i < BoxCount; i++)
        {
            _boxes[i] = new LayoutBox(60f + (i % 50), 28f, 0f, 0f);
            _scalarBoxes[i] = _boxes[i];
        }

        _polarisFrame = PolarisGridBridge.CreateSyntheticBenchmarkFrame(1_000_000);
        _dataGrid = new VirtualDataGrid
        {
            SourceDataFrame = _polarisFrame,
            Width = 1200f,
            Height = 700f
        };
        _dataGrid.Measure(1200f, 700f);
        _dataGrid.Arrange(new LayoutBox(1200f, 700f, 0f, 0f));

        var imageInfo = new SKImageInfo(1280, 800, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(imageInfo);
        _canvas = _surface.Canvas;

        // Visual tree layout with menus, panels, buttons, and status bar
        var dock = new DockPanel();
        var menu = new MenuBar();
        menu.Add(new MenuItem("File"));
        menu.Add(new MenuItem("Edit"));
        menu.Add(new MenuItem("View"));
        menu.Add(new MenuItem("Help"));
        dock.Add(menu, DockPosition.Top);

        var statusBar = new StatusBar("Ready - 1,000,000 Records Loaded");
        dock.Add(statusBar, DockPosition.Bottom);

        var row = new FlexRow(spacing: 8f);
        for (int i = 0; i < 20; i++)
        {
            row.Add(new DesktopButton($"Action {i + 1}") { Width = 90f, Height = 30f });
        }
        dock.Add(row, DockPosition.Top);

        var centralPanel = new Panel { BackgroundColor = Color4.DarkBackground };
        dock.Add(centralPanel, DockPosition.Fill);

        _window = new GlacierWindow("Benchmark Window", 1280, 800)
        {
            RootVisual = dock
        };
        _window.Show();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _surface.Dispose();
        _window.Dispose();
    }

    [Benchmark(Description = "AVX-512/AVX2 Kogge-Stone Row Arrange (10,000 Boxes)")]
    public void Benchmark_KoggeStone_SIMD()
    {
        LayoutKernels.ArrangeHorizontalRow(_boxes.AsSpan(), 0f, 5f);
    }

    [Benchmark(Baseline = true, Description = "Scalar Row Arrange Baseline (10,000 Boxes)")]
    public void Benchmark_ScalarRowArrange()
    {
        float curX = 0f;
        for (int i = 0; i < _scalarBoxes.Length; i++)
        {
            _scalarBoxes[i].ActualX = curX;
            curX += _scalarBoxes[i].DesiredWidth + 5f;
        }
    }

    [Benchmark(Description = "VirtualDataGrid 1M Row Viewport Slicing & Skia Render")]
    public void Benchmark_VirtualGrid_Render()
    {
        _dataGrid.ScrollOffsetY += 28f;
        if (_dataGrid.ScrollOffsetY > 28000f) _dataGrid.ScrollOffsetY = 0f;
        _dataGrid.Render(_canvas);
    }

    [Benchmark(Description = "Full Desktop Visual Tree Measure, Arrange, and Frame Render")]
    public void Benchmark_VisualTree_Frame()
    {
        _window.Step(0.016f);
    }
}
