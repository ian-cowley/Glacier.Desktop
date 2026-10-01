namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.Layout;
using Glacier.Desktop.UI;
using Glacier.Desktop.UI.Controls;
using Glacier.Desktop.Grids;
using Glacier.Windowing;

/// <summary>
/// Native desktop window orchestrator managing the visual tree, layout calculations, and GPU frames.
/// Directly bridges with Glacier.Windowing native message loops to eliminate UI input polling jitter.
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
    public IWindow? NativeWindow { get; private set; }
    public bool IsOpen => _isOpen;
    public long FrameCount { get; private set; }
    public ulong InputDispatchedCount { get; private set; }

    public event Action? Opened;
    public event Action? Closed;
    public event Action<InputEvent>? InputReceived;

    public GlacierWindow(string title = "Glacier Desktop", int width = 1280, int height = 800, IWindowRenderer? renderer = null, IWindow? nativeWindow = null)
    {
        Title = title;
        Width = width;
        Height = height;
        Renderer = renderer ?? new HeadlessWindowRenderer(width, height);
        if (nativeWindow != null)
        {
            AttachNativeWindow(nativeWindow);
        }
    }

    public void AttachNativeWindow(IWindow window)
    {
        NativeWindow = window;
        NativeWindow.InputReceived += OnNativeInput;
        NativeWindow.Resized += (w, h) =>
        {
            Width = w;
            Height = h;
            RenderFrame();
        };
        NativeWindow.Closing += Close;
    }

    private void OnNativeInput(InputEvent e)
    {
        InputDispatchedCount++;
        InputReceived?.Invoke(e);

        if (RootVisual != null)
        {
            switch (e.Type)
            {
                case InputEventType.MouseDown:
                    var hit = RootVisual.HitTest(e.X, e.Y);
                    if (hit is DesktopButton btn)
                    {
                        btn.PerformClick();
                    }
                    else if (hit is VirtualDataGrid grid)
                    {
                        grid.HandleMouseDown(e.X, e.Y);
                    }
                    break;
                case InputEventType.MouseWheel:
                    if (RootVisual is VirtualDataGrid vdg)
                    {
                        vdg.ScrollBy(-e.Y);
                    }
                    break;
            }
        }
    }

    public void PollEvents()
    {
        NativeWindow?.PollEvents();
    }

    public void Show()
    {
        _isOpen = true;
        Opened?.Invoke();
        RenderFrame();
    }

    public void Step(float dt)
    {
        PollEvents();
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
            NativeWindow?.Dispose();
            Renderer.Dispose();
            _disposed = true;
        }
    }
}
