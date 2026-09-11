namespace Glacier.Desktop.Layout;

using System.Runtime.InteropServices;

/// <summary>
/// Contiguous 16-byte unmanaged bounding box representation for SIMD layout passes.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct LayoutBox
{
    public float DesiredWidth;
    public float DesiredHeight;
    public float ActualX;
    public float ActualY;

    public LayoutBox(float desiredWidth, float desiredHeight)
    {
        DesiredWidth = desiredWidth;
        DesiredHeight = desiredHeight;
        ActualX = 0f;
        ActualY = 0f;
    }

    public LayoutBox(float desiredWidth, float desiredHeight, float actualX, float actualY)
    {
        DesiredWidth = desiredWidth;
        DesiredHeight = desiredHeight;
        ActualX = actualX;
        ActualY = actualY;
    }
}
