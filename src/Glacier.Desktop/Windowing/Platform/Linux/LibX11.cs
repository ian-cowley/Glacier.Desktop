namespace Glacier.Desktop.Windowing.Platform.Linux;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class LibX11
{
    private const string DllName = "libX11.so.6";

    public const int ExposureMask = 1 << 15;
    public const int KeyPressMask = 1 << 0;
    public const int KeyReleaseMask = 1 << 1;
    public const int ButtonPressMask = 1 << 2;
    public const int ButtonReleaseMask = 1 << 3;
    public const int PointerMotionMask = 1 << 6;
    public const int StructureNotifyMask = 1 << 17;

    public const int Expose = 12;
    public const int KeyPress = 2;
    public const int KeyRelease = 3;
    public const int ButtonPress = 4;
    public const int ButtonRelease = 5;
    public const int MotionNotify = 6;
    public const int ConfigureNotify = 22;
    public const int ClientMessage = 33;
    public const int DestroyNotify = 17;

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct XEvent
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(0)]
        public XAnyEvent xany;

        [FieldOffset(0)]
        public XKeyEvent xkey;

        [FieldOffset(0)]
        public XButtonEvent xbutton;

        [FieldOffset(0)]
        public XMotionEvent xmotion;

        [FieldOffset(0)]
        public XConfigureEvent xconfigure;

        [FieldOffset(0)]
        public XClientMessageEvent xclient;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XAnyEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XKeyEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public UIntPtr time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint keycode;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XButtonEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public UIntPtr time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint button;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XMotionEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public UIntPtr time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public byte is_hint;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XConfigureEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr event_window;
        public IntPtr window;
        public int x, y;
        public int width, height;
        public int border_width;
        public IntPtr above;
        public int override_redirect;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XClientMessageEvent
    {
        public int type;
        public UIntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr message_type;
        public int format;
        public IntPtr ptr0;
        public IntPtr ptr1;
        public IntPtr ptr2;
        public IntPtr ptr3;
        public IntPtr ptr4;
    }

    [DllImport(DllName)]
    public static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport(DllName)]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport(DllName)]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(DllName)]
    public static extern int XDefaultScreen(IntPtr display);

    [DllImport(DllName)]
    public static extern IntPtr XCreateSimpleWindow(
        IntPtr display,
        IntPtr parent,
        int x, int y,
        uint width, uint height,
        uint borderWidth,
        UIntPtr border,
        UIntPtr background);

    [DllImport(DllName)]
    public static extern int XDestroyWindow(IntPtr display, IntPtr window);

    [DllImport(DllName)]
    public static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport(DllName)]
    public static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport(DllName)]
    public static extern int XStoreName(IntPtr display, IntPtr window, string windowName);

    [DllImport(DllName)]
    public static extern int XSelectInput(IntPtr display, IntPtr window, IntPtr eventMask);

    [DllImport(DllName)]
    public static extern int XPending(IntPtr display);

    [DllImport(DllName)]
    public static extern int XNextEvent(IntPtr display, out XEvent event_return);

    [DllImport(DllName)]
    public static extern IntPtr XInternAtom(IntPtr display, string atom_name, [MarshalAs(UnmanagedType.Bool)] bool only_if_exists);

    [DllImport(DllName)]
    public static extern int XResizeWindow(IntPtr display, IntPtr window, uint width, uint height);

    [DllImport(DllName)]
    public static extern int XSetWMProtocols(IntPtr display, IntPtr window, ref IntPtr protocols, int count);
}
