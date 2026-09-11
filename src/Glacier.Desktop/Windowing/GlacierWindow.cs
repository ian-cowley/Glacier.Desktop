namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;

/// <summary>
/// Native desktop window orchestrator managing the visual tree, layout calculations, and GPU frames.
/// </summary>
public class GlacierWindow : IDisposable
{
    private bool _disposed;
    private bool _isOpen;

    public string Title { get; set; } = "Glacier Desktop";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 800;
    public VisualNode? RootVisual { get; set; }
    public IWindowRenderer Renderer { get; private set; }
    public bool IsOpen => _isOpen;
    public long FrameCount { get; private set; }

    public event Action? Opened;
    public event Action? Closed;

    public GlacierWindow(string title = "Glacier Desktop", int width = 1280, int height = 800, IWindowRenderer? renderer = null)
    {
        Title = title;
        Width = width;
        Height = height;
        Renderer = renderer ?? new HeadlessWindowRenderer(width, height);
    }

    public void Show()
    {
        _isOpen = true;
        Opened?.Invoke();
        RenderFrame();
    }

    public void Step(float dt)
    {
        FrameCount++;
        RenderFrame();
    }

    public void RenderFrame()
    {
        if (!_isOpen || RootVisual == null) return;

        Renderer.BeginFrame();
        Renderer.RenderTree(RootVisual, Width, Height);
        Renderer.EndFrame();
    }

    public void Close()
    {
        _isOpen = false;
        Closed?.Invoke();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            Renderer.Dispose();
            _disposed = true;
        }
    }
}
