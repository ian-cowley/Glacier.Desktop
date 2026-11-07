namespace Glacier.Desktop.Windowing.Platform.Linux;

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Binary message wire framing for the pure C# Wayland protocol client.
/// </summary>
public static class WaylandProtocol
{
    public const uint DisplayObjectId = 1;

    // wl_display opcodes
    public const ushort DisplaySync = 0;
    public const ushort DisplayGetRegistry = 1;

    // wl_registry opcodes
    public const ushort RegistryBind = 0;

    // xdg_wm_base opcodes
    public const ushort XdgWmBaseDestroy = 0;
    public const ushort XdgWmBaseCreatePositioner = 1;
    public const ushort XdgWmBaseGetXdgSurface = 2;
    public const ushort XdgWmBasePong = 3;

    // xdg_surface opcodes
    public const ushort XdgSurfaceDestroy = 0;
    public const ushort XdgSurfaceGetToplevel = 1;
    public const ushort XdgSurfaceAckConfigure = 4;

    // xdg_toplevel opcodes
    public const ushort XdgToplevelDestroy = 0;
    public const ushort XdgToplevelSetTitle = 2;
    public const ushort XdgToplevelSetAppId = 3;

    /// <summary>
    /// Writes an 8-byte Wayland message header to a byte buffer.
    /// </summary>
    public static void WriteHeader(Span<byte> buffer, uint objectId, ushort opcode, ushort size)
    {
        BitConverter.TryWriteBytes(buffer[0..4], objectId);
        BitConverter.TryWriteBytes(buffer[4..6], opcode);
        BitConverter.TryWriteBytes(buffer[6..8], size);
    }

    /// <summary>
    /// Parses an 8-byte Wayland message header from a byte buffer.
    /// </summary>
    public static (uint ObjectId, ushort Opcode, ushort Size) ReadHeader(ReadOnlySpan<byte> buffer)
    {
        uint objectId = BitConverter.ToUInt32(buffer[0..4]);
        ushort opcode = BitConverter.ToUInt16(buffer[4..6]);
        ushort size = BitConverter.ToUInt16(buffer[6..8]);
        return (objectId, opcode, size);
    }

    /// <summary>
    /// Encodes a UTF-8 string with length prefix and 4-byte padding according to Wayland wire specification.
    /// </summary>
    public static int EncodeString(Span<byte> destination, string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        uint lenWithNull = (uint)utf8.Length + 1;
        BitConverter.TryWriteBytes(destination[0..4], lenWithNull);

        utf8.CopyTo(destination[4..]);
        destination[4 + utf8.Length] = 0; // Null terminator

        int totalBytes = 4 + (int)lenWithNull;
        int paddedBytes = (totalBytes + 3) & ~3;

        // Clear padding bytes
        for (int i = totalBytes; i < paddedBytes; i++)
        {
            destination[i] = 0;
        }

        return paddedBytes;
    }
}
