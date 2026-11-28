namespace Glacier.Desktop.Grids;

using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Polaris;
using Glacier.Polaris.Data;
using Glacier.Graphics;
using Glacier.Graphics.Text;
using Glacier.Graphics.Vector;

/// <summary>
/// High-performance CPU/GPU-rendered virtual data grid capable of rendering 1,000,000+ rows
/// with pooled vector paths and zero-allocation cell formatting on scroll hot paths.
/// </summary>
public sealed class VirtualDataGrid : VisualNode, IDisposable
{
    private DataFrame? _source;
    private bool _disposed;

    private static readonly Font s_headerFont = new(12f, bold: true);
    private static readonly Font s_cellFont = new(12f, bold: false);

    private readonly Paint _selPaint = new(new Rgba32(25, 75, 140, 240), PaintStyle.Fill);
    private readonly Paint _trackPaint = new(new Rgba32(20, 24, 32, 200), PaintStyle.Fill);
    private Paint _cellTextPaint = new(Rgba32.White, PaintStyle.Fill);

    private readonly VectorPath _clipPath = new(8);
    private readonly VectorPath _scratchPath = new(8);
    private readonly VectorPath _evenRowsPath = new(128);
    private readonly VectorPath _oddRowsPath = new(128);
    private readonly VectorPath _gridLinesPath = new(256);

    public DataFrame? SourceDataFrame
    {
        get => _source;
        set
        {
            _source = value;
            ScrollOffsetY = 0f;
        }
    }

    public float HeaderHeight { get; set; } = 32.0f;
    public float RowHeight { get; set; } = 28.0f;
    public float ScrollOffsetY { get; set; } = 0f;
    public long? SelectedRowIndex { get; set; }
    public Action<long>? OnRowSelected { get; set; }
    public float MaxScrollOffsetY => MathF.Max(0f, (TotalRowCount * RowHeight) - MathF.Max(0f, Bounds.DesiredHeight - HeaderHeight));

    private bool _isDraggingScrollbar;

    public long TotalRowCount => _source?.RowCount ?? 0;
    public int VisibleRowCount { get; private set; }

    public Color4 HeaderBg { get; set; } = Color4.HeaderBackground;
    public Color4 RowBgEven { get; set; } = Color4.DarkBackground;
    public Color4 RowBgOdd { get; set; } = Color4.PanelBackground;
    public Color4 GridLineColor { get; set; } = Color4.BorderColor;
    public Color4 TextColor { get; set; } = Color4.White;

    public VirtualDataGrid()
    {
        BackgroundColor = Color4.DarkBackground;
    }

    public void ScrollToRow(long rowIndex)
    {
        long total = TotalRowCount;
        if (total == 0) return;
        long target = Math.Clamp(rowIndex, 0, total - 1);
        ScrollOffsetY = Math.Clamp(target * RowHeight, 0f, MaxScrollOffsetY);
    }

    public void ScrollBy(float deltaPixels)
    {
        ScrollOffsetY = Math.Clamp(ScrollOffsetY + deltaPixels, 0f, MaxScrollOffsetY);
    }

    public bool HandleMouseDown(float px, float py)
    {
        if (_disposed || _source == null || TotalRowCount == 0) return false;

        float gridX = Bounds.ActualX;
        float gridY = Bounds.ActualY;
        float gridW = Bounds.DesiredWidth;
        float gridH = Bounds.DesiredHeight;
        float bodyY = gridY + HeaderHeight;
        float bodyH = gridH - HeaderHeight;

        // Check scrollbar click (right 14px)
        float trackW = 14f;
        float trackX = gridX + gridW - trackW;
        if (px >= trackX && px <= gridX + gridW && py >= bodyY && py <= gridY + gridH)
        {
            _isDraggingScrollbar = true;
            float ratio = Math.Clamp((py - bodyY) / MathF.Max(1f, bodyH), 0f, 1f);
            ScrollOffsetY = ratio * MaxScrollOffsetY;
            return true;
        }

        // Check row click
        if (py >= bodyY && py <= gridY + gridH && px >= gridX && px < trackX)
        {
            long clickedRow = (long)MathF.Floor((ScrollOffsetY + py - bodyY) / RowHeight);
            if (clickedRow >= 0 && clickedRow < TotalRowCount)
            {
                SelectedRowIndex = clickedRow;
                OnRowSelected?.Invoke(clickedRow);
                return true;
            }
        }

        return false;
    }

