namespace Glacier.Desktop.Layout;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Margins and padding definitions for desktop visual elements.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public static readonly Thickness Zero = new(0f);
}
