namespace ModernOverlay.Win32;

public enum Win32WindowDisplayAffinity
{
    None = 0,
    Monitor = 1,
    ExcludeFromCapture = 0x11,
}

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

    public static void SetDisplayAffinity(nint hwnd, Win32WindowDisplayAffinity affinity)
    {
        if (!Win32WindowQuery.IsWindow(hwnd))
        {
            throw new ArgumentException("The HWND must be a valid window.", nameof(hwnd));
        }

        uint nativeAffinity = ToNativeDisplayAffinity(affinity);
        if (!NativeMethods.SetWindowDisplayAffinity(hwnd, nativeAffinity))
        {
            throw new NativeWin32Exception("SetWindowDisplayAffinity");
        }
    }

    public static Win32WindowDisplayAffinity GetDisplayAffinity(nint hwnd)
    {
        return !Win32WindowQuery.IsWindow(hwnd)
            ? throw new ArgumentException("The HWND must be a valid window.", nameof(hwnd))
            : NativeMethods.GetWindowDisplayAffinity(hwnd, out uint nativeAffinity)
                ? FromNativeDisplayAffinity(nativeAffinity)
                : throw new NativeWin32Exception("GetWindowDisplayAffinity");
    }

    public static void ExcludeFromCapture(nint hwnd) => SetDisplayAffinity(hwnd, Win32WindowDisplayAffinity.ExcludeFromCapture);

    public static void ClearDisplayAffinity(nint hwnd) => SetDisplayAffinity(hwnd, Win32WindowDisplayAffinity.None);

    private static uint ToNativeDisplayAffinity(Win32WindowDisplayAffinity affinity)
        => affinity switch
        {
            Win32WindowDisplayAffinity.None => NativeMethods.WdaNone,
            Win32WindowDisplayAffinity.Monitor => NativeMethods.WdaMonitor,
            Win32WindowDisplayAffinity.ExcludeFromCapture => NativeMethods.WdaExcludeFromCapture,
            _ => throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Unsupported display affinity."),
        };

    private static Win32WindowDisplayAffinity FromNativeDisplayAffinity(uint affinity)
        => affinity switch
        {
            NativeMethods.WdaNone => Win32WindowDisplayAffinity.None,
            NativeMethods.WdaMonitor => Win32WindowDisplayAffinity.Monitor,
            NativeMethods.WdaExcludeFromCapture => Win32WindowDisplayAffinity.ExcludeFromCapture,
            _ => throw new InvalidOperationException($"Unsupported display affinity value: 0x{affinity:X}."),
        };
}
