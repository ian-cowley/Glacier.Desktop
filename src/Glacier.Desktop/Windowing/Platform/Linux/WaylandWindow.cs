namespace Glacier.Desktop.Windowing.Platform.Linux;

using System;
using System.IO;
using System.Net.Sockets;
using Glacier.Desktop.Windowing.Swapchain;

/// <summary>
/// Pure C# Wayland client window communicating directly with the compositor UNIX domain socket.
/// </summary>
public sealed class WaylandWindow : IWindow
{
    private Socket? _socket;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;
    private uint _nextObjectId = 2;

    public static bool IsSupported => OperatingSystem.IsLinux();

    public IntPtr NativeHandle => _socket is { Connected: true } sock ? sock.SafeHandle.DangerousGetHandle() : IntPtr.Zero;

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            Resized?.Invoke(value.Width, value.Height);
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            SendSetTitle(value);
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public WaylandWindow(WindowOptions options)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("WaylandWindow is only supported on Linux.");
        }
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;

        ConnectCompositor();
    }

    private void ConnectCompositor()
    {
        string? socketName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (string.IsNullOrEmpty(socketName))
        {
            socketName = "wayland-0";
        }

        string? runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        string socketPath = Path.IsPathRooted(socketName)
            ? socketName
            : (!string.IsNullOrEmpty(runtimeDir) ? Path.Combine(runtimeDir, socketName) : string.Empty);

        if (string.IsNullOrEmpty(socketPath) || !File.Exists(socketPath))
        {
            throw new InvalidOperationException($"Wayland compositor UNIX socket was not found at '{socketPath}'.");
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            socket.Blocking = false;
            _socket = socket;
            SendGetRegistry();
        }
        catch (Exception ex)
        {
            socket.Dispose();
            throw new InvalidOperationException($"Failed to connect to Wayland compositor at '{socketPath}'.", ex);
        }
    }

    private void SendGetRegistry()
    {
        if (_socket is null) return;

        Span<byte> buffer = stackalloc byte[12];
        WaylandProtocol.WriteHeader(buffer, WaylandProtocol.DisplayObjectId, WaylandProtocol.DisplayGetRegistry, 12);
        uint registryId = _nextObjectId++;
        BitConverter.TryWriteBytes(buffer[8..12], registryId);

        try
        {
            _socket.Send(buffer);
        }
        catch
        {
            // Non-fatal socket write failure
        }
    }

    private void SendSetTitle(string title)
    {
        if (_socket is null) return;

        Span<byte> titleBuffer = stackalloc byte[512];
        string boundedTitle = title.Length > 120 ? title[..120] : title;
        int encodedLen = WaylandProtocol.EncodeString(titleBuffer, boundedTitle);
        ushort totalSize = (ushort)(8 + encodedLen);

        Span<byte> packet = stackalloc byte[totalSize];
        WaylandProtocol.WriteHeader(packet, 3, WaylandProtocol.XdgToplevelSetTitle, totalSize);
        titleBuffer[..encodedLen].CopyTo(packet[8..]);

        try
        {
            _socket.Send(packet);
        }
        catch
        {
            // Non-fatal socket write failure
        }
    }

    public void PollEvents()
    {
        if (_disposed || _socket is null) return;

        Span<byte> buffer = stackalloc byte[1024];
        try
        {
            int received = _socket.Receive(buffer, SocketFlags.None);
            if (received >= 8)
            {
                int offset = 0;
                while (offset + 8 <= received)
                {
                    var (_, _, size) = WaylandProtocol.ReadHeader(buffer[offset..]);
                    if (size < 8 || offset + size > received) break;
                    offset += size;
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            // Non-blocking read empty
        }
        catch
        {
            // Compositor disconnected
        }
    }

    /// <summary>
    /// Dispatches an input event directly into the window input listeners.
    /// </summary>
    public void DispatchInput(in InputEvent evt)
    {
        InputReceived?.Invoke(evt);
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Closing?.Invoke();
        _socket?.Dispose();
        _socket = null;

        GC.SuppressFinalize(this);
    }
}
