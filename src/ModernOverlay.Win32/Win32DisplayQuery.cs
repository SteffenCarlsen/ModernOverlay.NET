namespace ModernOverlay.Win32;

public static class Win32DisplayQuery
{
    private const int MinimumPlausibleRefreshRate = 24;
    private const int MaximumPlausibleRefreshRate = 1000;

    public static bool TryGetRefreshRateForWindow(nint hwnd, out int framesPerSecond)
    {
        framesPerSecond = 0;
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        nint monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor != 0)
        {
            var monitorInfo = new NativeMethods.MonitorInfoEx
            {
                Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
            };

            if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)
                && TryGetRefreshRateFromDeviceName(monitorInfo.DeviceName, out framesPerSecond))
            {
                return true;
            }
        }

        return TryGetRefreshRateFromWindowDc(hwnd, out framesPerSecond);
    }

    private static bool TryGetRefreshRateFromDeviceName(string? deviceName, out int framesPerSecond)
    {
        framesPerSecond = 0;
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        nint dc = NativeMethods.CreateDC("DISPLAY", deviceName, null, 0);
        if (dc == 0)
        {
            return false;
        }

        try
        {
            return TryReadRefreshRate(dc, out framesPerSecond);
        }
        finally
        {
            _ = NativeMethods.DeleteDC(dc);
        }
    }

    private static bool TryGetRefreshRateFromWindowDc(nint hwnd, out int framesPerSecond)
    {
        framesPerSecond = 0;
        nint dc = NativeMethods.GetDC(hwnd);
        if (dc == 0)
        {
            return false;
        }

        try
        {
            return TryReadRefreshRate(dc, out framesPerSecond);
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(hwnd, dc);
        }
    }

    private static bool TryReadRefreshRate(nint dc, out int framesPerSecond)
    {
        int refreshRate = NativeMethods.GetDeviceCaps(dc, NativeMethods.VertRefresh);
        if (refreshRate is < MinimumPlausibleRefreshRate or > MaximumPlausibleRefreshRate)
        {
            framesPerSecond = 0;
            return false;
        }

        framesPerSecond = refreshRate;
        return true;
    }
}
