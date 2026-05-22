namespace ModernOverlay.Win32;

public static class Win32WindowEffects
{
    public static void ExtendFrameIntoClientArea(nint hwnd)
    {
        if (!Win32WindowQuery.IsWindow(hwnd))
        {
            throw new ArgumentException("The HWND must be a valid window.", nameof(hwnd));
        }

        var margins = new NativeMethods.Margins
        {
            LeftWidth = -1,
            RightWidth = -1,
            TopHeight = -1,
            BottomHeight = -1,
        };

        int hr = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, margins);
        if (hr < 0)
        {
            throw new NativeHResultException("DwmExtendFrameIntoClientArea", hr);
        }
    }

    public static void EnableBlurBehind(nint hwnd, bool enabled = true)
    {
        if (!Win32WindowQuery.IsWindow(hwnd))
        {
            throw new ArgumentException("The HWND must be a valid window.", nameof(hwnd));
        }

        var blurBehind = new NativeMethods.DwmBlurBehind
        {
            Flags = NativeMethods.DwmBbEnable,
            Enable = enabled,
        };

        int hr = NativeMethods.DwmEnableBlurBehindWindow(hwnd, blurBehind);
        if (hr < 0)
        {
            throw new NativeHResultException("DwmEnableBlurBehindWindow", hr);
        }
    }
}
