namespace Glacier.Desktop.Grids;

using System.Runtime.InteropServices;

/// <summary>
/// Transient, stack-allocated descriptor of a visible row segment inside VirtualDataGrid.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly ref struct VirtualRowSpan
{
    public readonly long RowIndex;
    public readonly float ScreenY;
    public readonly float Height;

    public VirtualRowSpan(long rowIndex, float screenY, float height)
    {
        RowIndex = rowIndex;
        ScreenY = screenY;
        Height = height;
    }
}
