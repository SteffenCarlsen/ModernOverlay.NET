namespace ModernOverlay.Win32;

public readonly record struct Win32DpiScale(float X, float Y)
{
    public static Win32DpiScale Default => new(1f, 1f);
}

public static class Win32DpiQuery
{
    public static bool TrySetProcessPerMonitorV2()
        => NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);

    public static Win32DpiScale GetScaleForWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return Win32DpiScale.Default;
        }

        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi == 0
            ? Win32DpiScale.Default
            : new Win32DpiScale(dpi / (float)NativeMethods.DefaultDpi, dpi / (float)NativeMethods.DefaultDpi);
    }
}
