namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using Glacier.Desktop.UI.Controls;
using Glacier.Desktop.Windowing;
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
}
