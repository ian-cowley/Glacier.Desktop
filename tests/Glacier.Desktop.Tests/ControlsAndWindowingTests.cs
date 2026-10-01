namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using Glacier.Desktop.UI.Controls;
using Glacier.Desktop.Windowing;
using Glacier.Graphics;
using Glacier.Graphics.Raster;
using Glacier.Windowing;
using Xunit;

public class ControlsAndWindowingTests
{
    [Fact]
    public void TextBlock_MeasuresAndRenders()
    {
        using var renderer = new HeadlessWindowRenderer(600, 400);
        var tb = new TextBlock("Sample Desktop Text") { FontSize = 16f, IsBold = true };

        renderer.BeginFrame();
        renderer.RenderTree(tb, 600f, 400f);
        renderer.EndFrame();

        Assert.True(tb.Bounds.DesiredWidth > 0f);
        Assert.True(tb.Bounds.DesiredHeight > 0f);
        Assert.Equal(1, renderer.RenderedFrameCount);

        byte[] png = renderer.EncodeToPng();
        Assert.NotEmpty(png);
    }

    [Fact]
    public void DesktopButton_Click_InvokesAction()
    {
        bool clicked = false;
        var btn = new DesktopButton("Save", onClick: () => clicked = true);

        btn.PerformClick();
        Assert.True(clicked);
    }

    [Fact]
    public void GlacierWindow_Lifecycle_ShowStepClose()
    {
        using var window = new GlacierWindow("Test App", 800, 600);
        Assert.False(window.IsOpen);

        window.RootVisual = new DockPanel();
        window.Show();
        Assert.True(window.IsOpen);

        for (int i = 0; i < 5; i++)
        {
            window.Step(1.0f / 120.0f);
        }
        Assert.Equal(5, window.FrameCount);

        window.Close();
        Assert.False(window.IsOpen);
    }

    [Fact]
    public void MenuBar_AddMenu_And_DropDown_Lifecycle()
    {
        var menuBar = new MenuBar();
        var fileMenu = menuBar.AddMenu("File");
        bool newClicked = false;
        fileMenu.Add("New", () => newClicked = true, "Ctrl+N");
        fileMenu.AddSeparator();
        fileMenu.Add("Exit");

        Assert.Single(menuBar.Children);
        Assert.True(fileMenu.HasChildren);
        Assert.Equal(3, fileMenu.Items.Count);

        // Initially closed
        Assert.Null(menuBar.OpenMenu);
        Assert.Null(menuBar.ActiveDropDown);

        // Toggle open
        fileMenu.PerformClick();
        Assert.Same(fileMenu, menuBar.OpenMenu);
        Assert.NotNull(menuBar.ActiveDropDown);

        // Hit test inside dropdown
        var drop = menuBar.ActiveDropDown;
        drop.UpdateLayout();
        Assert.True(drop.Bounds.DesiredHeight > 50f);

        // Click on first item
        var item0 = fileMenu.Items[0];
        item0.PerformClick();
        Assert.True(newClicked);
        Assert.Null(menuBar.OpenMenu); // Closes menu
    }

    [Fact]
    public void GlacierGraphicsRenderer_RendersAndEncodesPng()
    {
        using var renderer = new GlacierGraphicsRenderer(400, 300);
        var panel = new Panel { BackgroundColor = Color4.DarkBackground };
        var button = new DesktopButton("OK") { BackgroundColor = Color4.GlacierBlue };
        panel.Add(button);

        renderer.BeginFrame();
        renderer.RenderTree(panel, 400, 300);
        renderer.EndFrame();

        Assert.Equal(1, renderer.RenderedFrameCount);
        byte[] png = renderer.EncodeToPng();
        Assert.NotNull(png);
        Assert.True(png.Length > 64);
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);
    }

    [Fact]
    public void GlacierDesktopWindowFactory_CreatesHeadlessWindow()
    {
        using var win = GlacierDesktopWindowFactory.CreateHeadlessWindow(800, 600);
        Assert.NotNull(win);
        Assert.Equal(800, win.Size.Width);
        Assert.Equal(600, win.Size.Height);
    }

    [Fact]
    public void VisualNode_Render_DirectlyTargetingIGraphicsCanvas_WithoutSkiaSharp()
    {
        using var fb = new LinearFramebuffer(500, 300);
        using var canvas = new CpuGraphicsCanvas(fb);
        var panel = new Panel { BackgroundColor = Color4.DarkBackground };
        var btn = new DesktopButton("Pure C#") { BackgroundColor = Color4.GlacierBlue };
        var text = new TextBlock("Sub-pixel Text") { TextColor = Color4.White };
        panel.Add(btn);
        panel.Add(text);

        panel.Measure(500, 300);
        panel.Arrange(new LayoutBox(500, 300, 0, 0));

        // Directly verify VisualNode.Render accepts IGraphicsCanvas
        IGraphicsCanvas gCanvas = canvas;
        panel.Render(gCanvas);
        gCanvas.Flush();

        // Verify non-zero bytes in framebuffer
        var pixels = fb.AsByteSpan();
        bool hasNonZero = false;
        foreach (byte b in pixels)
        {
            if (b > 0)
            {
                hasNonZero = true;
                break;
            }
        }
        Assert.True(hasNonZero);
    }

    [Fact]
    public void GlacierWindow_NativeWindowInputLoop_EliminatesPollingJitter()
    {
        using var nativeWin = GlacierDesktopWindowFactory.CreateHeadlessWindow(640, 480);
        using var window = new GlacierWindow("Native Input App", 640, 480, nativeWindow: nativeWin);

        bool btnClicked = false;
        var btn = new DesktopButton("Submit", onClick: () => btnClicked = true)
        {
            Width = 100,
            Height = 30
        };
        btn.Bounds = new LayoutBox(100, 30, 10, 10);
        window.RootVisual = btn;

        Assert.Equal(0ul, window.InputDispatchedCount);

        // Native hardware input received directly without OS polling jitter
        var inputEvt = new InputEvent(InputEventType.MouseDown, 1, 20f, 20f, 1000000UL);
        if (nativeWin is Glacier.Windowing.Platform.Headless.HeadlessWindow hw)
        {
            hw.EnqueueInput(inputEvt);
        }

        window.Step(0.016f);

        // Verify event handling via window input loop
        Assert.NotNull(window.NativeWindow);
        Assert.Equal(1, window.FrameCount);
        Assert.Equal(1ul, window.InputDispatchedCount);
        Assert.True(btnClicked);
    }
}

