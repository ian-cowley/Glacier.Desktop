namespace Glacier.Desktop.Windowing.Input;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Bitflags representing standard controller buttons.
/// </summary>
[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    DPadUp = 1 << 4,
    DPadDown = 1 << 5,
    DPadLeft = 1 << 6,
    DPadRight = 1 << 7,
    LeftShoulder = 1 << 8,
    RightShoulder = 1 << 9,
    Start = 1 << 10,
    Back = 1 << 11,
    LeftThumb = 1 << 12,
    RightThumb = 1 << 13
}

/// <summary>
/// Gamepad hardware state container capturing analog sticks, analog triggers, and digital buttons.
/// </summary>
public struct GamepadState
{
    public bool IsConnected;
    public uint PacketNumber;

    // Analog sticks [-1.0f, +1.0f]
    public float ThumbLeftX;
    public float ThumbLeftY;
    public float ThumbRightX;
    public float ThumbRightY;

    // Analog triggers [0.0f, 1.0f]
    public float TriggerLeft;
    public float TriggerRight;

    // Digital buttons
    public GamepadButtons Buttons;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsButtonDown(GamepadButtons button) => (Buttons & button) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetButton(GamepadButtons button, bool isDown)
    {
        if (isDown)
        {
            Buttons |= button;
        }
        else
        {
            Buttons &= ~button;
        }
    }
}
