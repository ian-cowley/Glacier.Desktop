namespace Glacier.Desktop.Windowing.Platform.Win32;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class Dwmapi
{
    private const string DllName = "dwmapi.dll";

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [DllImport(DllName, PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, void* pvAttribute, uint cbAttribute);

    [DllImport(DllName, PreserveSig = true)]
    public static extern int DwmFlush();
}

internal static unsafe partial class Kernel32
{
    private const string DllName = "kernel32.dll";

    [DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport(DllName)]
    public static extern uint GetCurrentThreadId();

    [DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateEventW(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName);

    [DllImport(DllName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetEvent(IntPtr hEvent);

    [DllImport(DllName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceFrequency(out long lpFrequency);
}

internal static unsafe partial class Gdi32
{
    private const string DllName = "gdi32.dll";

    public const uint BI_RGB = 0;
    public const uint DIB_RGB_COLORS = 0;
    public const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport(DllName, ExactSpelling = true)]
    public static extern int StretchDIBits(
        IntPtr hdc,
        int xDest,
        int yDest,
        int DestWidth,
        int DestHeight,
        int xSrc,
        int ySrc,
        int SrcWidth,
        int SrcHeight,
        void* lpBits,
        BITMAPINFO* lpbmi,
        uint iUsage,
        uint rop);

    /// <summary>
    /// Blits an unmanaged RGBA8888 pixel buffer to the specified Win32 HWND client area,
    /// converting RGBA to top-down BGRA32 into the provided scratch buffer.
    /// </summary>
    public static void PresentRgbaToHwnd(IntPtr hwnd, void* rgbaSource, void* bgraScratch, int width, int height)
    {
        if (hwnd == IntPtr.Zero || rgbaSource == null || bgraScratch == null || width <= 0 || height <= 0)
        {
            return;
        }

        if (!User32.IsWindow(hwnd))
        {
            return;
        }

        int pixelCount = width * height;
        uint* src = (uint*)rgbaSource;
        uint* dst = (uint*)bgraScratch;

        // Swizzle little-endian 0xAABBGGRR (RGBA byte order) -> 0xAARRGGBB (BGRA byte order for Win32 DIB)
        for (int i = 0; i < pixelCount; i++)
        {
            uint px = src[i];
            dst[i] = (px & 0xFF00FF00u) | ((px & 0x000000FFu) << 16) | ((px & 0x00FF0000u) >> 16);
        }

        IntPtr hdc = User32.GetDC(hwnd);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            int destWidth = width;
            int destHeight = height;
            if (User32.GetClientRect(hwnd, out User32.RECT clientRect) && clientRect.Width > 0 && clientRect.Height > 0)
            {
                destWidth = clientRect.Width;
                destHeight = clientRect.Height;
            }

            BITMAPINFO bmi = default;
            bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Negative height = top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            StretchDIBits(
                hdc,
                0, 0, destWidth, destHeight,
                0, 0, width, height,
                bgraScratch,
                &bmi,
                DIB_RGB_COLORS,
                SRCCOPY);
        }
        finally
        {
            User32.ReleaseDC(hwnd, hdc);
        }
    }
}
