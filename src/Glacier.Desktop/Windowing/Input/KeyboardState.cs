namespace Glacier.Desktop.Windowing.Input;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Bitset-backed keyboard state tracker supporting 256 virtual key codes with zero heap allocations.
/// </summary>
public struct KeyboardState
{
    private ulong _b0;
    private ulong _b1;
    private ulong _b2;
    private ulong _b3;

    /// <summary>
    /// Checks if the specified virtual key is currently pressed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsKeyDown(int virtualKey)
    {
        if ((uint)virtualKey >= 256) return false;

        int word = virtualKey >> 6;
        ulong mask = 1UL << (virtualKey & 63);

        return word switch
        {
            0 => (_b0 & mask) != 0,
            1 => (_b1 & mask) != 0,
            2 => (_b2 & mask) != 0,
            3 => (_b3 & mask) != 0,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the specified virtual key is currently released.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsKeyUp(int virtualKey) => !IsKeyDown(virtualKey);

    /// <summary>
    /// Updates the state of the specified virtual key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetKey(int virtualKey, bool isDown)
    {
        if ((uint)virtualKey >= 256) return;

        int word = virtualKey >> 6;
        ulong mask = 1UL << (virtualKey & 63);

        if (isDown)
        {
            switch (word)
            {
                case 0: _b0 |= mask; break;
                case 1: _b1 |= mask; break;
                case 2: _b2 |= mask; break;
                case 3: _b3 |= mask; break;
            }
        }
        else
        {
            switch (word)
            {
                case 0: _b0 &= ~mask; break;
                case 1: _b1 &= ~mask; break;
                case 2: _b2 &= ~mask; break;
                case 3: _b3 &= ~mask; break;
            }
        }
    }

    /// <summary>
    /// Resets all key states to released.
    /// </summary>
    public void Clear()
    {
        _b0 = 0;
        _b1 = 0;
        _b2 = 0;
        _b3 = 0;
    }
}
