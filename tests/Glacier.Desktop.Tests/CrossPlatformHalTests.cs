namespace Glacier.Desktop.Tests;

using System;
using System.Collections.Generic;
using Glacier.Desktop.Windowing;
using Glacier.Desktop.Windowing.Platform.Linux;
using Glacier.Desktop.Windowing.Platform.Mac;
using Glacier.Desktop.Windowing.Platform.Mobile;
using Xunit;

public class CrossPlatformHalTests
{
    [Fact]
    public void WaylandProtocol_HeaderSerialization_RoundtripsCorrectly()
    {
        Span<byte> buffer = stackalloc byte[8];
        uint objId = 42;
        ushort opcode = 3;
        ushort size = 28;

        WaylandProtocol.WriteHeader(buffer, objId, opcode, size);

        var (parsedObj, parsedOpcode, parsedSize) = WaylandProtocol.ReadHeader(buffer);
        Assert.Equal(objId, parsedObj);
        Assert.Equal(opcode, parsedOpcode);
        Assert.Equal(size, parsedSize);
    }

    [Fact]
    public void WaylandProtocol_StringEncoding_PadsTo4Bytes()
    {
        Span<byte> buffer = stackalloc byte[64];
        string title = "Glacier"; // 7 chars + 1 null = 8 bytes -> 4-byte aligned!
        int encodedLen = WaylandProtocol.EncodeString(buffer, title);

        // 4 bytes length prefix (8) + 8 bytes "Glacier\0" = 12 bytes total
        Assert.Equal(12, encodedLen);
        uint stringLen = BitConverter.ToUInt32(buffer[0..4]);
        Assert.Equal(8U, stringLen);

        string titleOdd = "Hi"; // 2 chars + 1 null = 3 bytes -> total 4 + 3 = 7 -> padded to 8 bytes
        int encodedOdd = WaylandProtocol.EncodeString(buffer, titleOdd);
        Assert.Equal(8, encodedOdd);
    }

    [Fact]
    public void AndroidNativeWindow_TouchDispatchAndZeroInitialHandle_FunctionsCorrectly()
    {
        using var window = new AndroidNativeWindow(new WindowOptions { Width = 1080, Height = 2400 });
        Assert.Equal(IntPtr.Zero, window.NativeHandle);
        Assert.False(window.IsSurfaceAttached);

        if (!OperatingSystem.IsAndroid())
        {
            Assert.Throws<PlatformNotSupportedException>(() => window.AttachSurface(new IntPtr(1)));
        }

        var events = new List<InputEvent>();
        window.InputReceived += evt => events.Add(evt);

        TouchSnapshot[] touches =
        [
            new TouchSnapshot(1, 100, 200, 0.8f, TouchPhase.Began, 1000),
            new TouchSnapshot(1, 110, 210, 0.85f, TouchPhase.Moved, 2000),
            new TouchSnapshot(1, 120, 220, 0.0f, TouchPhase.Ended, 3000)
        ];

        window.DispatchTouch(touches);

        Assert.Equal(3, events.Count);
        Assert.Equal(InputEventType.TouchStart, events[0].Type);
        Assert.Equal(1, events[0].KeyOrButton);
        Assert.Equal(100f, events[0].X);

        Assert.Equal(InputEventType.TouchMove, events[1].Type);
        Assert.Equal(InputEventType.TouchEnd, events[2].Type);
    }

    [Fact]
    public void IosUiWindow_ProMotionAndTouch_ZeroFakeHandles()
    {
        using var window = new IosUiWindow(new WindowOptions { Width = 1179, Height = 2556 });
        Assert.Equal(IntPtr.Zero, window.NativeHandle);
        Assert.Equal(IntPtr.Zero, window.MetalLayer);
        Assert.Equal(120.0, window.DisplayRefreshRate);

        if (!OperatingSystem.IsIOS())
        {
            Assert.Throws<PlatformNotSupportedException>(() => window.AttachHandles(new IntPtr(1), new IntPtr(2)));
        }

        window.SetPreferredFramesPerSecond(60);
        Assert.Equal(60.0, window.DisplayRefreshRate);

        var events = new List<InputEvent>();
        window.InputReceived += evt => events.Add(evt);

        TouchSnapshot[] touches =
        [
            new TouchSnapshot(0, 50, 50, 1.0f, TouchPhase.Began, 500)
        ];

        window.DispatchTouches(touches);

        Assert.Single(events);
        Assert.Equal(InputEventType.TouchStart, events[0].Type);
    }

    [Fact]
    public void PlatformSpecificDesktopWindows_EnforceHostOperatingSystem()
    {
        var opts = new WindowOptions { Width = 800, Height = 600, IsVisible = false };

        if (!OperatingSystem.IsLinux())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new X11Window(opts));
            Assert.Throws<PlatformNotSupportedException>(() => new WaylandWindow(opts));
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            Assert.ThrowsAny<Exception>(() => new X11Window(opts));
        }

        if (!OperatingSystem.IsMacOS())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new CocoaWindow(opts));
        }
    }
}
