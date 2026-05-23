namespace ModernOverlay.UI;

public sealed record OverlayUiOptions
{
    public bool RegisterInputRegions { get; init; } = true;

    public UiTheme Theme { get; init; } = UiTheme.Default;
}

public static class OverlayUi
{
    public static OverlayUiRoot Attach(OverlayWindow overlay, OverlayUiOptions? options = null)
        => new(overlay, options ?? new OverlayUiOptions());
}

public sealed class OverlayUiRoot : IDisposable, IOverlayInputRegionResolver
{
    private readonly OverlayWindow overlay;
    private readonly Queue<Action> deferredOperations = [];
    private int ownerThreadId;
    private UiInvalidation invalidation = UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion;
    private RectF lastLayoutBounds;
    private UiRootPhase phase;
    private bool disposed;
    private UiElement? pressedElement;
    private PointF pressedPosition;
    private bool pressedExceededDragThreshold;
    private float dragThreshold = 4f;
    private List<UiElement> hoveredElements = [];
    private readonly bool registeredInputRegions;

    internal OverlayUiRoot(OverlayWindow overlay, OverlayUiOptions options)
    {
        this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ThemeResources = new UiThemeResources(overlay.Resources, Options.Theme);
        Root = new Canvas
        {
            Width = float.NaN,
            Height = float.NaN,
        };
        Root.SetRoot(this);

        overlay.PointerMoved += HandlePointerMoved;
        overlay.PointerPressed += HandlePointerPressed;
        overlay.PointerReleased += HandlePointerReleased;
        overlay.PointerWheel += HandlePointerWheel;
        overlay.KeyPressed += HandleKeyPressed;
        overlay.KeyReleased += HandleKeyReleased;
        overlay.TextInput += HandleTextInput;
        overlay.DeviceRestored += HandleDeviceRestored;
        overlay.Disposed += HandleOverlayDisposed;
        if (Options.RegisterInputRegions)
        {
            registeredInputRegions = true;
            overlay.SetInputRegionResolver(this);
        }
    }

    public OverlayUiOptions Options { get; }

    public Canvas Root { get; }

    public UiElement? FocusedElement { get; private set; }

    public UiElement? CapturedElement { get; private set; }

    public UiThemeResources ThemeResources { get; }

    public UiTheme Theme => ThemeResources.Theme;

    public RectF BoundsDips => overlay.BoundsDips;

    public float DragThreshold
    {
        get => dragThreshold;
        set
        {
            if (value < 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Drag threshold must be finite and non-negative.");
            }

            dragThreshold = value;
        }
    }

    internal bool IsInProtectedPhase => phase != UiRootPhase.Idle;

    public void Render(DrawContext frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        BindAccess();
        VerifyAccess();
        UpdateFrameTimers();
        EnsureLayout();
        using (EnterPhase(UiRootPhase.Render))
        {
            Root.Render(new UiRenderContext(frame, ThemeResources));
        }
    }

    public OverlayInputRegionResult ResolveInputRegion(PointF position)
    {
        if (disposed)
        {
            return OverlayInputRegionResult.PassThrough;
        }

        BindAccess();
        VerifyAccess();
        EnsureLayout();
        return ResolveInputTarget(position) is not null || HasOpenOutsideDismissPopup()
            ? OverlayInputRegionResult.Interactive
            : OverlayInputRegionResult.PassThrough;
    }

    internal void Invalidate(UiInvalidation flags)
    {
        VerifyAccess();
        if (flags == UiInvalidation.None)
        {
            return;
        }

        invalidation |= flags;
    }

    public void Focus(UiElement? element)
    {
        VerifyAccess();
        using (EnterPhase(UiRootPhase.FocusChange))
        {
            if (element is not null && element.Root != this)
            {
                throw new InvalidOperationException("Cannot focus an element attached to another UI root.");
            }

            if (element is not null && (!element.Focusable || element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled))
            {
                throw new InvalidOperationException("Only visible, enabled, focusable UI elements can receive focus.");
            }

            FocusedElement = element;
            invalidation |= UiInvalidation.Render | UiInvalidation.FocusState;
        }
    }

    public void CapturePointer(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        VerifyAccess();
        if (element.Root != this)
        {
            throw new InvalidOperationException("Cannot capture pointer for an element attached to another UI root.");
        }

        if (element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException("Only visible, enabled UI elements can capture the pointer.");
        }

        CapturedElement = element;
        invalidation |= UiInvalidation.Render;
    }

