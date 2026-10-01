namespace Glacier.Desktop.UI;

using System.Runtime.InteropServices;
using Glacier.Graphics;

/// <summary>
/// Fast unmanaged 32-bit RGBA color representation for desktop UI elements.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Color4(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Color4 Transparent = new(0, 0, 0, 0);
    public static readonly Color4 Black = new(0, 0, 0, 255);
    public static readonly Color4 White = new(255, 255, 255, 255);
    public static readonly Color4 GlacierBlue = new(50, 180, 255, 255);
    public static readonly Color4 DarkBackground = new(18, 22, 28, 255);
    public static readonly Color4 PanelBackground = new(28, 34, 44, 255);
    public static readonly Color4 BorderColor = new(45, 55, 72, 255);
    public static readonly Color4 TextMuted = new(160, 175, 195, 255);
    public static readonly Color4 HeaderBackground = new(22, 27, 34, 255);

    public Rgba32 ToRgba32() => new(R, G, B, A);

    public static implicit operator Rgba32(Color4 c) => new(c.R, c.G, c.B, c.A);
    public static implicit operator Color4(Rgba32 c) => new(c.R, c.G, c.B, c.A);
}