    public bool HandleMouseMove(float px, float py, bool isMouseDown)
    {
        if (!_disposed && _isDraggingScrollbar && isMouseDown)
        {
            float gridY = Bounds.ActualY;
            float gridH = Bounds.DesiredHeight;
            float bodyY = gridY + HeaderHeight;
            float bodyH = gridH - HeaderHeight;

            float ratio = Math.Clamp((py - bodyY) / MathF.Max(1f, bodyH), 0f, 1f);
            ScrollOffsetY = ratio * MaxScrollOffsetY;
            return true;
        }
        return false;
    }

    public void HandleMouseUp()
    {
        _isDraggingScrollbar = false;
    }

    public override void Measure(float availableWidth, float availableHeight)
    {
        base.Measure(availableWidth, availableHeight);
        float bodyHeight = MathF.Max(0f, Bounds.DesiredHeight - HeaderHeight);
        VisibleRowCount = (int)MathF.Ceiling(bodyHeight / RowHeight) + 1;
    }

    public override void Render(IGraphicsCanvas canvas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsVisible) return;
        base.Render(canvas);

        if (_source == null || _source.Columns.Count == 0) return;

        float gridX = Bounds.ActualX;
        float gridY = Bounds.ActualY;
        float gridW = Bounds.DesiredWidth;
        float gridH = Bounds.DesiredHeight;

        int colCount = _source.Columns.Count;
        float colWidth = gridW / MathF.Max(1, colCount);

        canvas.Save();
        _clipPath.Clear();
        _clipPath.AddRect(gridX, gridY, gridW, gridH);
        canvas.ClipPath(_clipPath);

        // 1. Render Column Headers
        _scratchPath.Clear();
        _scratchPath.AddRect(gridX, gridY, gridW, HeaderHeight);
        canvas.FillPath(_scratchPath, new Paint(HeaderBg.ToRgba32(), PaintStyle.Fill));

        var headerTextPaint = new Paint(Color4.GlacierBlue.ToRgba32(), PaintStyle.Fill);
        var gridLinePaint = new Paint(GridLineColor.ToRgba32(), PaintStyle.Stroke, 1f);

        _gridLinesPath.Clear();
        for (int c = 0; c < colCount; c++)
        {
            float cx = gridX + c * colWidth;
            canvas.DrawText(_source.Columns[c].Name.AsSpan(), cx + 8f, gridY + 20f, s_headerFont, headerTextPaint);
            _gridLinesPath.AddLine(cx, gridY, cx, gridY + gridH);
        }
        _gridLinesPath.AddLine(gridX, gridY + HeaderHeight, gridX + gridW, gridY + HeaderHeight);

        // 2. Render Virtualized Rows
        long totalRows = TotalRowCount;
        if (totalRows == 0)
        {
            canvas.DrawPath(_gridLinesPath, gridLinePaint);
            canvas.Restore();
            return;
        }

        float bodyY = gridY + HeaderHeight;
        float bodyH = gridH - HeaderHeight;

        long startRow = (long)MathF.Floor(ScrollOffsetY / RowHeight);
        startRow = Math.Clamp(startRow, 0, totalRows);
        int rowsToRender = (int)Math.Min((long)VisibleRowCount + 2L, Math.Max(0L, totalRows - startRow));

