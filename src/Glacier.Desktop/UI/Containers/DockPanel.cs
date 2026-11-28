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

    public override bool Remove(VisualNode child)
    {
        _dockPositions.Remove(child);
        return base.Remove(child);
    }

    public override void Clear()
    {
        _dockPositions.Clear();
        base.Clear();
    }

    public override void Arrange(in LayoutBox finalBounds)
    {
        Bounds.ActualX = finalBounds.ActualX + Margin.Left;
        Bounds.ActualY = finalBounds.ActualY + Margin.Top;
        Bounds.DesiredWidth = !float.IsNaN(Width) && Width > 0 ? Width : finalBounds.DesiredWidth;
        Bounds.DesiredHeight = !float.IsNaN(Height) && Height > 0 ? Height : finalBounds.DesiredHeight;

        float curLeft = Bounds.ActualX + Padding.Left;
        float curTop = Bounds.ActualY + Padding.Top;
        float curRight = MathF.Max(curLeft, Bounds.ActualX + Bounds.DesiredWidth - Padding.Right);
        float curBottom = MathF.Max(curTop, Bounds.ActualY + Bounds.DesiredHeight - Padding.Bottom);

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.IsVisible) continue;

            DockPosition dock = _dockPositions.GetValueOrDefault(child, DockPosition.Fill);
            float availW = MathF.Max(0f, curRight - curLeft);
            float availH = MathF.Max(0f, curBottom - curTop);

            switch (dock)
            {
                case DockPosition.Top:
                {
                    float h = Math.Clamp(child.Bounds.DesiredHeight, 0f, availH);
                    child.Arrange(new LayoutBox(availW, h, curLeft, curTop));
                    curTop = MathF.Min(curBottom, curTop + h);
                    break;
                }

                case DockPosition.Bottom:
                {
                    float h = Math.Clamp(child.Bounds.DesiredHeight, 0f, availH);
                    child.Arrange(new LayoutBox(availW, h, curLeft, curBottom - h));
                    curBottom = MathF.Max(curTop, curBottom - h);
                    break;
                }

                case DockPosition.Left:
                {
                    float w = Math.Clamp(child.Bounds.DesiredWidth, 0f, availW);
                    child.Arrange(new LayoutBox(w, availH, curLeft, curTop));
                    curLeft = MathF.Min(curRight, curLeft + w);
                    break;
                }

                case DockPosition.Right:
                {
                    float w = Math.Clamp(child.Bounds.DesiredWidth, 0f, availW);
                    child.Arrange(new LayoutBox(w, availH, curRight - w, curTop));
                    curRight = MathF.Max(curLeft, curRight - w);
                    break;
                }

                case DockPosition.Fill:
                    child.Arrange(new LayoutBox(availW, availH, curLeft, curTop));
                    break;
            }
        }
    }
}
