namespace Glacier.Desktop.Tests;

using System;
using System.Threading;
using Glacier.Desktop.Windowing;
using Glacier.Desktop.Windowing.Input;
using Glacier.Desktop.Windowing.Platform.Headless;
using Xunit;

public class InputSystemTests
{
    [Fact]
    public void KeyboardState_256KeysBitmanipulation_Accurate()
    {
        var kb = new KeyboardState();

        for (int i = 0; i < 256; i++)
        {
            Assert.False(kb.IsKeyDown(i));
            Assert.True(kb.IsKeyUp(i));
        }

        // Set sparse keys
        kb.SetKey(13, true);  // Enter
        kb.SetKey(32, true);  // Space
        kb.SetKey(65, true);  // 'A'
        kb.SetKey(128, true); // Extended
        kb.SetKey(255, true); // Last

        Assert.True(kb.IsKeyDown(13));
        Assert.True(kb.IsKeyDown(32));
        Assert.True(kb.IsKeyDown(65));
        Assert.True(kb.IsKeyDown(128));
        Assert.True(kb.IsKeyDown(255));

        Assert.False(kb.IsKeyDown(14));
        Assert.False(kb.IsKeyDown(64));

        kb.SetKey(65, false);
        Assert.False(kb.IsKeyDown(65));
        Assert.True(kb.IsKeyUp(65));

        kb.Clear();
        Assert.False(kb.IsKeyDown(13));
        Assert.False(kb.IsKeyDown(255));
    }

    [Fact]
    public void MouseState_PositionDeltasAndButtons_Accurate()
    {
        var mouse = new MouseState();

        mouse.SetPosition(100, 200);
        Assert.Equal(100, mouse.X);
        Assert.Equal(200, mouse.Y);

        mouse.AddDelta(15, -5);
        Assert.Equal(115, mouse.X);
        Assert.Equal(195, mouse.Y);
        Assert.Equal(15, mouse.DeltaX);
        Assert.Equal(-5, mouse.DeltaY);

        mouse.AddWheelDelta(120);
        Assert.Equal(120, mouse.WheelDelta);

        mouse.SetButton(0, true);
        mouse.SetButton(1, true);
        Assert.True(mouse.LeftButton);
        Assert.True(mouse.RightButton);
        Assert.False(mouse.MiddleButton);

        mouse.ResetDeltas();
        // Position and buttons remain, deltas reset to 0
        Assert.Equal(115, mouse.X);
        Assert.Equal(195, mouse.Y);
        Assert.Equal(0, mouse.DeltaX);
        Assert.Equal(0, mouse.DeltaY);
        Assert.Equal(0, mouse.WheelDelta);
        Assert.True(mouse.LeftButton);
    }

    [Fact]
    public void GamepadState_AnalogAndButtons_Accurate()
    {
        var gp = new GamepadState
        {
            IsConnected = true,
            PacketNumber = 42,
            ThumbLeftX = -0.75f,
            ThumbLeftY = 0.5f,
            TriggerRight = 1.0f
        };

        gp.SetButton(GamepadButtons.A, true);
        gp.SetButton(GamepadButtons.RightShoulder, true);

        Assert.True(gp.IsConnected);
        Assert.Equal(42U, gp.PacketNumber);
        Assert.Equal(-0.75f, gp.ThumbLeftX);
        Assert.Equal(0.5f, gp.ThumbLeftY);
        Assert.Equal(1.0f, gp.TriggerRight);
        Assert.True(gp.IsButtonDown(GamepadButtons.A));
        Assert.True(gp.IsButtonDown(GamepadButtons.RightShoulder));
        Assert.False(gp.IsButtonDown(GamepadButtons.B));

        gp.SetButton(GamepadButtons.A, false);
        Assert.False(gp.IsButtonDown(GamepadButtons.A));
    }

    [Fact]
    public void InputState_UnifiedReceiver_ProcessesEvents()
    {
        var state = new InputState();

        state.OnInput(new InputEvent(InputEventType.KeyDown, 65, 0, 0, 1000));
        Assert.True(state.Keyboard.IsKeyDown(65));
        Assert.Equal(1000UL, state.LastEventTimestampNs);

        state.OnInput(new InputEvent(InputEventType.KeyUp, 65, 0, 0, 2000));
        Assert.False(state.Keyboard.IsKeyDown(65));

        state.OnInput(new InputEvent(InputEventType.MouseMove, 0, 300, 400, 3000));
        Assert.Equal(300, state.Mouse.X);
        Assert.Equal(400, state.Mouse.Y);
        Assert.Equal(0, state.Mouse.DeltaX);
        Assert.Equal(0, state.Mouse.DeltaY);

        state.OnInput(new InputEvent(InputEventType.MouseDelta, 0, 12, -8, 3500));
        Assert.Equal(312, state.Mouse.X);
        Assert.Equal(392, state.Mouse.Y);
        Assert.Equal(12, state.Mouse.DeltaX);
        Assert.Equal(-8, state.Mouse.DeltaY);

        state.NewFrame();
        Assert.Equal(0, state.Mouse.DeltaX);
        Assert.Equal(0, state.Mouse.DeltaY);
        Assert.Equal(312, state.Mouse.X);

        state.OnInput(new InputEvent(InputEventType.MouseDown, 0, 300, 400, 4000));
        Assert.True(state.Mouse.LeftButton);

        state.OnInput(new InputEvent(InputEventType.MouseUp, 0, 300, 400, 5000));
        Assert.False(state.Mouse.LeftButton);

        state.OnInput(new InputEvent(InputEventType.MouseWheel, 0, 0, -120, 6000));
        Assert.Equal(-120, state.Mouse.WheelDelta);
    }

    [Fact]
    public void InputPoller_SynchronousAndThreadedPolling_IncrementsCounts()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        using var poller = new InputPoller(window, targetFrequencyHz: 1000.0);

        Assert.Equal(0, poller.TotalPollCount);
        Assert.Equal(1000.0, poller.TargetFrequencyHz);

        poller.PollTick();
        Assert.Equal(1, poller.TotalPollCount);

        poller.Start();
        Thread.Sleep(50); // ~50 ticks at 1000Hz
        poller.Stop();

        Assert.True(poller.TotalPollCount > 10, $"Expected > 10 polls, got {poller.TotalPollCount}");
    }
}