        var rowEvenPaint = new Paint(RowBgEven.ToRgba32(), PaintStyle.Fill);
        var rowOddPaint = new Paint(RowBgOdd.ToRgba32(), PaintStyle.Fill);
        _cellTextPaint = new Paint(TextColor.ToRgba32(), PaintStyle.Fill);
        var selBarPaint = new Paint(Color4.GlacierBlue.ToRgba32(), PaintStyle.Fill);

        _evenRowsPath.Clear();
        _oddRowsPath.Clear();

        float scrollMod = ScrollOffsetY % RowHeight;
        long? selectedDrawRow = null;
        float selectedDrawY = 0f;

        for (int r = 0; r < rowsToRender; r++)
        {
            long rowIndex = startRow + r;
            if (rowIndex >= totalRows) break;

            float rowY = bodyY + (r * RowHeight) - scrollMod;
            if (rowIndex == SelectedRowIndex)
            {
                selectedDrawRow = rowIndex;
                selectedDrawY = rowY;
            }
            else if ((rowIndex & 1L) == 0L)
            {
                _evenRowsPath.AddRect(gridX, rowY, gridW, RowHeight);
            }
            else
            {
                _oddRowsPath.AddRect(gridX, rowY, gridW, RowHeight);
            }

            _gridLinesPath.AddLine(gridX, rowY + RowHeight, gridX + gridW, rowY + RowHeight);
        }

        if (_evenRowsPath.VerbCount > 0)
        {
            canvas.FillPath(_evenRowsPath, rowEvenPaint);
        }
        if (_oddRowsPath.VerbCount > 0)
        {
            canvas.FillPath(_oddRowsPath, rowOddPaint);
        }
        if (selectedDrawRow.HasValue)
        {
            _scratchPath.Clear();
            _scratchPath.AddRect(gridX, selectedDrawY, gridW, RowHeight);
            canvas.FillPath(_scratchPath, _selPaint);

            _scratchPath.Clear();
            _scratchPath.AddRect(gridX, selectedDrawY, 4f, RowHeight);
            canvas.FillPath(_scratchPath, selBarPaint);
        }

        for (int r = 0; r < rowsToRender; r++)
        {
            long rowIndex = startRow + r;
            if (rowIndex >= totalRows) break;

            float rowY = bodyY + (r * RowHeight) - scrollMod;
            for (int c = 0; c < colCount; c++)
            {
                float cx = gridX + c * colWidth;
                RenderCell(canvas, _source.Columns[c], rowIndex, cx + 8f, rowY + 19f);
            }
        }

        canvas.DrawPath(_gridLinesPath, gridLinePaint);

        // 3. Render Modern Scrollbar
        float trackW = 12f;
        float trackX = gridX + gridW - trackW;
        float trackY = bodyY;
        float trackH = bodyH;

        _scratchPath.Clear();
        _scratchPath.AddRect(trackX, trackY, trackW, trackH);
        canvas.FillPath(_scratchPath, _trackPaint);

        float viewRatio = trackH / MathF.Max(trackH, TotalRowCount * RowHeight);
        float thumbH = Math.Clamp(trackH * viewRatio, 24f, trackH);
        float scrollRatio = MaxScrollOffsetY > 0 ? Math.Clamp(ScrollOffsetY / MaxScrollOffsetY, 0f, 1f) : 0f;
        float thumbY = trackY + scrollRatio * (trackH - thumbH);

        var thumbColor = _isDraggingScrollbar ? Color4.GlacierBlue.ToRgba32() : new Rgba32(70, 95, 130, 220);
        _scratchPath.Clear();
        _scratchPath.AddRect(trackX + 2f, thumbY, trackW - 4f, thumbH);
        canvas.FillPath(_scratchPath, new Paint(thumbColor, PaintStyle.Fill));

