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

            if (state.ExcludeFromCapture)
            {
                Win32WindowEffects.ExcludeFromCapture(state.Hwnd);
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

    public void SetTransparentColorKey(uint colorKey, byte alpha = byte.MaxValue)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            if (!NativeMethods.SetLayeredWindowAttributes(state.Hwnd, colorKey, alpha, NativeMethods.LwaColorKey | NativeMethods.LwaAlpha))
            {
                throw new NativeWin32Exception("SetLayeredWindowAttributes(color key)");
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

    public void SetKeyboardCallback(Action<Win32KeyboardEvent>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.KeyboardEvent = callback);
    }

    public void SetTextInputCallback(Action<Win32TextInputEvent>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.TextInputEvent = callback);
    }

    public void SetInputRegionCallback(Func<int, int, bool>? callback)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() => state.InputRegion = callback);
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

    public void RunFrameLoop(Func<TimeSpan> resolveInterval, Action renderFrame, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ownerThread.RunFrameLoop(resolveInterval, renderFrame, cancellationToken);
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

        var state = new WindowState(className, instance, hwnd, windowProcedure, options.ExcludeFromCapture);
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

        if (message == NativeMethods.WmNcHitTest && TryGetWindowState(hwnd, out state) && state is not null)
        {
            return ResolveInputRegion(hwnd, lParam, state)
                ? NativeMethods.HtClient
                : NativeMethods.HtTransparent;
        }

        if (TryGetKeyboardEvent(message, wParam, lParam, out Win32KeyboardEvent keyboardEvent)
            && TryGetWindowState(hwnd, out state)
            && state is not null)
        {
            state.KeyboardEvent?.Invoke(keyboardEvent);
            return 0;
        }

        if (TryGetTextInputEvent(message, wParam, out Win32TextInputEvent textInputEvent)
            && TryGetWindowState(hwnd, out state)
            && state is not null)
        {
            state.TextInputEvent?.Invoke(textInputEvent);
            return 0;
        }

        if (message == NativeMethods.WmDpiChanged && TryGetWindowState(hwnd, out state) && state is not null)
        {
            HandleDpiChanged(hwnd, wParam, lParam, state);
            return 0;
        }

        if (TryGetPointerEvent(hwnd, message, wParam, lParam, out Win32PointerEvent pointerEvent)
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

    private static bool ResolveInputRegion(nint hwnd, nint lParam, WindowState state)
    {
        Func<int, int, bool>? inputRegion = state.InputRegion;
        NativeMethods.Point point = GetPointerPoint(NativeMethods.WmNcHitTest, lParam);
        _ = NativeMethods.ScreenToClient(hwnd, ref point)
            ? true
            : throw new NativeWin32Exception("ScreenToClient(input region)");
        return inputRegion?.Invoke(point.X, point.Y) ?? true;
    }

    private static bool TryGetPointerEvent(nint hwnd, uint message, nuint wParam, nint lParam, out Win32PointerEvent pointerEvent)
    {
        Win32PointerEventKind kind;
        Win32PointerButton button;
        int wheelDelta = 0;
        bool isHorizontalWheel = false;
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
            case NativeMethods.WmMouseWheel:
                kind = Win32PointerEventKind.Wheel;
                button = Win32PointerButton.None;
                wheelDelta = GetSignedHighWord(unchecked((ulong)wParam));
                break;
            case NativeMethods.WmMouseHWheel:
                kind = Win32PointerEventKind.Wheel;
                button = Win32PointerButton.None;
                wheelDelta = GetSignedHighWord(unchecked((ulong)wParam));
                isHorizontalWheel = true;
                break;
            default:
                pointerEvent = default;
                return false;
        }

        NativeMethods.Point point = GetPointerPoint(message, lParam);
        if (kind == Win32PointerEventKind.Wheel && !NativeMethods.ScreenToClient(hwnd, ref point))
        {
            throw new NativeWin32Exception("ScreenToClient(pointer wheel)");
        }

        pointerEvent = new Win32PointerEvent(kind, button, point.X, point.Y, wheelDelta, isHorizontalWheel);
        return true;
    }

    private static bool TryGetKeyboardEvent(uint message, nuint wParam, nint lParam, out Win32KeyboardEvent keyboardEvent)
    {
        bool isPressed;
        bool isSystem;
        switch (message)
        {
            case NativeMethods.WmKeyDown:
                isPressed = true;
                isSystem = false;
                break;
            case NativeMethods.WmSysKeyDown:
                isPressed = true;
                isSystem = true;
                break;
            case NativeMethods.WmKeyUp:
                isPressed = false;
                isSystem = false;
                break;
            case NativeMethods.WmSysKeyUp:
                isPressed = false;
                isSystem = true;
                break;
            default:
                keyboardEvent = default;
                return false;
        }

        int data = lParam.ToInt32();
        keyboardEvent = new Win32KeyboardEvent(
            unchecked((int)wParam),
            isPressed,
            isSystem,
            data & 0xFFFF,
            (data >> 16) & 0xFF,
            (data & (1 << 24)) != 0,
            (data & (1 << 30)) != 0,
            (data & (1 << 31)) != 0,
            GetModifierKeys());
        return true;
    }

    private static bool TryGetTextInputEvent(uint message, nuint wParam, out Win32TextInputEvent textInputEvent)
    {
        if (message is not NativeMethods.WmChar and not NativeMethods.WmSysChar)
        {
            textInputEvent = default;
            return false;
        }

        textInputEvent = new Win32TextInputEvent(char.ConvertFromUtf32(unchecked((int)wParam)), message == NativeMethods.WmSysChar);
        return true;
    }

    private static Win32ModifierKeys GetModifierKeys()
    {
        Win32ModifierKeys modifiers = Win32ModifierKeys.None;
        if (IsKeyDown(NativeMethods.VkShift))
        {
            modifiers |= Win32ModifierKeys.Shift;
        }

        if (IsKeyDown(NativeMethods.VkControl))
        {
            modifiers |= Win32ModifierKeys.Control;
        }

        if (IsKeyDown(NativeMethods.VkMenu))
        {
            modifiers |= Win32ModifierKeys.Alt;
        }

        if (IsKeyDown(NativeMethods.VkLWin) || IsKeyDown(NativeMethods.VkRWin))
        {
            modifiers |= Win32ModifierKeys.Windows;
        }

        return modifiers;
    }

    private static bool IsKeyDown(int virtualKey) => (NativeMethods.GetKeyState(virtualKey) & unchecked((short)0x8000)) != 0;

    private static NativeMethods.Point GetPointerPoint(uint message, nint lParam)
    {
        long value = lParam.ToInt64();
        return new NativeMethods.Point
        {
            X = unchecked((short)(value & 0xFFFF)),
            Y = unchecked((short)((value >> 16) & 0xFFFF)),
        };
    }

    private static int GetSignedHighWord(ulong value)
        => unchecked((short)((value >> 16) & 0xFFFF));

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

    private sealed record WindowState(string ClassName, nint Instance, nint Hwnd, NativeMethods.WndProc WindowProcedure, bool ExcludeFromCapture)
    {
        public Action<int>? HotkeyPressed { get; set; }

        public Action<Win32DpiScale, Win32WindowBounds>? DpiChanged { get; set; }

        public Action<Win32PointerEvent>? PointerEvent { get; set; }

        public Action<Win32KeyboardEvent>? KeyboardEvent { get; set; }

        public Action<Win32TextInputEvent>? TextInputEvent { get; set; }

        public Func<int, int, bool>? InputRegion { get; set; }
    }
}

public readonly record struct Win32PointerEvent(
    Win32PointerEventKind Kind,
    Win32PointerButton Button,
    int X,
    int Y,
    int WheelDelta = 0,
    bool IsHorizontalWheel = false);

public enum Win32PointerEventKind
{
    Moved,
    Pressed,
    Released,
    Wheel,
}

public enum Win32PointerButton
{
    None,
    Left,
    Right,
    Middle,
}

public readonly record struct Win32KeyboardEvent(
    int VirtualKey,
    bool IsPressed,
    bool IsSystemKey,
    int RepeatCount,
    int ScanCode,
    bool IsExtendedKey,
    bool WasDown,
    bool IsTransitionState,
    Win32ModifierKeys Modifiers);

public readonly record struct Win32TextInputEvent(string Text, bool IsSystemCharacter);

[Flags]
public enum Win32ModifierKeys
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3,
}