    public void ReleasePointerCapture(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        VerifyAccess();
        if (CapturedElement == element)
        {
            using (EnterPhase(UiRootPhase.CaptureRelease))
            {
                CapturedElement = null;
                invalidation |= UiInvalidation.Render;
            }
        }
    }

    public void MoveFocusNext() => MoveFocus(forward: true);

    public void MoveFocusPrevious() => MoveFocus(forward: false);

    public void ApplyTheme(UiTheme theme)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(theme);
        VerifyAccess();
        ThemeResources.ApplyTheme(theme);
        invalidation |= UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.Resource;
    }

    public bool IsKeyboardFocusWithin(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        VerifyAccess();
        return IsSameOrDescendant(element, FocusedElement);
    }

    public void Defer(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        deferredOperations.Enqueue(operation);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        overlay.PointerMoved -= HandlePointerMoved;
        overlay.PointerPressed -= HandlePointerPressed;
        overlay.PointerReleased -= HandlePointerReleased;
        overlay.PointerWheel -= HandlePointerWheel;
        overlay.KeyPressed -= HandleKeyPressed;
        overlay.KeyReleased -= HandleKeyReleased;
        overlay.TextInput -= HandleTextInput;
        overlay.DeviceRestored -= HandleDeviceRestored;
        overlay.Disposed -= HandleOverlayDisposed;
        if (registeredInputRegions)
        {
            overlay.SetInputRegionResolver(null);
        }

        DismissAllPopups(UiPopupDismissReason.RootDisposed);
        Root.SetRoot(null);
        ThemeResources.Dispose();
    }

    internal void NotifyElementDetached(UiElement element)
    {
        DismissPopupsOwnedBy(element, UiPopupDismissReason.OwnerDetached);

        if (IsSameOrDescendant(element, FocusedElement))
        {
            FocusedElement = null;
        }

        if (IsSameOrDescendant(element, CapturedElement))
        {
            CapturedElement = null;
        }

        if (IsSameOrDescendant(element, pressedElement))
        {
            pressedElement!.IsPressed = false;
            pressedElement = null;
        }

        ClearHoveredDescendants(element);
    }

    internal void NotifyElementStateChanged(UiElement element)
    {
        if (disposed)
        {
            return;
        }

        bool treeUnavailable = IsUnavailableForTreeInput(element);
        bool elementCannotFocus = !element.Focusable;
        if (!treeUnavailable && !elementCannotFocus)
        {
            return;
        }

        if (treeUnavailable)
        {
            DismissPopupsOwnedBy(element, UiPopupDismissReason.OwnerUnavailable);
        }

        if ((treeUnavailable && IsSameOrDescendant(element, FocusedElement))
            || (elementCannotFocus && ReferenceEquals(FocusedElement, element)))
        {
            FocusedElement = null;
        }

        if (treeUnavailable && IsSameOrDescendant(element, CapturedElement))
        {
            CapturedElement = null;
        }

        if (treeUnavailable && IsSameOrDescendant(element, pressedElement))
        {
            pressedElement!.IsPressed = false;
            pressedElement = null;
        }

        if (treeUnavailable)
        {
            ClearHoveredDescendants(element);
        }

        invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState;
    }

    private void EnsureLayout()
    {
        RectF bounds = overlay.BoundsDips;
        if (bounds != lastLayoutBounds)
        {
            invalidation |= UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion;
        }

        if ((invalidation & (UiInvalidation.Measure | UiInvalidation.Arrange)) == 0)
        {
            return;
        }

        var available = new SizeF(bounds.Width, bounds.Height);
        using (EnterPhase(UiRootPhase.Measure))
        {
            Root.Measure(available);
        }

        using (EnterPhase(UiRootPhase.Arrange))
        {
            Root.Arrange(new RectF(0f, 0f, available.Width, available.Height));
        }

        lastLayoutBounds = bounds;
        invalidation &= ~(UiInvalidation.Measure | UiInvalidation.Arrange);
    }

    private void HandlePointerMoved(OverlayWindow sender, OverlayPointerEventArgs args)
        => DispatchPointer(args, OverlayPointerEventKind.Moved);

    private void HandlePointerPressed(OverlayWindow sender, OverlayPointerEventArgs args)
        => DispatchPointer(args, OverlayPointerEventKind.Pressed);

    private void HandlePointerReleased(OverlayWindow sender, OverlayPointerEventArgs args)
        => DispatchPointer(args, OverlayPointerEventKind.Released);

    private void HandlePointerWheel(OverlayWindow sender, OverlayPointerEventArgs args)
        => DispatchPointer(args, OverlayPointerEventKind.Wheel);

    private void HandleOverlayDisposed(OverlayWindow sender) => Dispose();

    private void HandleDeviceRestored(OverlayWindow sender, OverlayDeviceEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        invalidation |= UiInvalidation.Render | UiInvalidation.Resource;
    }

    private void HandleKeyPressed(OverlayWindow sender, OverlayKeyboardEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        if (args.VirtualKey == UiVirtualKeys.Tab)
        {
            if ((args.Modifiers & OverlayModifierKeys.Shift) != 0)
            {
                MoveFocusPrevious();
            }
            else
            {
                MoveFocusNext();
            }

            return;
        }

        if (args.VirtualKey == UiVirtualKeys.Escape && DismissTopmostEscapePopup())
        {
            return;
        }

        DispatchKey(args, pressed: true);
    }

    private void HandleKeyReleased(OverlayWindow sender, OverlayKeyboardEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        DispatchKey(args, pressed: false);
    }

    private void HandleTextInput(OverlayWindow sender, OverlayTextInputEventArgs args)
    {
        if (disposed || FocusedElement is null)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        var uiArgs = new UiTextInputEventArgs(args.Text);
        using (EnterPhase(UiRootPhase.EventDispatch))
        {
            RouteTextInput(FocusedElement, uiArgs);
        }

        invalidation |= UiInvalidation.Render;
    }

    private void DispatchPointer(OverlayPointerEventArgs overlayArgs, OverlayPointerEventKind kind)
    {
        if (disposed)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        EnsureLayout();
        if (kind == OverlayPointerEventKind.Moved)
        {
            UpdateHoveredElement(ResolveInputTarget(overlayArgs.Position), overlayArgs.Position);
        }

        UiElement? target = CapturedElement ?? ResolveInputTarget(overlayArgs.Position);
        if (kind == OverlayPointerEventKind.Pressed && DismissPopupsOutside(overlayArgs.Position))
        {
            target = CapturedElement ?? ResolveInputTarget(overlayArgs.Position);
        }

        if (target is null)
        {
            return;
        }

        if (kind == OverlayPointerEventKind.Moved && pressedElement is not null && !pressedExceededDragThreshold)
        {
            pressedExceededDragThreshold = HasExceededDragThreshold(overlayArgs.Position);
        }

        bool isDragGesture = (kind is OverlayPointerEventKind.Moved or OverlayPointerEventKind.Released) && pressedExceededDragThreshold;
        var args = new UiPointerEventArgs(kind, overlayArgs.Button, overlayArgs.Position, overlayArgs.WheelDelta, overlayArgs.IsHorizontalWheel, isDragGesture);
        using (EnterPhase(UiRootPhase.EventDispatch))
        {
            if (kind == OverlayPointerEventKind.Pressed)
            {
                pressedElement = target;
                pressedPosition = overlayArgs.Position;
                pressedExceededDragThreshold = false;
                target.IsPressed = true;
                if (target.Focusable)
                {
                    Focus(target);
                }
            }

            RoutePointer(target, args);

            if (kind == OverlayPointerEventKind.Released)
            {
                if (pressedElement is { } pressed)
                {
                    pressed.IsPressed = false;
                }

                pressedElement = null;
                pressedExceededDragThreshold = false;
            }
        }

        invalidation |= UiInvalidation.Render;
    }

    private void UpdateHoveredElement(UiElement? next, PointF position)
    {
        List<UiElement> nextPath = BuildElementPath(next);
        if (hoveredElements.SequenceEqual(nextPath))
        {
            return;
        }

        var args = new UiPointerEventArgs(OverlayPointerEventKind.Moved, OverlayPointerButton.None, position);
        foreach (UiElement exited in hoveredElements.Where(element => !nextPath.Contains(element)).ToArray())
        {
            exited.IsMouseOver = false;
            exited.RaisePointerExited(args);
        }

        foreach (UiElement entered in nextPath.Where(element => !hoveredElements.Contains(element)).Reverse().ToArray())
        {
            entered.IsMouseOver = true;
            entered.RaisePointerEntered(args);
        }

        hoveredElements = nextPath;
        invalidation |= UiInvalidation.Render;
    }

    private static void RoutePointer(UiElement target, UiPointerEventArgs args)
    {
        for (UiElement? current = target; current is not null && !args.Handled; current = current.Parent)
        {
            switch (args.Kind)
            {
                case OverlayPointerEventKind.Moved:
                    current.RaisePointerMoved(args);
                    break;
                case OverlayPointerEventKind.Pressed:
                    current.RaisePointerPressed(args);
                    break;
                case OverlayPointerEventKind.Released:
                    current.RaisePointerReleased(args);
                    break;
                case OverlayPointerEventKind.Wheel:
                    current.RaisePointerWheel(args);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(args), "Unsupported pointer event kind.");
            }
        }
    }

    private void DispatchKey(OverlayKeyboardEventArgs overlayArgs, bool pressed)
    {
        if (FocusedElement is null)
        {
            return;
        }

        var args = new UiKeyboardEventArgs(overlayArgs.VirtualKey, overlayArgs.IsRepeat, overlayArgs.Modifiers);
        using (EnterPhase(UiRootPhase.EventDispatch))
        {
            RouteKey(FocusedElement, args, pressed);
        }

        invalidation |= UiInvalidation.Render;
    }

    private static void RouteKey(UiElement target, UiKeyboardEventArgs args, bool pressed)
    {
        for (UiElement? current = target; current is not null && !args.Handled; current = current.Parent)
        {
            if (pressed)
            {
                current.RaiseKeyPressed(args);
            }
            else
            {
                current.RaiseKeyReleased(args);
            }
        }
    }

    private static void RouteTextInput(UiElement target, UiTextInputEventArgs args)
    {
        for (UiElement? current = target; current is not null && !args.Handled; current = current.Parent)
        {
            current.RaiseTextInput(args);
        }
    }

    private void MoveFocus(bool forward)
    {
        VerifyAccess();
        UiElement[] treeOrder = Root.DescendantsAndSelf().ToArray();
        UiElement[] focusable = treeOrder
            .Where(element => element.Focusable && element.Visibility == UiVisibility.Visible && element.IsEffectivelyEnabled)
            .OrderBy(element => element.TabIndex)
            .ThenBy(element => Array.IndexOf(treeOrder, element))
            .ToArray();
        if (focusable.Length == 0)
        {
            Focus(null);
            return;
        }

        int currentIndex = FocusedElement is null ? -1 : Array.IndexOf(focusable, FocusedElement);
        int nextIndex = forward
            ? (currentIndex + 1 + focusable.Length) % focusable.Length
            : (currentIndex - 1 + focusable.Length) % focusable.Length;
        Focus(focusable[nextIndex]);
    }

    private IDisposable EnterPhase(UiRootPhase nextPhase)
    {
        UiRootPhase previous = phase;
        phase = nextPhase;
        return new PhaseScope(this, previous);
    }

    private void LeavePhase(UiRootPhase previous)
    {
        phase = previous;
        if (phase == UiRootPhase.Idle)
        {
            FlushDeferredOperations();
        }
    }

    private void FlushDeferredOperations()
    {
        while (deferredOperations.TryDequeue(out Action? operation))
        {
            operation();
        }
    }

    private void UpdateFrameTimers()
    {
        long timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (ToolTip toolTip in Root.DescendantsAndSelf().OfType<ToolTip>().ToArray())
        {
            toolTip.UpdateFrame(timestamp);
        }
    }

    private bool HasOpenOutsideDismissPopup()
        => OpenPopups().Any(popup => popup.DismissOnOutsidePointer);

    private UiElement? ResolveInputTarget(PointF point)
        => ResolvePopupInput(point) ?? Root.ResolveInput(point);

    private UiElement? ResolvePopupInput(PointF point)
    {
        foreach (IUiPopup popup in OpenPopups()
            .OrderByDescending(openPopup => openPopup.PopupElement.ZIndex)
            .ThenByDescending(openPopup => openPopup.PopupElement.InsertionOrder))
        {
            if (!popup.ContainsPopupPoint(point))
            {
                continue;
            }

            UiElement popupElement = popup.PopupElement;
            UiElement? target = popupElement.ResolveInput(point);
            if (target is not null)
            {
                return target;
            }

            if (popupElement.ReceivesInput)
            {
                return popupElement;
            }
        }

        return null;
    }

    private IEnumerable<IUiPopup> OpenPopups()
        => Root.DescendantsAndSelf()
            .OfType<IUiPopup>()
            .Where(popup => popup.IsPopupOpen);

    private bool DismissPopupsOutside(PointF point)
    {
        IUiPopup[] popups = OpenPopups()
            .Where(popup => popup.DismissOnOutsidePointer && !popup.ContainsPopupPoint(point))
            .OrderByDescending(popup => popup.PopupElement.ZIndex)
            .ThenByDescending(popup => popup.PopupElement.InsertionOrder)
            .ToArray();
        if (popups.Length == 0)
        {
            return false;
        }

        using (EnterPhase(UiRootPhase.PopupDismissal))
        {
            foreach (IUiPopup popup in popups)
            {
                popup.DismissPopup(UiPopupDismissReason.OutsidePointer);
            }
        }

        invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion;
        return true;
    }

    private bool DismissTopmostEscapePopup()
    {
        IUiPopup? popup = OpenPopups()
            .Where(openPopup => openPopup.DismissOnEscape)
            .OrderByDescending(openPopup => openPopup.PopupElement.ZIndex)
            .ThenByDescending(openPopup => openPopup.PopupElement.InsertionOrder)
            .FirstOrDefault();
        if (popup is null)
        {
            return false;
        }

        using (EnterPhase(UiRootPhase.PopupDismissal))
        {
            popup.DismissPopup(UiPopupDismissReason.Escape);
        }

        invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion;
        return true;
    }

    private void DismissPopupsOwnedBy(UiElement element, UiPopupDismissReason reason)
    {
        IUiPopup[] popups = OpenPopups()
            .Where(popup => IsSameOrDescendant(element, popup.PopupOwner) || IsSameOrDescendant(element, popup.PopupElement))
            .ToArray();
        if (popups.Length == 0)
        {
            return;
        }

        using (EnterPhase(UiRootPhase.PopupDismissal))
        {
            foreach (IUiPopup popup in popups)
            {
                popup.DismissPopup(reason);
            }
        }

        invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion;
    }

    private void DismissAllPopups(UiPopupDismissReason reason)
    {
        foreach (IUiPopup popup in OpenPopups().ToArray())
        {
            popup.DismissPopup(reason);
        }
    }

    private static bool IsSameOrDescendant(UiElement ancestor, UiElement? element)
    {
        for (UiElement? current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearHoveredDescendants(UiElement element)
    {
        UiElement[] removed = hoveredElements
            .Where(hovered => IsSameOrDescendant(element, hovered))
            .ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        foreach (UiElement hovered in removed)
        {
            hovered.IsMouseOver = false;
        }

        hoveredElements = hoveredElements
            .Where(hovered => !removed.Contains(hovered))
            .ToList();
    }

    private static List<UiElement> BuildElementPath(UiElement? element)
    {
        var path = new List<UiElement>();
        for (UiElement? current = element; current is not null; current = current.Parent)
        {
            path.Add(current);
        }

        return path;
    }

    private static bool IsUnavailableForTreeInput(UiElement element)
        => element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled;

    private bool HasExceededDragThreshold(PointF position)
    {
        float threshold = MathF.Max(0f, DragThreshold);
        float deltaX = position.X - pressedPosition.X;
        float deltaY = position.Y - pressedPosition.Y;
        return deltaX * deltaX + deltaY * deltaY >= threshold * threshold;
    }

    private void VerifyAccess()
    {
        if (ownerThreadId != 0 && Environment.CurrentManagedThreadId != ownerThreadId)
        {
            throw new InvalidOperationException("Overlay UI roots are thread-affine. Mutate and render the UI tree from the owning overlay thread or use a root-owned deferred operation API.");
        }
    }

    private void BindAccess()
    {
        if (ownerThreadId == 0)
        {
            ownerThreadId = Environment.CurrentManagedThreadId;
        }
    }

    private readonly struct PhaseScope : IDisposable
    {
        private readonly OverlayUiRoot root;
        private readonly UiRootPhase previous;

        public PhaseScope(OverlayUiRoot root, UiRootPhase previous)
        {
            this.root = root;
            this.previous = previous;
        }

        public void Dispose() => root.LeavePhase(previous);
    }
}

internal static class UiVirtualKeys
{
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int Delete = 0x2E;
    public const int A = 0x41;
}