        canvas.Restore();
    }

    private static readonly string[] s_intStrings = InitializeIntStrings();
    private static string[] InitializeIntStrings()
    {
        var arr = new string[1024];
        for (int i = 0; i < arr.Length; i++) arr[i] = i.ToString();
        return arr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawSpan(IGraphicsCanvas canvas, ReadOnlySpan<char> chars, float x, float y)
    {
        canvas.DrawText(chars, x, y, s_cellFont, _cellTextPaint);
    }

    private void RenderCell(IGraphicsCanvas canvas, ISeries col, long rowIndex, float x, float y)
    {
        if ((ulong)rowIndex >= (ulong)col.Length)
        {
            return;
        }
        int idx = (int)rowIndex;

        if (col.ValidityMask.IsNull(idx))
        {
            canvas.DrawText("null".AsSpan(), x, y, s_cellFont, _cellTextPaint);
            return;
        }

        Span<char> charBuffer = stackalloc char[64];
        int written;

        if (col is Series<int> intCol)
        {
            int intVal = intCol[idx];
            if ((uint)intVal < (uint)s_intStrings.Length)
            {
                canvas.DrawText(s_intStrings[intVal].AsSpan(), x, y, s_cellFont, _cellTextPaint);
                return;
            }
            if (intVal.TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<float> floatCol)
        {
            if (floatCol[idx].TryFormat(charBuffer, out written, "G6", CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<double> dblCol)
        {
            if (dblCol[idx].TryFormat(charBuffer, out written, "G6", CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<long> longCol)
        {
            if (longCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is CategoricalSeries catCol)
        {
            uint code = catCol[idx];
            string catText = code < (uint)catCol.RevMap.Length ? catCol.RevMap[code] : "Unknown";
            canvas.DrawText(catText.AsSpan(), x, y, s_cellFont, _cellTextPaint);
            return;
        }
        else if (col is Utf8StringSeries utf8Col)
        {
            ReadOnlySpan<byte> utf8 = utf8Col.GetStringSpan(idx);
            if (utf8.IsEmpty)
            {
                return;
            }

            int charCount = Encoding.UTF8.GetCharCount(utf8);
            char[]? rented = null;
            Span<char> decoded = charCount <= 256
                ? stackalloc char[256]
                : (rented = ArrayPool<char>.Shared.Rent(charCount));
            try
            {
                int decodedLen = Encoding.UTF8.GetChars(utf8, decoded);
                DrawSpan(canvas, decoded[..decodedLen], x, y);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
            return;
        }
        else if (col is Series<uint> uintCol)
        {
            if (uintCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<ulong> ulongCol)
        {
            if (ulongCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<bool> boolCol)
        {
            if (boolCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<sbyte> sbyteCol)
        {
            if (sbyteCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<byte> byteCol)
        {
            if (byteCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<short> shortCol)
        {
            if (shortCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<ushort> ushortCol)
        {
            if (ushortCol[idx].TryFormat(charBuffer, out written))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<decimal> decCol)
        {
            if (decCol[idx].TryFormat(charBuffer, out written, default, CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<DateTime> dtCol)
        {
            if (dtCol[idx].TryFormat(charBuffer, out written, "O", CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<DateTimeOffset> dtoCol)
        {
            if (dtoCol[idx].TryFormat(charBuffer, out written, "O", CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }
        else if (col is Series<TimeSpan> tsCol)
        {
            if (tsCol[idx].TryFormat(charBuffer, out written, "c", CultureInfo.InvariantCulture))
            {
                DrawSpan(canvas, charBuffer[..written], x, y);
                return;
            }
        }

        object? val = col.Get(idx);
        string fallbackText = val?.ToString() ?? "null";
        canvas.DrawText(fallbackText.AsSpan(), x, y, s_cellFont, _cellTextPaint);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source = null;
        OnRowSelected = null;
        _clipPath.Clear();
        _scratchPath.Clear();
        _evenRowsPath.Clear();
        _oddRowsPath.Clear();
        _gridLinesPath.Clear();
    }
}
