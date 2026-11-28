namespace Glacier.Desktop.Windowing.Platform.Mac;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class LibObjC
{
    private const string DllName = "/usr/lib/libobjc.A.dylib";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string QuartzCoreFramework = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NSRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }

    public static void EnsureFrameworksLoaded()
    {
        NativeLibrary.TryLoad(AppKitFramework, out _);
        NativeLibrary.TryLoad(QuartzCoreFramework, out _);
    }

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr objc_getClass(string name);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr sel_registerName(string name);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial IntPtr objc_msgSend(IntPtr self, IntPtr op);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial IntPtr objc_msgSend(IntPtr self, IntPtr op, IntPtr arg1);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial IntPtr objc_msgSend(IntPtr self, IntPtr op, nuint arg1);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial void objc_msgSend_CGSize(IntPtr self, IntPtr op, CGSize size);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial IntPtr objc_msgSend_InitWindow(
        IntPtr self,
        IntPtr op,
        NSRect contentRect,
        nuint styleMask,
        nuint backing,
        byte defer);

    [LibraryImport(DllName, EntryPoint = "objc_msgSend")]
    public static partial IntPtr objc_msgSend(IntPtr self, IntPtr op, byte arg1);
}
