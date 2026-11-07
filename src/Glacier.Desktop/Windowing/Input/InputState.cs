namespace Glacier.Desktop.Windowing.Input;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Unified hardware input receiver maintaining current Keyboard, Mouse, and Gamepad states.
/// </summary>
public sealed class InputState : IInputReceiver
{
    public KeyboardState Keyboard;
    public MouseState Mouse;
    public GamepadState Gamepad;

    private ulong _lastEventTimestampNs;

    /// <summary>
    /// Gets the nanosecond timestamp of the most recent input event.
    /// </summary>
    public ulong LastEventTimestampNs => _lastEventTimestampNs;

    /// <summary>
    /// Ingests a raw hardware input event into the tracked state representations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnInput(in InputEvent evt)
    {
        _lastEventTimestampNs = evt.TimestampNs;

        switch (evt.Type)
        {
            case InputEventType.KeyDown:
                Keyboard.SetKey(evt.KeyOrButton, true);
                break;

            case InputEventType.KeyUp:
                Keyboard.SetKey(evt.KeyOrButton, false);
                break;

            case InputEventType.MouseMove:
                Mouse.SetPosition(evt.X, evt.Y);
                break;

            case InputEventType.MouseDelta:
                Mouse.AddDelta(evt.X, evt.Y);
                break;

            case InputEventType.MouseDown:
                Mouse.SetButton(evt.KeyOrButton, true);
                break;

            case InputEventType.MouseUp:
                Mouse.SetButton(evt.KeyOrButton, false);
                break;

            case InputEventType.MouseWheel:
                Mouse.AddWheelDelta(evt.Y);
                break;

            case InputEventType.TouchStart:
            case InputEventType.TouchMove:
            case InputEventType.TouchEnd:
                Mouse.SetPosition(evt.X, evt.Y);
                if (evt.Type != InputEventType.TouchMove)
                {
                    Mouse.SetButton(0, evt.Type == InputEventType.TouchStart);
                }
                break;
        }
    }

    /// <summary>
    /// Resets transient per-frame deltas at the start of a new frame tick.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void NewFrame()
    {
        Mouse.ResetDeltas();
    }
}
