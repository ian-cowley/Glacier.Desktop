namespace Glacier.Desktop.Windowing.Input;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// High-frequency mouse state tracker recording position, relative motion deltas, scroll, and buttons.
/// </summary>
public struct MouseState
{
    private float _x;
    private float _y;
    private float _deltaX;
    private float _deltaY;
    private float _wheelDelta;
    private byte _buttonMask; // bit 0: Left, bit 1: Right, bit 2: Middle, bit 3: X1, bit 4: X2

    public readonly float X => _x;
    public readonly float Y => _y;
    public readonly float DeltaX => _deltaX;
    public readonly float DeltaY => _deltaY;
    public readonly float WheelDelta => _wheelDelta;

    public readonly bool LeftButton => (_buttonMask & 0x01) != 0;
    public readonly bool RightButton => (_buttonMask & 0x02) != 0;
    public readonly bool MiddleButton => (_buttonMask & 0x04) != 0;
    public readonly bool XButton1 => (_buttonMask & 0x08) != 0;
    public readonly bool XButton2 => (_buttonMask & 0x10) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsButtonDown(int button)
    {
        if ((uint)button >= 5) return false;
        return (_buttonMask & (1 << button)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPosition(float x, float y)
    {
        _x = x;
        _y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddDelta(float dx, float dy)
    {
        _deltaX += dx;
        _deltaY += dy;
        _x += dx;
        _y += dy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddWheelDelta(float delta)
    {
        _wheelDelta += delta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetButton(int button, bool isDown)
    {
        if ((uint)button >= 5) return;
        byte mask = (byte)(1 << button);
        if (isDown)
        {
            _buttonMask |= mask;
        }
        else
        {
            _buttonMask &= (byte)~mask;
        }
    }

    /// <summary>
    /// Resets transient per-frame relative deltas (movement and wheel) while preserving absolute position and buttons.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetDeltas()
    {
        _deltaX = 0;
        _deltaY = 0;
        _wheelDelta = 0;
    }
}
