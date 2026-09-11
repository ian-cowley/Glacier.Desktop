namespace Glacier.Desktop.UI.Containers;

using System;
using System.Collections;
using System.Collections.Generic;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using SkiaSharp;

/// <summary>
/// Base multi-child container for desktop layouts.
/// </summary>
public class Panel : VisualNode, IEnumerable<VisualNode>
{
    protected readonly List<VisualNode> _children = new();
    public IReadOnlyList<VisualNode> Children => _children;

    public void Add(VisualNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public bool Remove(VisualNode child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            return true;
        }
        return false;
    }

    public void Clear()
    {
        foreach (var c in _children) c.Parent = null;
        _children.Clear();
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        base.Measure(availableWidth, availableHeight);
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Measure(availableWidth, availableHeight);
        }
    }

    public override void Arrange(in LayoutBox finalBounds)
    {
        base.Arrange(finalBounds);
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Arrange(finalBounds);
        }
    }

    public override void Render(SKCanvas canvas)
    {
        base.Render(canvas);
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Render(canvas);
        }
    }

    public override VisualNode? HitTest(float px, float py)
    {
        if (!IsVisible) return null;
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(px, py);
            if (hit != null) return hit;
        }
        return base.HitTest(px, py);
    }

    public IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
