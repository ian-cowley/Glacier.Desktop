namespace Glacier.Desktop.UI.Controls;

using System;
using System.Collections.Generic;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Containers;
using SkiaSharp;

/// <summary>
/// Top-level desktop menu bar containing root menu items ("File", "Edit", etc.).
/// </summary>
public class MenuBar : FlexRow
{
    private MenuItem? _openMenu;

    public MenuItem? OpenMenu
    {
        get => _openMenu;
        set
        {
            if (_openMenu != value)
            {
                if (_openMenu != null) _openMenu.IsOpen = false;
                _openMenu = value;
                if (_openMenu != null) _openMenu.IsOpen = true;
            }
        }
    }

    public MenuDropDown? ActiveDropDown => OpenMenu?.DropDown;

    public MenuBar() : base(spacing: 2f)
    {
        BackgroundColor = Color4.HeaderBackground;
        Padding = new Thickness(6f, 3f);
        Height = 28f;
    }

    public MenuItem AddMenu(string header)
    {
        var item = new MenuItem(header) { IsTopLevel = true, MenuBar = this };
        Add(item);
        return item;
    }

    public void CloseMenu()
    {
        OpenMenu = null;
    }

    public void ToggleMenu(MenuItem item)
    {
        if (OpenMenu == item)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu = item;
            item.EnsureDropDown();
        }
    }
}

/// <summary>
/// Menu item entry in a desktop menu bar or drop-down popup.
/// </summary>
public class MenuItem : VisualNode
{
    private readonly List<MenuItem> _items = new();

    public string Header { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
    public bool IsSeparator { get; set; }
    public Action? OnClick { get; set; }
    public IReadOnlyList<MenuItem> Items => _items;
    public bool HasChildren => _items.Count > 0;

    public bool IsTopLevel { get; set; }
    public MenuBar? MenuBar { get; set; }
    public bool IsOpen { get; set; }
    public bool IsHovered { get; set; }

    public MenuDropDown? DropDown { get; private set; }

    public MenuItem(string header, Action? onClick = null)
    {
        Header = header;
        OnClick = onClick;
        Padding = new Thickness(8f, 3f);
    }

    public MenuItem Add(string header, Action? onClick = null, string? shortcut = null)
    {
        var child = new MenuItem(header, onClick)
        {
            Shortcut = shortcut,
            MenuBar = this.MenuBar
        };
        _items.Add(child);
        EnsureDropDown();
        return child;
    }

    public void AddSeparator()
    {
        _items.Add(new MenuItem(string.Empty) { IsSeparator = true, MenuBar = this.MenuBar });
        EnsureDropDown();
    }

    public void EnsureDropDown()
    {
        if (_items.Count > 0 && DropDown == null)
        {
            DropDown = new MenuDropDown(this);
        }
    }

    public void PerformClick()
    {
        if (IsTopLevel && HasChildren && MenuBar != null)
        {
            MenuBar.ToggleMenu(this);
        }
        else if (!IsSeparator)
        {
            OnClick?.Invoke();
            MenuBar?.CloseMenu();
        }
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        if (IsSeparator)
        {
            Bounds.DesiredWidth = availableWidth;
            Bounds.DesiredHeight = 8f;
            return;
        }

        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 12f,
            IsAntialias = true
        };

        float w = paint.MeasureText(Header);
        Bounds.DesiredWidth = w + Padding.Horizontal + 4f;
        Bounds.DesiredHeight = IsTopLevel ? 22f : 26f;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        if (IsSeparator)
        {
            using var sepPaint = new SKPaint { Color = new SKColor(55, 65, 85, 255), StrokeWidth = 1f };
            float sy = Bounds.ActualY + 4f;
            canvas.DrawLine(Bounds.ActualX + 8f, sy, Bounds.ActualX + Bounds.DesiredWidth - 8f, sy, sepPaint);
            return;
        }

        // Draw hover or open highlight
        if (IsHovered || IsOpen)
        {
            using var bgPaint = new SKPaint
            {
                Color = IsTopLevel ? new SKColor(45, 55, 75, 255) : new SKColor(35, 75, 130, 240),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            var r = new SKRoundRect(new SKRect(Bounds.ActualX, Bounds.ActualY, Bounds.ActualX + Bounds.DesiredWidth, Bounds.ActualY + Bounds.DesiredHeight), 4f);
            canvas.DrawRoundRect(r, bgPaint);
        }

        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", (IsHovered || IsOpen) ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 12f,
            Color = (IsHovered || IsOpen) ? Color4.GlacierBlue.ToSKColor() : Color4.White.ToSKColor(),
            IsAntialias = true
        };

        float ty = Bounds.ActualY + (Bounds.DesiredHeight + 8f) * 0.5f;
        canvas.DrawText(Header, Bounds.ActualX + Padding.Left, ty, paint);

        if (!string.IsNullOrEmpty(Shortcut) && !IsTopLevel)
        {
            using var scPaint = new SKPaint
            {
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                TextSize = 11f,
                Color = new SKColor(140, 155, 175, 255),
                IsAntialias = true
            };
            float scW = scPaint.MeasureText(Shortcut);
            canvas.DrawText(Shortcut, Bounds.ActualX + Bounds.DesiredWidth - Padding.Right - scW, ty, scPaint);
        }
    }
}

