namespace ModernOverlay.Win32;

public sealed class Win32OverlayWindow : IDisposable
{
    private static readonly Lock WindowsGate = new();
    private static readonly Dictionary<nint, WindowState> Windows = [];

    private readonly Win32OwnerThread ownerThread;
    private readonly WindowState state;
    private bool disposed;

    private Win32OverlayWindow(Win32OwnerThread ownerThread, WindowState state)
    {
        this.ownerThread = ownerThread;
        this.state = state;
    }

    public nint Hwnd => state.Hwnd;

    public int OwnerThreadId => ownerThread.OwnerThreadId;

    public static Win32OverlayWindow Create(Win32OverlayWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.PerMonitorDpiAware)
        {
            _ = Win32DpiQuery.TrySetProcessPerMonitorV2();
        }

        var ownerThread = new Win32OwnerThread();

        try
        {
            WindowState state = ownerThread.Invoke(() => CreateOnOwnerThread(options));
            return new Win32OverlayWindow(ownerThread, state);
        }
        catch
        {
            ownerThread.Dispose();
            throw;
        }
    }

    public void Show()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            _ = NativeMethods.ShowWindow(state.Hwnd, NativeMethods.SwShowNoActivate);
            if (!NativeMethods.SetWindowPos(state.Hwnd, 0, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow))
            {
                throw new NativeWin32Exception("SetWindowPos(show)");
            }
        });
    }

    public void Hide()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => _ = NativeMethods.ShowWindow(state.Hwnd, NativeMethods.SwHide));
    }

    public void Minimize()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => _ = NativeMethods.ShowWindow(state.Hwnd, NativeMethods.SwShowMinNoActive));
    }

    public void Restore()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => _ = NativeMethods.ShowWindow(state.Hwnd, NativeMethods.SwRestore));
    }

    public void SetBounds(int x, int y, int width, int height)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            if (!NativeMethods.SetWindowPos(state.Hwnd, 0, x, y, width, height, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate))
            {
                throw new NativeWin32Exception("SetWindowPos(bounds)");
            }
        });
    }

    public void SetClickThrough(bool enabled)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            nint current = NativeMethods.GetWindowLongPtr(state.Hwnd, WindowStyles.GwlExStyle);
            nint next = WindowStyles.WithFlag(current, WindowStyles.WsExTransparent, enabled);
            _ = NativeMethods.SetWindowLongPtr(state.Hwnd, WindowStyles.GwlExStyle, next);
            ApplyFrameChanged();
        });
    }

    public void SetTopMost(bool enabled)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            if (enabled)
            {
                Win32WindowZOrder.MakeTopmost(state.Hwnd);
                return;
            }

            Win32WindowZOrder.RemoveTopmost(state.Hwnd);
        });
    }

    public void PlaceRelativeTo(nint hwndInsertAfter)
    {
        ThrowIfDisposed();
        if (hwndInsertAfter == 0)
        {
            throw new ArgumentException("The z-order target HWND cannot be null.", nameof(hwndInsertAfter));
        }

        ownerThread.Invoke(() => Win32WindowZOrder.PlaceAbove(state.Hwnd, hwndInsertAfter));
    }

    public void ExtendFrameIntoClientArea()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            Win32WindowEffects.ExtendFrameIntoClientArea(state.Hwnd);
        });
    }

    public void EnableBlurBehind(bool enabled = true)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            Win32WindowEffects.EnableBlurBehind(state.Hwnd, enabled);
        });
    }

    public void SetLayeredAlpha(byte alpha)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            if (!NativeMethods.SetLayeredWindowAttributes(state.Hwnd, 0, alpha, NativeMethods.LwaAlpha))
            {
                throw new NativeWin32Exception("SetLayeredWindowAttributes");
            }
        });
    }

    public Win32DpiScale GetDpiScale()
    {
        ThrowIfDisposed();
        return ownerThread.Invoke(() => Win32DpiQuery.GetScaleForWindow(state.Hwnd));
    }

    public void SetHotkeyCallback(Action<int>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.HotkeyPressed = callback);
    }

    public void SetDpiChangedCallback(Action<Win32DpiScale, Win32WindowBounds>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.DpiChanged = callback);
    }

    public void SetPointerCallback(Action<Win32PointerEvent>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.PointerEvent = callback);
    }

    public void RegisterHotKey(int id, uint modifiers, uint virtualKey)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            if (!NativeMethods.RegisterHotKey(state.Hwnd, id, modifiers, virtualKey))
            {
                throw new NativeWin32Exception("RegisterHotKey");
            }
        });
    }

    public void UnregisterHotKey(int id)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => _ = NativeMethods.UnregisterHotKey(state.Hwnd, id));
    }

    public void InvokeOnOwnerThread(Action action)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(action);
    }

    public void RunFrameLoop(TimeSpan interval, Action renderFrame, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ownerThread.RunFrameLoop(interval, renderFrame, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        ownerThread.Invoke(DestroyOnOwnerThread);
        ownerThread.Dispose();
        GC.KeepAlive(state.WindowProcedure);
    }

    private static WindowState CreateOnOwnerThread(Win32OverlayWindowOptions options)
    {
        string className = options.ClassName ?? $"ModernOverlay_{Guid.NewGuid():N}";
        nint instance = NativeMethods.GetModuleHandle(null);
        if (instance == 0)
        {
            throw new NativeWin32Exception("GetModuleHandleW");
        }

        NativeMethods.WndProc windowProcedure = WindowProc;
        var windowClass = new NativeMethods.WndClassEx
        {
            Instance = instance,
            ClassName = className,
            WindowProcedure = windowProcedure,
        };

        if (NativeMethods.RegisterClassEx(windowClass) == 0)
        {
            throw new NativeWin32Exception("RegisterClassExW");
        }

        uint extendedStyle = WindowStyles.BuildExtendedStyle(options.ClickThrough, options.TopMost, options.ToolWindow);
        nint hwnd = NativeMethods.CreateWindowEx(
            extendedStyle,
            className,
            options.Title,
            WindowStyles.WsPopup,
            options.X,
            options.Y,
            options.Width,
            options.Height,
            0,
            0,
            instance,
            0);

        if (hwnd == 0)
        {
            _ = NativeMethods.UnregisterClass(className, instance);
            throw new NativeWin32Exception("CreateWindowExW");
        }

        var state = new WindowState(className, instance, hwnd, windowProcedure);
        lock (WindowsGate)
        {
            Windows.Add(hwnd, state);
        }

        return state;
    }

    private static nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (message == NativeMethods.WmHotKey && TryGetWindowState(hwnd, out WindowState? state) && state is not null)
        {
            state.HotkeyPressed?.Invoke(unchecked((int)wParam));
            return 0;
        }

        if (message == NativeMethods.WmDpiChanged && TryGetWindowState(hwnd, out state) && state is not null)
        {
            HandleDpiChanged(hwnd, wParam, lParam, state);
            return 0;
        }

        if (TryGetPointerEvent(message, lParam, out Win32PointerEvent pointerEvent)
            && TryGetWindowState(hwnd, out state)
            && state is not null)
        {
            state.PointerEvent?.Invoke(pointerEvent);
            return 0;
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static bool TryGetWindowState(nint hwnd, out WindowState? state)
    {
        lock (WindowsGate)
        {
            return Windows.TryGetValue(hwnd, out state);
        }
    }

    private static void HandleDpiChanged(nint hwnd, nuint wParam, nint lParam, WindowState state)
    {
        uint dpiX = unchecked((uint)wParam) & 0xFFFF;
        uint dpiY = unchecked((uint)wParam) >> 16;
        var scale = new Win32DpiScale(
            dpiX == 0 ? 1f : dpiX / (float)NativeMethods.DefaultDpi,
            dpiY == 0 ? 1f : dpiY / (float)NativeMethods.DefaultDpi);

        NativeMethods.Rect suggestedRect = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.Rect>(lParam);
        var bounds = new Win32WindowBounds(
            suggestedRect.Left,
            suggestedRect.Top,
            suggestedRect.Width,
            suggestedRect.Height);

        if (!bounds.IsEmpty)
        {
            _ = NativeMethods.SetWindowPos(
                hwnd,
                0,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }

        state.DpiChanged?.Invoke(scale, bounds);
    }

    private static bool TryGetPointerEvent(uint message, nint lParam, out Win32PointerEvent pointerEvent)
    {
        Win32PointerEventKind kind;
        Win32PointerButton button;
        switch (message)
        {
            case NativeMethods.WmMouseMove:
                kind = Win32PointerEventKind.Moved;
                button = Win32PointerButton.None;
                break;
            case NativeMethods.WmLButtonDown:
                kind = Win32PointerEventKind.Pressed;
                button = Win32PointerButton.Left;
                break;
            case NativeMethods.WmLButtonUp:
                kind = Win32PointerEventKind.Released;
                button = Win32PointerButton.Left;
                break;
            case NativeMethods.WmRButtonDown:
                kind = Win32PointerEventKind.Pressed;
                button = Win32PointerButton.Right;
                break;
            case NativeMethods.WmRButtonUp:
                kind = Win32PointerEventKind.Released;
                button = Win32PointerButton.Right;
                break;
            case NativeMethods.WmMButtonDown:
                kind = Win32PointerEventKind.Pressed;
                button = Win32PointerButton.Middle;
                break;
            case NativeMethods.WmMButtonUp:
                kind = Win32PointerEventKind.Released;
                button = Win32PointerButton.Middle;
                break;
            default:
                pointerEvent = default;
                return false;
        }

        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        pointerEvent = new Win32PointerEvent(kind, button, x, y);
        return true;
    }

    private void ApplyFrameChanged()
    {
        if (!NativeMethods.SetWindowPos(state.Hwnd, 0, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
        {
            throw new NativeWin32Exception("SetWindowPos(frame changed)");
        }
    }

    private void DestroyOnOwnerThread()
    {
        if (state.Hwnd != 0)
        {
            lock (WindowsGate)
            {
                _ = Windows.Remove(state.Hwnd);
            }

            _ = NativeMethods.DestroyWindow(state.Hwnd);
        }

        _ = NativeMethods.UnregisterClass(state.ClassName, state.Instance);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record WindowState(string ClassName, nint Instance, nint Hwnd, NativeMethods.WndProc WindowProcedure)
    {
        public Action<int>? HotkeyPressed { get; set; }

        public Action<Win32DpiScale, Win32WindowBounds>? DpiChanged { get; set; }

        public Action<Win32PointerEvent>? PointerEvent { get; set; }
    }
}

public readonly record struct Win32PointerEvent(Win32PointerEventKind Kind, Win32PointerButton Button, int X, int Y);

public enum Win32PointerEventKind
{
    Moved,
    Pressed,
    Released,
}

public enum Win32PointerButton
{
    None,
    Left,
    Right,
    Middle,
}
