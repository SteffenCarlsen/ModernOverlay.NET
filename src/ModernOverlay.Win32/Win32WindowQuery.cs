using System.Diagnostics;

namespace ModernOverlay.Win32;

public enum Win32WindowTitleMatchMode
{
    Exact,
    Contains,
    StartsWith,
    EndsWith,
}

public readonly record struct Win32WindowBounds(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct Win32WindowStyles(nint Style, nint ExtendedStyle);

public static class Win32WindowQuery
{
    public static bool IsWindow(nint hwnd) => hwnd != 0 && NativeMethods.IsWindow(hwnd);

    public static bool IsVisible(nint hwnd) => IsWindow(hwnd) && NativeMethods.IsWindowVisible(hwnd);

    public static bool TryGetWindowBounds(nint hwnd, bool clientArea, out Win32WindowBounds bounds)
    {
        bounds = default;
        return IsWindow(hwnd)
            && (clientArea
            ? TryGetClientBounds(hwnd, out bounds)
            : TryGetWholeWindowBounds(hwnd, out bounds));
    }

    public static bool TryGetWindowStyles(nint hwnd, out Win32WindowStyles styles)
    {
        styles = default;
        if (!IsWindow(hwnd))
        {
            return false;
        }

        styles = new Win32WindowStyles(
            NativeMethods.GetWindowLongPtr(hwnd, WindowStyles.GwlStyle),
            NativeMethods.GetWindowLongPtr(hwnd, WindowStyles.GwlExStyle));
        return true;
    }

    public static bool TryFindWindowByProcessName(string processName, out nint hwnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        string normalizedProcessName = NormalizeProcessName(processName);
        return TryFindTopLevelWindow(window =>
        {
            _ = NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0)
            {
                return false;
            }

            try
            {
                using Process process = Process.GetProcessById((int)processId);
                return string.Equals(process.ProcessName, normalizedProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }, out hwnd);
    }

    public static bool TryFindWindowByProcessId(int processId, out nint hwnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return TryFindTopLevelWindow(window =>
        {
            _ = NativeMethods.GetWindowThreadProcessId(window, out uint nativeProcessId);
            return nativeProcessId == processId;
        }, out hwnd);
    }

    public static bool TryFindWindowByTitle(string title, out nint hwnd)
        => TryFindWindowByTitle(title, Win32WindowTitleMatchMode.Exact, out hwnd);

    public static bool TryFindWindowByTitle(string title, Win32WindowTitleMatchMode matchMode, out nint hwnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return TryFindTopLevelWindow(window => Matches(GetWindowTitle(window), title, matchMode), out hwnd);
    }

    public static bool TryFindWindowByClassName(string className, out nint hwnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return TryFindTopLevelWindow(window => string.Equals(GetWindowClassName(window), className, StringComparison.OrdinalIgnoreCase), out hwnd);
    }

    public static bool TryFindChildWindowByTitle(nint parentHwnd, string title, out nint hwnd)
        => TryFindChildWindowByTitle(parentHwnd, title, Win32WindowTitleMatchMode.Exact, out hwnd);

    public static bool TryFindChildWindowByTitle(nint parentHwnd, string title, Win32WindowTitleMatchMode matchMode, out nint hwnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return TryFindChildWindow(parentHwnd, window => Matches(GetWindowTitle(window), title, matchMode), out hwnd);
    }

    public static bool TryFindChildWindowByClassName(nint parentHwnd, string className, out nint hwnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return TryFindChildWindow(parentHwnd, window => string.Equals(GetWindowClassName(window), className, StringComparison.OrdinalIgnoreCase), out hwnd);
    }

    public static bool TryFindChildWindow(nint parentHwnd, string? className, string? title, out nint hwnd)
        => TryFindChildWindow(parentHwnd, className, title, Win32WindowTitleMatchMode.Exact, out hwnd);

    public static bool TryFindChildWindow(nint parentHwnd, string? className, string? title, Win32WindowTitleMatchMode titleMatchMode, out nint hwnd)
    {
        bool hasClassName = !string.IsNullOrWhiteSpace(className);
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        _ = hasClassName || hasTitle
            ? true
            : throw new ArgumentException("A child-window class name or title must be provided.", nameof(className));

        return TryFindChildWindow(
            parentHwnd,
            window => (!hasClassName || string.Equals(GetWindowClassName(window), className, StringComparison.OrdinalIgnoreCase))
                && (!hasTitle || Matches(GetWindowTitle(window), title!, titleMatchMode)),
            out hwnd);
    }

    public static bool TryGetForegroundWindow(out nint hwnd)
    {
        hwnd = NativeMethods.GetForegroundWindow();
        return hwnd != 0 && NativeMethods.IsWindow(hwnd);
    }

    public static bool TryGetActiveWindow(out nint hwnd)
    {
        hwnd = NativeMethods.GetActiveWindow();
        return hwnd != 0 && NativeMethods.IsWindow(hwnd);
    }

    public static bool TryGetDesktopWindow(out nint hwnd)
    {
        hwnd = NativeMethods.GetDesktopWindow();
        return hwnd != 0 && NativeMethods.IsWindow(hwnd);
    }

    public static bool TryGetShellWindow(out nint hwnd)
    {
        hwnd = NativeMethods.GetShellWindow();
        return hwnd != 0 && NativeMethods.IsWindow(hwnd);
    }

    public static bool TryGetOwnerWindow(nint hwnd, out nint owner)
        => TryGetRelatedWindow(hwnd, NativeMethods.GwOwner, out owner);

    public static bool TryGetFirstChildWindow(nint hwnd, out nint child)
        => TryGetRelatedWindow(hwnd, NativeMethods.GwChild, out child);

    public static bool TryGetNextWindow(nint hwnd, out nint next)
        => TryGetRelatedWindow(hwnd, NativeMethods.GwHwndNext, out next);

    public static bool TryGetPreviousWindow(nint hwnd, out nint previous)
        => TryGetRelatedWindow(hwnd, NativeMethods.GwHwndPrev, out previous);

    public static bool IsWindowMinimized(nint hwnd)
        => IsWindow(hwnd) && NativeMethods.IsIconic(hwnd);

    public static bool TryGetWindowProcessId(nint hwnd, out int processId)
    {
        processId = 0;
        if (!IsWindow(hwnd))
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint nativeProcessId);
        if (nativeProcessId == 0)
        {
            return false;
        }

        processId = checked((int)nativeProcessId);
        return true;
    }

    private static bool TryGetWholeWindowBounds(nint hwnd, out Win32WindowBounds bounds)
    {
        bounds = default;
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect rect))
        {
            return false;
        }

        bounds = new Win32WindowBounds(rect.Left, rect.Top, rect.Width, rect.Height);
        return !bounds.IsEmpty;
    }

    private static bool TryGetClientBounds(nint hwnd, out Win32WindowBounds bounds)
    {
        bounds = default;
        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.Rect rect))
        {
            return false;
        }

        var origin = new NativeMethods.Point();
        if (!NativeMethods.ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        bounds = new Win32WindowBounds(origin.X, origin.Y, rect.Width, rect.Height);
        return !bounds.IsEmpty;
    }

    private static bool TryFindTopLevelWindow(Func<nint, bool> predicate, out nint hwnd)
    {
        nint found = 0;
        _ = NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindow(window) || !predicate(window))
            {
                return true;
            }

            found = window;
            return false;
        }, 0);

        hwnd = found;
        return hwnd != 0;
    }

    private static bool TryFindChildWindow(nint parentHwnd, Func<nint, bool> predicate, out nint hwnd)
    {
        hwnd = 0;
        if (!IsWindow(parentHwnd))
        {
            return false;
        }

        nint found = 0;
        _ = NativeMethods.EnumChildWindows(parentHwnd, (window, _) =>
        {
            if (!NativeMethods.IsWindow(window) || !predicate(window))
            {
                return true;
            }

            found = window;
            return false;
        }, 0);

        hwnd = found;
        return hwnd != 0;
    }

    private static bool TryGetRelatedWindow(nint hwnd, uint command, out nint related)
    {
        related = 0;
        if (!IsWindow(hwnd))
        {
            return false;
        }

        related = NativeMethods.GetWindow(hwnd, command);
        return related != 0 && NativeMethods.IsWindow(related);
    }

    private static string GetWindowTitle(nint hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        char[] title = new char[length + 1];
        int copied = NativeMethods.GetWindowText(hwnd, title, title.Length);
        return copied > 0 ? new string(title, 0, copied) : string.Empty;
    }

    private static string GetWindowClassName(nint hwnd)
    {
        char[] className = new char[256];
        int copied = NativeMethods.GetClassName(hwnd, className, className.Length);
        return copied > 0 ? new string(className, 0, copied) : string.Empty;
    }

    private static string NormalizeProcessName(string processName)
        => processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

    private static bool Matches(string value, string pattern, Win32WindowTitleMatchMode matchMode)
        => matchMode switch
        {
            Win32WindowTitleMatchMode.Exact => string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase),
            Win32WindowTitleMatchMode.Contains => value.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            Win32WindowTitleMatchMode.StartsWith => value.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            Win32WindowTitleMatchMode.EndsWith => value.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unsupported match mode."),
        };
}
