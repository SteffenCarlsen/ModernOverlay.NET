using System.ComponentModel;
using ModernOverlay.Win32;

namespace ModernOverlay.Windows;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class WindowsNamespace
{
}

public readonly record struct WindowStylesSnapshot(nint Style, nint ExtendedStyle);

public static class WindowQuery
{
    public static bool IsWindow(WindowHandle hwnd) => Win32WindowQuery.IsWindow(hwnd.Value);

    public static bool IsVisible(WindowHandle hwnd) => Win32WindowQuery.IsVisible(hwnd.Value);

    public static bool TryFindWindow(string? className, string? title, out WindowHandle hwnd)
        => TryFindWindow(className, title, MatchMode.Exact, out hwnd);

    public static bool TryFindWindow(string? className, string? title, MatchMode titleMatchMode, out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryFindWindow(className, title, ToWin32MatchMode(titleMatchMode), out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryFindWindowByTitle(string title, out WindowHandle hwnd)
        => TryFindWindowByTitle(title, MatchMode.Exact, out hwnd);

    public static bool TryFindWindowByTitle(string title, MatchMode matchMode, out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryFindWindowByTitle(title, ToWin32MatchMode(matchMode), out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryFindWindowByClassName(string className, out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryFindWindowByClassName(className, out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryFindWindowByProcessName(string processName, out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryFindWindowByProcessName(processName, out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryFindWindowByProcessId(int processId, out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryFindWindowByProcessId(processId, out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryFindChildWindow(WindowHandle parent, string? className, string? title, out WindowHandle hwnd)
        => TryFindChildWindow(parent, className, title, MatchMode.Exact, out hwnd);

    public static bool TryFindChildWindow(WindowHandle parent, string? className, string? title, MatchMode titleMatchMode, out WindowHandle hwnd)
        => FromNativeResult(
            Win32WindowQuery.TryFindChildWindow(parent.Value, className, title, ToWin32MatchMode(titleMatchMode), out nint nativeHwnd),
            nativeHwnd,
            out hwnd);

    public static bool TryGetForegroundWindow(out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryGetForegroundWindow(out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryGetActiveWindow(out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryGetActiveWindow(out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryGetDesktopWindow(out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryGetDesktopWindow(out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryGetShellWindow(out WindowHandle hwnd)
        => FromNativeResult(Win32WindowQuery.TryGetShellWindow(out nint nativeHwnd), nativeHwnd, out hwnd);

    public static bool TryGetOwnerWindow(WindowHandle hwnd, out WindowHandle owner)
        => FromNativeResult(Win32WindowQuery.TryGetOwnerWindow(hwnd.Value, out nint nativeOwner), nativeOwner, out owner);

    public static bool TryGetFirstChildWindow(WindowHandle hwnd, out WindowHandle child)
        => FromNativeResult(Win32WindowQuery.TryGetFirstChildWindow(hwnd.Value, out nint nativeChild), nativeChild, out child);

    public static bool TryGetNextWindow(WindowHandle hwnd, out WindowHandle next)
        => FromNativeResult(Win32WindowQuery.TryGetNextWindow(hwnd.Value, out nint nativeNext), nativeNext, out next);

    public static bool TryGetPreviousWindow(WindowHandle hwnd, out WindowHandle previous)
        => FromNativeResult(Win32WindowQuery.TryGetPreviousWindow(hwnd.Value, out nint nativePrevious), nativePrevious, out previous);

    public static bool TryGetWindowProcessId(WindowHandle hwnd, out int processId)
        => Win32WindowQuery.TryGetWindowProcessId(hwnd.Value, out processId);

    public static bool TryGetWindowBounds(WindowHandle hwnd, out WindowBounds bounds)
        => TryGetBounds(hwnd, clientArea: false, out bounds);

    public static bool TryGetClientBounds(WindowHandle hwnd, out WindowBounds bounds)
        => TryGetBounds(hwnd, clientArea: true, out bounds);

    public static bool TryGetWindowStyles(WindowHandle hwnd, out WindowStylesSnapshot styles)
    {
        styles = default;
        if (!Win32WindowQuery.TryGetWindowStyles(hwnd.Value, out Win32WindowStyles nativeStyles))
        {
            return false;
        }

        styles = new WindowStylesSnapshot(nativeStyles.Style, nativeStyles.ExtendedStyle);
        return true;
    }

    private static bool TryGetBounds(WindowHandle hwnd, bool clientArea, out WindowBounds bounds)
    {
        bounds = default;
        if (!Win32WindowQuery.TryGetWindowBounds(hwnd.Value, clientArea, out Win32WindowBounds nativeBounds))
        {
            return false;
        }

        bounds = new WindowBounds(nativeBounds.X, nativeBounds.Y, nativeBounds.Width, nativeBounds.Height);
        return true;
    }

    private static bool FromNativeResult(bool success, nint nativeHwnd, out WindowHandle hwnd)
    {
        hwnd = success ? new WindowHandle(nativeHwnd) : default;
        return success;
    }

    private static Win32WindowTitleMatchMode ToWin32MatchMode(MatchMode matchMode)
        => matchMode switch
        {
            MatchMode.Exact => Win32WindowTitleMatchMode.Exact,
            MatchMode.Contains => Win32WindowTitleMatchMode.Contains,
            MatchMode.StartsWith => Win32WindowTitleMatchMode.StartsWith,
            MatchMode.EndsWith => Win32WindowTitleMatchMode.EndsWith,
            _ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unsupported match mode."),
        };
}

public static class WindowZOrder
{
    public static void MakeTopmost(WindowHandle hwnd) => Win32WindowZOrder.MakeTopmost(hwnd.Value);

    public static void RemoveTopmost(WindowHandle hwnd) => Win32WindowZOrder.RemoveTopmost(hwnd.Value);

    public static void PlaceAbove(WindowHandle hwnd, WindowHandle hwndInsertAfter)
        => Win32WindowZOrder.PlaceAbove(hwnd.Value, hwndInsertAfter.Value);
}

public static class WindowEffects
{
    public static void ExtendFrameIntoClientArea(WindowHandle hwnd)
        => Win32WindowEffects.ExtendFrameIntoClientArea(hwnd.Value);

    public static bool TryExtendFrameIntoClientArea(WindowHandle hwnd)
        => TryApply(() => Win32WindowEffects.ExtendFrameIntoClientArea(hwnd.Value));

    public static void EnableBlurBehind(WindowHandle hwnd, bool enabled = true)
        => Win32WindowEffects.EnableBlurBehind(hwnd.Value, enabled);

    public static bool TryEnableBlurBehind(WindowHandle hwnd, bool enabled = true)
        => TryApply(() => Win32WindowEffects.EnableBlurBehind(hwnd.Value, enabled));

    public static void ExcludeFromCapture(WindowHandle hwnd)
        => Win32WindowEffects.ExcludeFromCapture(hwnd.Value);

    public static bool TryExcludeFromCapture(WindowHandle hwnd)
        => TryApply(() => Win32WindowEffects.ExcludeFromCapture(hwnd.Value));

    public static void ClearDisplayAffinity(WindowHandle hwnd)
        => Win32WindowEffects.ClearDisplayAffinity(hwnd.Value);

    public static bool TryClearDisplayAffinity(WindowHandle hwnd)
        => TryApply(() => Win32WindowEffects.ClearDisplayAffinity(hwnd.Value));

    private static bool TryApply(Action apply)
    {
        try
        {
            apply();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NativeHResultException or NativeWin32Exception)
        {
            return false;
        }
    }
}
