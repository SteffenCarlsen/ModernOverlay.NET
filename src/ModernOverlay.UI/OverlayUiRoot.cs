using ModernOverlay.Diagnostics;

namespace ModernOverlay.UI;

/// <summary>
/// Configures a retained UI root attached to an overlay window.
/// </summary>
public sealed record OverlayUiOptions
{
    /// <summary>
    /// Gets whether the UI root registers itself as the overlay input-region resolver.
    /// </summary>
    public bool RegisterInputRegions { get; init; } = true;

    /// <summary>
    /// Gets the initial theme used to realize UI resources.
    /// </summary>
    public UiTheme Theme { get; init; } = UiTheme.Default;
}

/// <summary>
/// Entry point for attaching retained UI to an overlay window.
/// </summary>
public static class OverlayUi
{
    /// <summary>
    /// Attaches a retained UI root to an overlay window.
    /// </summary>
    /// <param name="overlay">The overlay window that owns rendering, input, and resources.</param>
    /// <param name="options">Optional UI configuration.</param>
    /// <returns>The attached UI root. Dispose it before disposing the overlay when early cleanup is needed.</returns>
    public static OverlayUiRoot Attach(OverlayWindow overlay, OverlayUiOptions? options = null)
        => new(overlay, options ?? new OverlayUiOptions());
}

/// <summary>
/// Reports runtime counters for an attached UI root.
/// </summary>
/// <param name="ElementCount">The number of attached elements, including the root canvas.</param>
/// <param name="LayoutPasses">The number of completed layout passes.</param>
/// <param name="RenderPasses">The number of completed render passes.</param>
/// <param name="InputRegionChecks">The number of input-region checks served by the root.</param>
/// <param name="RoutedEvents">The number of routed UI events dispatched by the root.</param>
/// <param name="ActivePopupCount">The number of currently open popups.</param>
public sealed record OverlayUiMetrics(
    int ElementCount,
    long LayoutPasses,
    long RenderPasses,
    long InputRegionChecks,
    long RoutedEvents,
    int ActivePopupCount);

/// <summary>
/// Owns retained UI state, input routing, layout, theme resources, and rendering for one overlay window.
/// </summary>
public sealed class OverlayUiRoot : IDisposable, IOverlayInputRegionResolver
{
    private const int MaxLayoutPassesPerFrame = 8;

    private readonly OverlayWindow overlay;
    private readonly Queue<Action> deferredOperations = [];
    private int ownerThreadId;
    private UiInvalidation invalidation = UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion;
    private RectF lastLayoutBounds;
    private UiRootPhase phase;
    private bool flushingDeferredOperations;
    private bool disposed;
    private UiElement? pressedElement;
    private PointF pressedPosition;
    private bool pressedExceededDragThreshold;
    private int pressedClickCount = 1;
    private UiElement? lastClickElement;
    private OverlayPointerButton lastClickButton;
    private PointF lastClickPosition;
    private long lastClickTimestamp;
    private float dragThreshold = 4f;
    private TimeSpan doubleClickTime = TimeSpan.FromMilliseconds(500);
    private float doubleClickDistance = 4f;
    private TimeSpan caretBlinkInterval = TimeSpan.FromMilliseconds(530);
    private long caretBlinkStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    private long layoutPasses;
    private long renderPasses;
    private long inputRegionChecks;
    private long routedEvents;
    private List<UiElement> hoveredElements = [];
    private readonly bool registeredInputRegions;
    private RectF? lastLayoutTargetBounds;
    private PointF? lastPointerPosition;
    private DrawContext? layoutMeasurementContext;

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

    /// <summary>
    /// Gets the options used to create this UI root.
    /// </summary>
    public OverlayUiOptions Options { get; }

    /// <summary>
    /// Gets the root canvas that hosts retained UI elements.
    /// </summary>
    public Canvas Root { get; }

    /// <summary>
    /// Gets the element that currently owns keyboard focus.
    /// </summary>
    public UiElement? FocusedElement { get; private set; }

    /// <summary>
    /// Gets the element that currently owns pointer capture.
    /// </summary>
    public UiElement? CapturedElement { get; private set; }

    /// <summary>
    /// Gets the realized theme resources for this root.
    /// </summary>
    public UiThemeResources ThemeResources { get; }

    /// <summary>
    /// Gets the currently active theme.
    /// </summary>
    public UiTheme Theme => ThemeResources.Theme;

    /// <summary>
    /// Gets the current overlay bounds in device-independent pixels.
    /// </summary>
    public RectF BoundsDips => overlay.BoundsDips;

