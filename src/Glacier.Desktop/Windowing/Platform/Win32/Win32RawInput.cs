namespace Glacier.Desktop.Windowing.Platform.Win32;

using System;
using System.Buffers;
using System.Runtime.InteropServices;

internal static unsafe class Win32RawInput
{
    public static bool Register(IntPtr hWnd)
    {
        Span<User32.RAWINPUTDEVICE> devices = stackalloc User32.RAWINPUTDEVICE[2];

        // Mouse: Generic Desktop (0x01), Mouse (0x02)
        devices[0] = new User32.RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x02,
            dwFlags = 0,
            hwndTarget = hWnd
        };

        // Keyboard: Generic Desktop (0x01), Keyboard (0x06)
        devices[1] = new User32.RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x06,
            dwFlags = 0,
            hwndTarget = hWnd
        };

        fixed (User32.RAWINPUTDEVICE* ptr = devices)
        {
            return User32.RegisterRawInputDevices(ptr, (uint)devices.Length, (uint)sizeof(User32.RAWINPUTDEVICE));
        }
    }

    public static void Process(IntPtr hWnd, IntPtr lParam, Action<InputEvent>? callback, ulong timestampNs)
    {
        if (callback is null)
        {
            return;
        }

        uint size = 0;
        User32.GetRawInputData(lParam, User32.RID_INPUT, null, ref size, (uint)sizeof(User32.RAWINPUTHEADER));
        if (size == 0 || size > 65536)
        {
            return;
        }

        byte[]? rented = null;
        Span<byte> storage = size <= 1024
            ? stackalloc byte[(int)size]
            : (rented = ArrayPool<byte>.Shared.Rent((int)size)).AsSpan(0, (int)size);

        try
        {
            fixed (byte* buffer = storage)
            {
                if (User32.GetRawInputData(lParam, User32.RID_INPUT, buffer, ref size, (uint)sizeof(User32.RAWINPUTHEADER)) != size)
                {
                    return;
                }

                User32.RAWINPUT* raw = (User32.RAWINPUT*)buffer;

                if (raw->header.dwType == User32.RIM_TYPEMOUSE)
                {
                    ref readonly User32.RAWMOUSE mouse = ref raw->data.mouse;

                    float clientX = 0f;
                    float clientY = 0f;
                    if (hWnd != IntPtr.Zero && User32.GetCursorPos(out User32.POINT cursorPt))
                    {
                        User32.ScreenToClient(hWnd, ref cursorPt);
                        clientX = cursorPt.x;
                        clientY = cursorPt.y;
                    }

                    if ((mouse.usFlags & User32.MOUSE_MOVE_ABSOLUTE) != 0)
                    {
                        bool virtualDesktop = (mouse.usFlags & User32.MOUSE_VIRTUAL_DESKTOP) != 0;
                        int originX = virtualDesktop ? User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN) : 0;
                        int originY = virtualDesktop ? User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN) : 0;
                        int screenW = User32.GetSystemMetrics(virtualDesktop ? User32.SM_CXVIRTUALSCREEN : User32.SM_CXSCREEN);
                        int screenH = User32.GetSystemMetrics(virtualDesktop ? User32.SM_CYVIRTUALSCREEN : User32.SM_CYSCREEN);

                        User32.POINT absPt = new()
                        {
                            x = originX + (int)((long)mouse.lLastX * Math.Max(1, screenW) / 65535L),
                            y = originY + (int)((long)mouse.lLastY * Math.Max(1, screenH) / 65535L)
                        };
                        if (hWnd != IntPtr.Zero)
                        {
                            User32.ScreenToClient(hWnd, ref absPt);
                        }
                        clientX = absPt.x;
                        clientY = absPt.y;
                        callback(new InputEvent(InputEventType.MouseMove, 0, clientX, clientY, timestampNs));
                    }
                    else if (mouse.lLastX != 0 || mouse.lLastY != 0)
                    {
                        callback(new InputEvent(InputEventType.MouseDelta, 0, mouse.lLastX, mouse.lLastY, timestampNs));
                        if (hWnd != IntPtr.Zero)
                        {
                            callback(new InputEvent(InputEventType.MouseMove, 0, clientX, clientY, timestampNs));
                        }
                    }

                    ushort btnFlags = mouse.usButtonFlags;
                    if ((btnFlags & 0x0001) != 0) // RI_MOUSE_LEFT_BUTTON_DOWN
                    {
                        callback(new InputEvent(InputEventType.MouseDown, 0, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0002) != 0) // RI_MOUSE_LEFT_BUTTON_UP
                    {
                        callback(new InputEvent(InputEventType.MouseUp, 0, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0004) != 0) // RI_MOUSE_RIGHT_BUTTON_DOWN
                    {
                        callback(new InputEvent(InputEventType.MouseDown, 1, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0008) != 0) // RI_MOUSE_RIGHT_BUTTON_UP
                    {
                        callback(new InputEvent(InputEventType.MouseUp, 1, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0010) != 0) // RI_MOUSE_MIDDLE_BUTTON_DOWN
                    {
                        callback(new InputEvent(InputEventType.MouseDown, 2, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0020) != 0) // RI_MOUSE_MIDDLE_BUTTON_UP
                    {
                        callback(new InputEvent(InputEventType.MouseUp, 2, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0040) != 0) // RI_MOUSE_BUTTON_4_DOWN (XButton1)
                    {
                        callback(new InputEvent(InputEventType.MouseDown, 3, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0080) != 0) // RI_MOUSE_BUTTON_4_UP
                    {
                        callback(new InputEvent(InputEventType.MouseUp, 3, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0100) != 0) // RI_MOUSE_BUTTON_5_DOWN (XButton2)
                    {
                        callback(new InputEvent(InputEventType.MouseDown, 4, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0200) != 0) // RI_MOUSE_BUTTON_5_UP
                    {
                        callback(new InputEvent(InputEventType.MouseUp, 4, clientX, clientY, timestampNs));
                    }
                    if ((btnFlags & 0x0400) != 0) // RI_MOUSE_WHEEL
                    {
                        short wheelDelta = (short)mouse.usButtonData;
                        callback(new InputEvent(InputEventType.MouseWheel, 0, 0, wheelDelta, timestampNs));
                    }
                }
                else if (raw->header.dwType == User32.RIM_TYPEKEYBOARD)
                {
                    ref readonly User32.RAWKEYBOARD kb = ref raw->data.keyboard;
                    if (kb.VKey < 256)
                    {
                        bool isUp = (kb.Flags & 1) != 0;
                        InputEventType type = isUp ? InputEventType.KeyUp : InputEventType.KeyDown;
                        callback(new InputEvent(type, kb.VKey, 0, 0, timestampNs));
                    }
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
