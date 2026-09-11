namespace Glacier.Desktop.UI.Containers;

using System;
using System.Collections.Generic;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;

public enum DockPosition : byte
{
    Top,
    Bottom,
    Left,
    Right,
    Fill
}

/// <summary>
/// Classic desktop docking panel docking children to edges and filling remaining central space.
/// </summary>
public class DockPanel : Panel
{
    private readonly Dictionary<VisualNode, DockPosition> _dockPositions = new();

    public void Add(VisualNode child, DockPosition dock)
    {
        _dockPositions[child] = dock;
        base.Add(child);
    }

    public override void Arrange(in LayoutBox finalBounds)
    {
        Bounds.ActualX = finalBounds.ActualX + Margin.Left;
        Bounds.ActualY = finalBounds.ActualY + Margin.Top;
        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : finalBounds.DesiredWidth;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : finalBounds.DesiredHeight;

        float curLeft = Bounds.ActualX + Padding.Left;
        float curTop = Bounds.ActualY + Padding.Top;
        float curRight = Bounds.ActualX + Bounds.DesiredWidth - Padding.Right;
        float curBottom = Bounds.ActualY + Bounds.DesiredHeight - Padding.Bottom;

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            DockPosition dock = _dockPositions.GetValueOrDefault(child, DockPosition.Fill);

            switch (dock)
            {
                case DockPosition.Top:
                    child.Arrange(new LayoutBox(curRight - curLeft, child.Bounds.DesiredHeight, curLeft, curTop));
                    curTop += child.Bounds.DesiredHeight;
                    break;

                case DockPosition.Bottom:
                    float h = child.Bounds.DesiredHeight;
                    child.Arrange(new LayoutBox(curRight - curLeft, h, curLeft, curBottom - h));
                    curBottom -= h;
                    break;

                case DockPosition.Left:
                    child.Arrange(new LayoutBox(child.Bounds.DesiredWidth, curBottom - curTop, curLeft, curTop));
                    curLeft += child.Bounds.DesiredWidth;
                    break;

                case DockPosition.Right:
                    float w = child.Bounds.DesiredWidth;
                    child.Arrange(new LayoutBox(w, curBottom - curTop, curRight - w, curTop));
                    curRight -= w;
                    break;

                case DockPosition.Fill:
                    child.Arrange(new LayoutBox(MathF.Max(0f, curRight - curLeft), MathF.Max(0f, curBottom - curTop), curLeft, curTop));
                    break;
            }
        }
    }
}