    internal RectF? TargetBoundsDips => ToOverlayLocalDips(overlay.TargetBoundsPixels);

    internal PointF? LastPointerPositionDips => lastPointerPosition;

    internal bool TryMeasureText(string text, FontHandle? font, out SizeF size)
    {
        if (layoutMeasurementContext is null)
        {
            size = default;
            return false;
        }

        size = layoutMeasurementContext.Measure.Text(text, font ?? ThemeResources.Font);
        return true;
    }

    /// <summary>
    /// Gets runtime counters for layout, rendering, input, and popup state.
    /// </summary>
    public OverlayUiMetrics Metrics
    {
        get
        {
            VerifyAccess();
            UiElement[] elements = Root.DescendantsAndSelf().ToArray();
            return new OverlayUiMetrics(
                elements.Length,
                layoutPasses,
                renderPasses,
                inputRegionChecks,
                routedEvents,
                elements.OfType<IUiPopup>().Count(popup => popup.IsPopupOpen));
        }
    }

    /// <summary>
    /// Gets or sets the pointer movement distance, in DIPs, that starts a drag gesture.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the maximum interval between clicks that counts as a double-click.
    /// </summary>
    public TimeSpan DoubleClickTime
    {
        get => doubleClickTime;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Double-click time cannot be negative.");
            }

            doubleClickTime = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum pointer distance, in DIPs, between clicks that counts as a double-click.
    /// </summary>
    public float DoubleClickDistance
    {
        get => doubleClickDistance;
        set
        {
            if (value < 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Double-click distance must be finite and non-negative.");
            }

            doubleClickDistance = value;
        }
    }

    /// <summary>
    /// Gets or sets the caret blink interval for focused text controls.
    /// </summary>
    public TimeSpan CaretBlinkInterval
    {
        get => caretBlinkInterval;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Caret blink interval cannot be negative.");
            }

