namespace Glacier.Desktop.Windowing;

using System;
using Glacier.Desktop.UI;

/// <summary>
/// Hardware GPU or software renderer contract for desktop windows.
/// </summary>
public interface IWindowRenderer : IDisposable
{
    void BeginFrame();
    void EndFrame();
    void RenderTree(VisualNode root, float width, float height);
}
