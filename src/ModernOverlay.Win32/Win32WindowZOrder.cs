namespace ModernOverlay.Win32;

public static class Win32WindowZOrder
{
    public static void MakeTopmost(nint hwnd)
    {
        SetZOrder(hwnd, NativeMethods.HwndTopMost, "SetWindowPos(make topmost)");
    }

    public static void RemoveTopmost(nint hwnd)
    {
        SetZOrder(hwnd, NativeMethods.HwndNoTopMost, "SetWindowPos(remove topmost)");
    }

    public static void PlaceAbove(nint hwnd, nint hwndInsertAfter)
    {
        if (!Win32WindowQuery.IsWindow(hwndInsertAfter))
        {
            throw new ArgumentException("The relative z-order HWND must be a valid window.", nameof(hwndInsertAfter));
        }

        SetZOrder(hwnd, hwndInsertAfter, "SetWindowPos(place above)");
    }

    private static void SetZOrder(nint hwnd, nint insertAfter, string operation)
    {
        if (!Win32WindowQuery.IsWindow(hwnd))
        {
            throw new ArgumentException("The HWND must be a valid window.", nameof(hwnd));
        }

        if (!NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate))
        {
            throw new NativeWin32Exception(operation);
        }
    }
}