            caretBlinkInterval = value;
            RestartCaretBlink();
        }
    }

    internal bool IsInProtectedPhase => phase != UiRootPhase.Idle;

    internal bool IsCaretVisible
    {
        get
        {
            if (CaretBlinkInterval == TimeSpan.Zero)
            {
                return true;
            }

            long timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            long blinkSlot = (long)(Elapsed(timestamp, caretBlinkStartedTimestamp).Ticks / CaretBlinkInterval.Ticks);
            return blinkSlot % 2 == 0;
        }
    }

    /// <summary>
    /// Lays out and renders the retained UI into an overlay frame.
    /// </summary>
    /// <param name="frame">The frame draw context supplied by the overlay render callback.</param>
    public void Render(DrawContext frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        RunWithDiagnostics("Render", () =>
        {
            BindAccess();
            VerifyAccess();
            UpdateFrameTimers();
            layoutMeasurementContext = frame;
            try
            {
                EnsureLayout();
            }
            finally
            {
                layoutMeasurementContext = null;
            }

            using (EnterPhase(UiRootPhase.Render))
            {
                Root.Render(new UiRenderContext(frame, ThemeResources));
            }

            renderPasses++;
        });
    }

    /// <summary>
    /// Resolves whether a pointer position should be handled by retained UI or pass through.
    /// </summary>
    /// <param name="position">The overlay-local pointer position in DIPs.</param>
    /// <returns>The input-region result for the position.</returns>
    public OverlayInputRegionResult ResolveInputRegion(PointF position)
    {
        return disposed
            ? OverlayInputRegionResult.PassThrough
            : RunWithDiagnostics("ResolveInputRegion", () =>
        {
            BindAccess();
            VerifyAccess();
            UpdatePointerPosition(position);
            EnsureLayout();
            inputRegionChecks++;
            return ResolveInputTarget(position) is not null || HasOpenOutsideDismissPopup()
                ? OverlayInputRegionResult.Interactive
                : OverlayInputRegionResult.PassThrough;
        });
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

    /// <summary>
    /// Moves keyboard focus to an attached element, or clears focus when <paramref name="element"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="element">The element to focus, or <see langword="null"/> to clear focus.</param>
    public void Focus(UiElement? element)
    {
        VerifyAccess();
        using (EnterPhase(UiRootPhase.FocusChange))
        {
            CleanupUnavailableFocusAndCapture();
            if (element is not null && element.Root != this)
            {
                throw new InvalidOperationException("Cannot focus an element attached to another UI root.");
            }

            if (element is not null && (!element.Focusable || element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled))
            {
                throw new InvalidOperationException("Only visible, enabled, focusable UI elements can receive focus.");
            }

            FocusedElement = element;
            RestartCaretBlink();
            invalidation |= UiInvalidation.Render | UiInvalidation.FocusState;
        }
    }

    /// <summary>
    /// Captures pointer events for an attached element until released.
    /// </summary>
    /// <param name="element">The visible enabled element that should receive captured pointer events.</param>
    public void CapturePointer(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        VerifyAccess();
        CleanupUnavailableFocusAndCapture();
        if (element.Root != this)
        {
            throw new InvalidOperationException("Cannot capture pointer for an element attached to another UI root.");
        }

        if (element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException("Only visible, enabled UI elements can capture the pointer.");
        }

        if (CapturedElement is { } previousCapture && !ReferenceEquals(previousCapture, element))
        {
            using (EnterPhase(UiRootPhase.CaptureRelease))
            {
                CapturedElement = null;
                invalidation |= UiInvalidation.Render;
            }
        }

        CapturedElement = element;
        invalidation |= UiInvalidation.Render;
    }

    /// <summary>
    /// Releases pointer capture when it is owned by the specified element.
    /// </summary>
    /// <param name="element">The element requesting capture release.</param>
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

    /// <summary>
    /// Moves keyboard focus to the next focusable element.
    /// </summary>
    public void MoveFocusNext() => MoveFocus(forward: true);

    /// <summary>
    /// Moves keyboard focus to the previous focusable element.
    /// </summary>
    public void MoveFocusPrevious() => MoveFocus(forward: false);

    internal void RestartCaretBlink()
    {
        caretBlinkStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        invalidation |= UiInvalidation.Render;
    }

    /// <summary>
    /// Replaces the current theme and recreates root-owned theme resources.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    public void ApplyTheme(UiTheme theme)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(theme);
        VerifyAccess();
        RunWithDiagnostics("ApplyTheme", () =>
        {
            ThemeResources.ApplyTheme(theme);
            invalidation |= UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.Resource;
        });
    }

    /// <summary>
    /// Determines whether keyboard focus is currently on the element or one of its descendants.
    /// </summary>
    /// <param name="element">The element to test.</param>
    /// <returns><see langword="true"/> when focus is within the element subtree.</returns>
    public bool IsKeyboardFocusWithin(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        VerifyAccess();
        return IsSameOrDescendant(element, FocusedElement);
    }

    /// <summary>
    /// Runs or queues an operation so it executes outside protected layout, render, and event-dispatch phases.
    /// </summary>
    /// <param name="operation">The operation to run or queue.</param>
    public void Defer(Action operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(operation);
        VerifyAccess();
        if (phase == UiRootPhase.Idle && !flushingDeferredOperations)
        {
            operation();
            return;
        }

        deferredOperations.Enqueue(operation);
    }

    /// <summary>
    /// Detaches the UI root from the overlay and releases root-owned resources.
    /// </summary>
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
        RectF? targetBounds = TargetBoundsDips;
        if (bounds != lastLayoutBounds || targetBounds != lastLayoutTargetBounds)
        {
            invalidation |= UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion;
        }

        if ((invalidation & (UiInvalidation.Measure | UiInvalidation.Arrange)) == 0)
        {
            return;
        }

        int passes = 0;
        bool layoutStillInvalidAfterCap = false;
        do
        {
            passes++;
            invalidation &= ~(UiInvalidation.Measure | UiInvalidation.Arrange);
            var available = new SizeF(bounds.Width, bounds.Height);
            using (EnterPhase(UiRootPhase.Measure))
            {
                Root.Measure(available);
            }

            using (EnterPhase(UiRootPhase.Arrange))
            {
                Root.Arrange(new RectF(0f, 0f, available.Width, available.Height));
            }

            layoutPasses++;
            if (passes == MaxLayoutPassesPerFrame
                && (invalidation & (UiInvalidation.Measure | UiInvalidation.Arrange)) != 0)
            {
                OverlayEventSource.Log.UiLayoutLoop(passes, Root.DescendantsAndSelf().Count());
                layoutStillInvalidAfterCap = true;
                break;
            }
        }
        while ((invalidation & (UiInvalidation.Measure | UiInvalidation.Arrange)) != 0);

        lastLayoutBounds = bounds;
        lastLayoutTargetBounds = targetBounds;
        if (!layoutStillInvalidAfterCap)
        {
            invalidation &= ~(UiInvalidation.Measure | UiInvalidation.Arrange);
        }
    }

    private void HandlePointerMoved(OverlayWindow sender, OverlayPointerEventArgs args)
        => RunWithDiagnostics("PointerMoved", () => DispatchPointer(args, OverlayPointerEventKind.Moved));

    private void HandlePointerPressed(OverlayWindow sender, OverlayPointerEventArgs args)
        => RunWithDiagnostics("PointerPressed", () => DispatchPointer(args, OverlayPointerEventKind.Pressed));

    private void HandlePointerReleased(OverlayWindow sender, OverlayPointerEventArgs args)
        => RunWithDiagnostics("PointerReleased", () => DispatchPointer(args, OverlayPointerEventKind.Released));

    private void HandlePointerWheel(OverlayWindow sender, OverlayPointerEventArgs args)
        => RunWithDiagnostics("PointerWheel", () => DispatchPointer(args, OverlayPointerEventKind.Wheel));

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

        RunWithDiagnostics("KeyPressed", () =>
        {
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
        });
    }

    private void HandleKeyReleased(OverlayWindow sender, OverlayKeyboardEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        RunWithDiagnostics("KeyReleased", () =>
        {
            BindAccess();
            VerifyAccess();
            DispatchKey(args, pressed: false);
        });
    }

    private void HandleTextInput(OverlayWindow sender, OverlayTextInputEventArgs args)
    {
        if (disposed || FocusedElement is null)
        {
            return;
        }

        RunWithDiagnostics("TextInput", () =>
        {
            BindAccess();
            VerifyAccess();
            var uiArgs = new UiTextInputEventArgs(args.Text);
            using (EnterPhase(UiRootPhase.EventDispatch))
            {
                RouteTextInput(FocusedElement, uiArgs);
                routedEvents++;
            }

            invalidation |= UiInvalidation.Render;
        });
    }

    private void DispatchPointer(OverlayPointerEventArgs overlayArgs, OverlayPointerEventKind kind)
    {
        if (disposed)
        {
            return;
        }

        BindAccess();
        VerifyAccess();
        UpdatePointerPosition(overlayArgs.Position);
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
        int clickCount = kind switch
        {
            OverlayPointerEventKind.Pressed => GetNextClickCount(target, overlayArgs),
            OverlayPointerEventKind.Released => pressedClickCount,
            _ => 0,
        };
        var args = new UiPointerEventArgs(kind, overlayArgs.Button, overlayArgs.Position, overlayArgs.WheelDelta, overlayArgs.IsHorizontalWheel, isDragGesture, clickCount);
        using (EnterPhase(UiRootPhase.EventDispatch))
        {
            if (kind == OverlayPointerEventKind.Pressed)
            {
                pressedElement = target;
                pressedPosition = overlayArgs.Position;
                pressedExceededDragThreshold = false;
                pressedClickCount = clickCount;
                target.IsPressed = true;
                if (target.Focusable)
                {
                    Focus(target);
                }
            }

            RoutePointer(target, args);
            routedEvents++;

            if (kind == OverlayPointerEventKind.Released)
            {
                if (pressedElement is { } pressed)
                {
                    pressed.IsPressed = false;
                }

                if (!pressedExceededDragThreshold && pressedElement is not null)
                {
                    lastClickElement = pressedElement;
                    lastClickButton = overlayArgs.Button;
                    lastClickPosition = overlayArgs.Position;
                    lastClickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                }

                pressedElement = null;
                pressedExceededDragThreshold = false;
                pressedClickCount = 1;
            }
        }

        invalidation |= UiInvalidation.Render;
    }

    private void UpdatePointerPosition(PointF position)
    {
        if (lastPointerPosition == position)
        {
            return;
        }

        lastPointerPosition = position;
        invalidation |= UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion;
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
        args.OriginalSource = target;
        foreach (UiElement current in BuildRoute(target))
        {
            if (args.Handled)
            {
                break;
            }

            args.Source = current;
            args.RoutePhase = ReferenceEquals(current, target) ? UiRoutedEventPhase.Direct : UiRoutedEventPhase.Bubble;
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
            routedEvents++;
        }

        invalidation |= UiInvalidation.Render;
    }

    private static void RouteKey(UiElement target, UiKeyboardEventArgs args, bool pressed)
    {
        args.OriginalSource = target;
        foreach (UiElement current in BuildRoute(target))
        {
            if (args.Handled)
            {
                break;
            }

            args.Source = current;
            args.RoutePhase = ReferenceEquals(current, target) ? UiRoutedEventPhase.Direct : UiRoutedEventPhase.Bubble;
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
        args.OriginalSource = target;
        foreach (UiElement current in BuildRoute(target))
        {
            if (args.Handled)
            {
                break;
            }

            args.Source = current;
            args.RoutePhase = ReferenceEquals(current, target) ? UiRoutedEventPhase.Direct : UiRoutedEventPhase.Bubble;
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
        if (flushingDeferredOperations)
        {
            return;
        }

        flushingDeferredOperations = true;
        try
        {
            while (deferredOperations.TryDequeue(out Action? operation))
            {
                operation();
            }
        }
        finally
        {
            flushingDeferredOperations = false;
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

    private static UiElement[] BuildRoute(UiElement target)
        => BuildElementPath(target).ToArray();

    private static bool IsUnavailableForTreeInput(UiElement element)
        => element.Visibility != UiVisibility.Visible || !element.IsEffectivelyEnabled;

    private void CleanupUnavailableFocusAndCapture()
    {
        UiElement? focused = FocusedElement;
        if (focused is not null
            && (focused.Root != this
                || focused.Visibility != UiVisibility.Visible
                || !focused.IsEffectivelyEnabled
                || !focused.Focusable))
        {
            DismissPopupsOwnedBy(focused, UiPopupDismissReason.OwnerUnavailable);
            FocusedElement = null;
            invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState;
        }

        UiElement? captured = CapturedElement;
        if (captured is not null
            && (captured.Root != this
                || captured.Visibility != UiVisibility.Visible
                || !captured.IsEffectivelyEnabled))
        {
            DismissPopupsOwnedBy(captured, UiPopupDismissReason.OwnerUnavailable);
            using (EnterPhase(UiRootPhase.CaptureRelease))
            {
                CapturedElement = null;
                invalidation |= UiInvalidation.Render | UiInvalidation.InputRegion;
            }
        }
    }

    private bool HasExceededDragThreshold(PointF position)
    {
        float threshold = MathF.Max(0f, DragThreshold);
        float deltaX = position.X - pressedPosition.X;
        float deltaY = position.Y - pressedPosition.Y;
        return deltaX * deltaX + deltaY * deltaY >= threshold * threshold;
    }

    private int GetNextClickCount(UiElement target, OverlayPointerEventArgs args)
    {
        if (args.Button == OverlayPointerButton.None || lastClickElement is null || !ReferenceEquals(lastClickElement, target))
        {
            return 1;
        }

        if (lastClickButton != args.Button)
        {
            return 1;
        }

        long timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        if (Elapsed(timestamp, lastClickTimestamp) > DoubleClickTime)
        {
            return 1;
        }

        float deltaX = args.Position.X - lastClickPosition.X;
        float deltaY = args.Position.Y - lastClickPosition.Y;
        float maxDistance = DoubleClickDistance;
        return deltaX * deltaX + deltaY * deltaY <= maxDistance * maxDistance ? 2 : 1;
    }

    private static TimeSpan Elapsed(long currentTimestamp, long startTimestamp)
        => TimeSpan.FromSeconds((currentTimestamp - startTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency);

    private RectF? ToOverlayLocalDips(WindowBounds? bounds)
    {
        if (bounds is not { IsEmpty: false } targetBounds)
        {
            return null;
        }

        DpiScale dpi = overlay.DpiScale;
        if (!IsUsableDpi(dpi))
        {
            return null;
        }

        WindowBounds overlayBounds = overlay.BoundsPixels;
        return new RectF(
            (targetBounds.X - overlayBounds.X) / dpi.X,
            (targetBounds.Y - overlayBounds.Y) / dpi.Y,
            targetBounds.Width / dpi.X,
            targetBounds.Height / dpi.Y);
    }

    private static bool IsUsableDpi(DpiScale dpi)
        => float.IsFinite(dpi.X) && dpi.X > 0f && float.IsFinite(dpi.Y) && dpi.Y > 0f;

    internal static void LogInvalidPlacement(UiElement element, string placementKind, string reason)
    {
        string elementName = string.IsNullOrWhiteSpace(element.Name)
            ? element.GetType().Name
            : element.Name!;
        OverlayEventSource.Log.UiInvalidPlacement(elementName, placementKind, reason);
    }

    private static void RunWithDiagnostics(string phaseName, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            LogUnhandledException(phaseName, ex);
            throw;
        }
    }

    private static T RunWithDiagnostics<T>(string phaseName, Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            LogUnhandledException(phaseName, ex);
            throw;
        }
    }

    private static void LogUnhandledException(string phaseName, Exception exception)
        => OverlayEventSource.Log.UiUnhandledException(
            phaseName,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message);

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
