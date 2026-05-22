namespace ModernOverlay.Win32;

public sealed class Win32OverlayWindow : IDisposable
{
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
            nint insertAfter = enabled ? NativeMethods.HwndTopMost : NativeMethods.HwndNoTopMost;
            if (!NativeMethods.SetWindowPos(state.Hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate))
            {
                throw new NativeWin32Exception("SetWindowPos(z-order)");
            }
        });
    }

    public void ExtendFrameIntoClientArea()
    {
        ThrowIfDisposed();
        ownerThread.Invoke(() =>
        {
            var margins = new NativeMethods.Margins
            {
                LeftWidth = -1,
                RightWidth = -1,
                TopHeight = -1,
                BottomHeight = -1,
            };

            int hr = NativeMethods.DwmExtendFrameIntoClientArea(state.Hwnd, margins);
            if (hr < 0)
            {
                throw new NativeHResultException("DwmExtendFrameIntoClientArea", hr);
            }
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

    public void InvokeOnOwnerThread(Action action)
    {
        ThrowIfDisposed();
        ownerThread.Invoke(action);
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

        return new WindowState(className, instance, hwnd, windowProcedure);
    }

    private static nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
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
            _ = NativeMethods.DestroyWindow(state.Hwnd);
        }

        _ = NativeMethods.UnregisterClass(state.ClassName, state.Instance);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record WindowState(string ClassName, nint Instance, nint Hwnd, NativeMethods.WndProc WindowProcedure);
}