/// <summary>
/// Drop-down overlay panel rendering on top of all desktop visuals with shadow, border, and items.
/// </summary>
public class MenuDropDown : VisualNode
{
    private readonly MenuItem _owner;

    public MenuItem Owner => _owner;
    public IReadOnlyList<MenuItem> Items => _owner.Items;

    public MenuDropDown(MenuItem owner)
    {
        _owner = owner;
        BackgroundColor = new Color4(24, 28, 38, 255);
    }

    public void UpdateLayout()
    {
        float startX = _owner.Bounds.ActualX;
        float startY = _owner.Bounds.ActualY + _owner.Bounds.DesiredHeight + 2f;

        // Measure item widths
        float maxW = 210f;
        using var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 12f
        };

        foreach (var item in _owner.Items)
        {
            if (item.IsSeparator) continue;
            float textW = paint.MeasureText(item.Header);
            float scW = !string.IsNullOrEmpty(item.Shortcut) ? paint.MeasureText(item.Shortcut) + 24f : 0f;
            maxW = MathF.Max(maxW, textW + scW + 40f);
        }

        float curY = startY + 4f;
        foreach (var item in _owner.Items)
        {
            float itemH = item.IsSeparator ? 8f : 26f;
            item.Bounds.ActualX = startX + 4f;
            item.Bounds.ActualY = curY;
            item.Bounds.DesiredWidth = maxW - 8f;
            item.Bounds.DesiredHeight = itemH;
            curY += itemH + 2f;
        }

        Bounds.ActualX = startX;
        Bounds.ActualY = startY;
        Bounds.DesiredWidth = maxW;
        Bounds.DesiredHeight = (curY - startY) + 4f;
    }

    public override VisualNode? HitTest(float px, float py)
    {
        UpdateLayout();
        if (px >= Bounds.ActualX && px <= Bounds.ActualX + Bounds.DesiredWidth &&
            py >= Bounds.ActualY && py <= Bounds.ActualY + Bounds.DesiredHeight)
        {
            foreach (var item in _owner.Items)
            {
                if (item.IsSeparator) continue;
                if (px >= item.Bounds.ActualX && px <= item.Bounds.ActualX + item.Bounds.DesiredWidth &&
                    py >= item.Bounds.ActualY && py <= item.Bounds.ActualY + item.Bounds.DesiredHeight)
                {
                    return item;
                }
            }
            return this;
        }
        return null;
    }

    public override void Render(SKCanvas canvas)
    {
        UpdateLayout();

        float x = Bounds.ActualX;
        float y = Bounds.ActualY;
        float w = Bounds.DesiredWidth;
        float h = Bounds.DesiredHeight;

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 120),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f),
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x + 2f, y + 3f, x + w + 2f, y + h + 3f), 6f), shadowPaint);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor.ToSKColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 6f);
        canvas.DrawRoundRect(rect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = Color4.BorderColor.ToSKColor(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, borderPaint);

        // Draw items
        foreach (var item in _owner.Items)
        {
            item.Render(canvas);
        }
    }
}
