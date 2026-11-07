namespace Glacier.Desktop.Tests;

using System;
using Glacier.Desktop.Windowing;
using Glacier.Desktop.Windowing.Platform;
using Glacier.Desktop.Windowing.Platform.Headless;
using Xunit;

public class WindowLifecycleTests
{
    [Fact]
    public void WindowOptions_DefaultValues_AreSensible()
    {
        var options = new WindowOptions();
        Assert.Equal("Glacier Application", options.Title);
        Assert.Equal(1280, options.Width);
        Assert.Equal(720, options.Height);
        Assert.True(options.IsVisible);
        Assert.True(options.Resizable);
        Assert.True(options.DarkMode);
        Assert.True(options.EnableRawInput);
        Assert.True(options.PerMonitorDpiAware);
        Assert.Equal(SwapchainBackend.Auto, options.PreferredBackend);
    }

    [Fact]
    public void WindowSize_Properties_AreConsistent()
    {
        var size = new WindowSize(1920, 1080);
        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
        Assert.Equal(1920f / 1080f, size.AspectRatio, 4);

        var empty = WindowSize.Empty;
        Assert.Equal(0, empty.Width);
        Assert.Equal(0, empty.Height);
        Assert.Equal(1.0f, empty.AspectRatio);
    }

    [Fact]
    public void HeadlessWindow_CreationAndPropertyMutation_BehavesCorrectly()
    {
        var options = new WindowOptions
        {
            Title = "Test Engine Window",
            Width = 800,
            Height = 600,
            IsVisible = false
        };

        using var window = new HeadlessWindow(options);
        Assert.Equal("Test Engine Window", window.Title);
        Assert.Equal(800, window.Size.Width);
        Assert.Equal(600, window.Size.Height);
        Assert.False(window.IsVisible);
        Assert.Equal(IntPtr.Zero, window.NativeHandle);

        window.Title = "Updated Title";
        Assert.Equal("Updated Title", window.Title);

        window.IsVisible = true;
        Assert.True(window.IsVisible);

        bool resizedFired = false;
        int newW = 0, newH = 0;
        window.Resized += (w, h) =>
        {
            resizedFired = true;
            newW = w;
            newH = h;
        };

        window.Size = new WindowSize(1024, 768);
        Assert.True(resizedFired);
        Assert.Equal(1024, newW);
        Assert.Equal(768, newH);
        Assert.Equal(1024, window.Size.Width);
        Assert.Equal(768, window.Size.Height);
    }

    [Fact]
    public void HeadlessWindow_InputEventDispatch_WorksFlawlessly()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        var received = new List<InputEvent>();

        window.InputReceived += evt => received.Add(evt);

        var e1 = new InputEvent(InputEventType.KeyDown, 65, 0, 0, 1000);
        var e2 = new InputEvent(InputEventType.MouseMove, 0, 100, 200, 2000);
        var e3 = new InputEvent(InputEventType.KeyUp, 65, 0, 0, 3000);

        window.EnqueueInput(e1);
        window.EnqueueInput(e2);
        window.EnqueueInput(e3);

        Assert.Empty(received);

        window.PollEvents();

        Assert.Equal(3, received.Count);
        Assert.Equal(e1, received[0]);
        Assert.Equal(e2, received[1]);
        Assert.Equal(e3, received[2]);

        // Second poll has nothing pending
        window.PollEvents();
        Assert.Equal(3, received.Count);
    }

    [Fact]
    public void HeadlessWindow_ClosingAndDisposal_FiresEvents()
    {
        var window = new HeadlessWindow(new WindowOptions());
        bool closingFired = false;

        window.Closing += () => closingFired = true;
        window.TriggerClosing();

        Assert.True(closingFired);

        window.Dispose();

        Assert.Throws<ObjectDisposedException>(() => window.CreateSwapchain(new SwapchainDescription(800, 600)));
    }

    [Fact]
    public void WindowFactory_CreateHeadless_ReturnsWorkingInstance()
    {
        using var window = WindowFactory.CreateHeadless(new WindowOptions { Width = 640, Height = 480 });
        Assert.NotNull(window);
        Assert.Equal(640, window.Size.Width);
        Assert.Equal(480, window.Size.Height);
    }

    [Fact]
    public void WindowFactory_CurrentPlatform_ReturnsValidKind()
    {
        PlatformKind current = WindowFactory.CurrentPlatform;
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(PlatformKind.Windows, current);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(PlatformKind.MacOS, current);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.True(current is PlatformKind.LinuxX11 or PlatformKind.LinuxWayland);
        }
    }
}
