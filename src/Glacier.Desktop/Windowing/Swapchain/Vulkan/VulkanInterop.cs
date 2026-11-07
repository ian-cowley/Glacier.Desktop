namespace Glacier.Desktop.Windowing.Swapchain.Vulkan;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class VulkanInterop
{
    public const int VK_SUCCESS = 0;
    public const int VK_NOT_READY = 1;
    public const int VK_TIMEOUT = 2;
    public const int VK_SUBOPTIMAL_KHR = 1000001003;
    public const int VK_ERROR_OUT_OF_DATE_KHR = -1000001004;

    public const int VK_STRUCTURE_TYPE_APPLICATION_INFO = 0;
    public const int VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1;

    public const int VK_FORMAT_R8G8B8A8_UNORM = 37;
    public const int VK_FORMAT_B8G8R8A8_UNORM = 44;
    public const int VK_FORMAT_A2B10G10R10_UNORM_PACK32 = 64;
    public const int VK_FORMAT_R16G16B16A16_SFLOAT = 97;

    public const int VK_COLOR_SPACE_SRGB_NONLINEAR_KHR = 0;
    public const int VK_COLOR_SPACE_EXTENDED_SRGB_LINEAR_EXT = 1000104000;
    public const int VK_COLOR_SPACE_HDR10_ST2084_EXT = 1000104008;

    public const int VK_PRESENT_MODE_IMMEDIATE_KHR = 0;
    public const int VK_PRESENT_MODE_MAILBOX_KHR = 1;
    public const int VK_PRESENT_MODE_FIFO_KHR = 2;
    public const int VK_PRESENT_MODE_FIFO_RELAXED_KHR = 3;

    public const uint VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT = 0x00000010;
    public const uint VK_IMAGE_USAGE_TRANSFER_DST_BIT = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct VkApplicationInfo
    {
        public int sType;
        public IntPtr pNext;
        public byte* pApplicationName;
        public uint applicationVersion;
        public byte* pEngineName;
        public uint engineVersion;
        public uint apiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkInstanceCreateInfo
    {
        public int sType;
        public IntPtr pNext;
        public uint flags;
        public VkApplicationInfo* pApplicationInfo;
        public uint enabledLayerCount;
        public byte** ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public byte** ppEnabledExtensionNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkExtent2D
    {
        public uint width;
        public uint height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSwapchainCreateInfoKHR
    {
        public int sType;
        public IntPtr pNext;
        public uint flags;
        public IntPtr surface;
        public uint minImageCount;
        public int imageFormat;
        public int imageColorSpace;
        public VkExtent2D imageExtent;
        public uint imageArrayLayers;
        public uint imageUsage;
        public int imageSharingMode;
        public uint queueFamilyIndexCount;
        public IntPtr pQueueFamilyIndices;
        public int preTransform;
        public int compositeAlpha;
        public int presentMode;
        public uint clipped;
        public IntPtr oldSwapchain;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkPresentInfoKHR
    {
        public int sType;
        public IntPtr pNext;
        public uint waitSemaphoreCount;
        public IntPtr pWaitSemaphores;
        public uint swapchainCount;
        public IntPtr pSwapchains;
        public IntPtr pImageIndices;
        public IntPtr pResults;
    }

    public static bool TryLoadVulkanLoader(out IntPtr libraryHandle)
    {
        ReadOnlySpan<string> candidates = OperatingSystem.IsWindows()
            ? ["vulkan-1.dll"]
            : OperatingSystem.IsMacOS()
                ? ["libvulkan.1.dylib", "libMoltenVK.dylib"]
                : ["libvulkan.so.1", "libvulkan.so"];

        for (int i = 0; i < candidates.Length; i++)
        {
            if (NativeLibrary.TryLoad(candidates[i], out libraryHandle))
            {
                return true;
            }
        }

        libraryHandle = IntPtr.Zero;
        return false;
    }
}
