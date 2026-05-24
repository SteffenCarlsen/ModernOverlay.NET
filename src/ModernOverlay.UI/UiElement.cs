using ModernOverlay.Diagnostics;

namespace ModernOverlay.UI;

/// <summary>
/// Base class for retained UI elements.
/// </summary>
public abstract class UiElement
{
    private string? name;
    private object? tag;
    private UiVisibility visibility = UiVisibility.Visible;
    private bool isEnabled = true;
    private bool receivesInput;
    private UiInputRegionHandler? inputRegion;
    private bool focusable;
    private int tabIndex;
    private float opacity = 1f;
    private int zIndex;
    private Thickness margin;
    private Thickness padding;
    private float width = float.NaN;
    private float height = float.NaN;
    private UiConstraints constraints = UiConstraints.Unbounded;
    private UiHorizontalAlignment horizontalAlignment = UiHorizontalAlignment.Stretch;
    private UiVerticalAlignment verticalAlignment = UiVerticalAlignment.Stretch;
    private BrushHandle? background;
    private BrushHandle? foreground;
    private BrushHandle? borderBrush;
    private BrushHandle? accentBrush;
    private BrushHandle? disabledBrush;
    private BrushHandle? hoverBackground;
    private BrushHandle? pressedBackground;
    private BrushHandle? focusBrush;
    private BrushHandle? popupBackground;
    private BrushHandle? windowChromeBackground;
    private HashSet<long>? loggedDisposedResourceFallbacks;

    internal int InsertionOrder { get; set; }

    /// <summary>
    /// Occurs when a pointer move routes to the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerMoved;

    /// <summary>
    /// Occurs when the pointer enters the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerEntered;

    /// <summary>
    /// Occurs when the pointer exits the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerExited;

    /// <summary>
    /// Occurs when a pointer press routes to the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerPressed;

    /// <summary>
    /// Occurs when a pointer release routes to the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerReleased;

    /// <summary>
    /// Occurs when a pointer wheel event routes to the element.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerWheel;

    /// <summary>
    /// Occurs when a key press routes to the focused element.
    /// </summary>
    public event EventHandler<UiKeyboardEventArgs>? KeyPressed;

    /// <summary>
    /// Occurs when a key release routes to the focused element.
    /// </summary>
    public event EventHandler<UiKeyboardEventArgs>? KeyReleased;

    /// <summary>
    /// Occurs when text input routes to the focused element.
    /// </summary>
    public event EventHandler<UiTextInputEventArgs>? TextInput;

    /// <summary>
    /// Occurs when the element is attached to a UI root.
    /// </summary>
    public event EventHandler? Attached;

    /// <summary>
    /// Occurs when the element is detached from a UI root.
    /// </summary>
    public event EventHandler? Detached;

    /// <summary>
    /// Gets or sets an optional element name for diagnostics and lookup by application code.
    /// </summary>
    public string? Name
    {
        get => name;
        set => SetProperty(ref name, value, UiInvalidation.None);
    }

    /// <summary>
    /// Gets or sets arbitrary application data associated with the element.
    /// </summary>
    public object? Tag
    {
        get => tag;
        set => SetProperty(ref tag, value, UiInvalidation.None);
    }

    /// <summary>
    /// Gets the parent panel, or <see langword="null"/> when the element is detached.
    /// </summary>
    public UiPanel? Parent { get; private set; }

    /// <summary>
    /// Gets the owning UI root, or <see langword="null"/> when the element is detached.
    /// </summary>
    public OverlayUiRoot? Root { get; private set; }

    /// <summary>
    /// Gets or sets whether the element is visible, hidden, or collapsed.
    /// </summary>
    public UiVisibility Visibility
    {
        get => visibility;
        set => SetProperty(ref visibility, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    /// <summary>
    /// Gets or sets whether the element is visible; setting <see langword="false"/> collapses the element.
    /// </summary>
    public bool IsVisible
    {
        get => Visibility == UiVisibility.Visible;
        set => Visibility = value ? UiVisibility.Visible : UiVisibility.Collapsed;
    }

    /// <summary>
    /// Gets or sets whether the element can render as enabled and participate in input.
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value, UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    /// <summary>
    /// Gets whether this element and all ancestors are enabled.
    /// </summary>
    public bool IsEffectivelyEnabled => IsEnabled && (Parent?.IsEffectivelyEnabled ?? true);

    /// <summary>
    /// Gets or sets whether this element can be returned by hit testing.
    /// </summary>
    public bool ReceivesInput
    {
        get => receivesInput;
        set => SetProperty(ref receivesInput, value, UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets a custom input-region predicate for selective click-through.
    /// </summary>
    public UiInputRegionHandler? InputRegion
    {
        get => inputRegion;
        set => SetProperty(ref inputRegion, value, UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets whether the element can receive keyboard focus.
    /// </summary>
    public bool Focusable
    {
        get => focusable;
        set => SetProperty(ref focusable, value, UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    /// <summary>
    /// Gets or sets the tab-order index used by focus navigation.
    /// </summary>
    public int TabIndex
    {
        get => tabIndex;
        set => SetProperty(ref tabIndex, value, UiInvalidation.FocusState);
    }

    /// <summary>
    /// Gets or sets element opacity from 0 to 1.
    /// </summary>
    public float Opacity
    {
        get => opacity;
        set
        {
            if (value is < 0f or > 1f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Opacity must be finite and between 0 and 1.");
            }

            SetProperty(ref opacity, value, UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets the z-order within the parent panel.
    /// </summary>
    public int ZIndex
    {
        get => zIndex;
        set => SetProperty(ref zIndex, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the outer layout margin.
    /// </summary>
    public Thickness Margin
    {
        get => margin;
        set => SetProperty(ref margin, value, UiInvalidation.Measure);
    }

    /// <summary>
    /// Gets or sets inner padding used by the element.
    /// </summary>
    public Thickness Padding
    {
        get => padding;
        set => SetProperty(ref padding, value, UiInvalidation.Measure);
    }

    /// <summary>
    /// Gets or sets the explicit width in DIPs, or <see cref="float.NaN"/> for automatic width.
    /// </summary>
    public float Width
    {
        get => width;
        set => SetLayoutDimension(ref width, value, allowAuto: true);
    }

    /// <summary>
    /// Gets or sets the explicit height in DIPs, or <see cref="float.NaN"/> for automatic height.
    /// </summary>
    public float Height
    {
        get => height;
        set => SetLayoutDimension(ref height, value, allowAuto: true);
    }

    /// <summary>
    /// Gets or sets the minimum layout width in DIPs.
    /// </summary>
    public float MinWidth
    {
        get => constraints.MinWidth;
        set => SetConstraints(constraints.WithMinWidth(value));
    }

    /// <summary>
    /// Gets or sets the minimum layout height in DIPs.
    /// </summary>
    public float MinHeight
    {
        get => constraints.MinHeight;
        set => SetConstraints(constraints.WithMinHeight(value));
    }

    /// <summary>
    /// Gets or sets the maximum layout width in DIPs.
    /// </summary>
    public float MaxWidth
    {
        get => constraints.MaxWidth;
        set => SetConstraints(constraints.WithMaxWidth(value));
    }

    /// <summary>
    /// Gets or sets the maximum layout height in DIPs.
    /// </summary>
    public float MaxHeight
    {
        get => constraints.MaxHeight;
        set => SetConstraints(constraints.WithMaxHeight(value));
    }

    /// <summary>
    /// Gets or sets combined min/max layout constraints.
    /// </summary>
    public UiConstraints Constraints
    {
        get => constraints;
        set => SetConstraints(value);
    }

    /// <summary>
    /// Gets or sets horizontal alignment within the assigned layout slot.
    /// </summary>
    public UiHorizontalAlignment HorizontalAlignment
    {
        get => horizontalAlignment;
        set => SetProperty(ref horizontalAlignment, value, UiInvalidation.Arrange);
    }

    /// <summary>
    /// Gets or sets vertical alignment within the assigned layout slot.
    /// </summary>
    public UiVerticalAlignment VerticalAlignment
    {
        get => verticalAlignment;
        set => SetProperty(ref verticalAlignment, value, UiInvalidation.Arrange);
    }

    /// <summary>
    /// Gets or sets the background brush override.
    /// </summary>
    public BrushHandle? Background
    {
        get => background;
        set => SetProperty(ref background, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the foreground brush override.
    /// </summary>
    public BrushHandle? Foreground
    {
        get => foreground;
        set => SetProperty(ref foreground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the border brush override.
    /// </summary>
    public BrushHandle? BorderBrush
    {
        get => borderBrush;
        set => SetProperty(ref borderBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the accent brush override.
    /// </summary>
    public BrushHandle? AccentBrush
    {
        get => accentBrush;
        set => SetProperty(ref accentBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the disabled-state brush override.
    /// </summary>
    public BrushHandle? DisabledBrush
    {
        get => disabledBrush;
        set => SetProperty(ref disabledBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the hover background brush override.
    /// </summary>
    public BrushHandle? HoverBackground
    {
        get => hoverBackground;
        set => SetProperty(ref hoverBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the pressed background brush override.
    /// </summary>
    public BrushHandle? PressedBackground
    {
        get => pressedBackground;
        set => SetProperty(ref pressedBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the focus brush override.
    /// </summary>
    public BrushHandle? FocusBrush
    {
        get => focusBrush;
        set => SetProperty(ref focusBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the popup background brush override.
    /// </summary>
    public BrushHandle? PopupBackground
    {
        get => popupBackground;
        set => SetProperty(ref popupBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets the window chrome background brush override.
    /// </summary>
    public BrushHandle? WindowChromeBackground
    {
        get => windowChromeBackground;
        set => SetProperty(ref windowChromeBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets whether the pointer is currently over the element.
    /// </summary>
    public bool IsMouseOver { get; internal set; }

    /// <summary>
    /// Gets whether the element is currently pressed.
    /// </summary>
    public bool IsPressed { get; internal set; }

    /// <summary>
    /// Gets whether the element currently owns keyboard focus.
    /// </summary>
    public bool IsFocused => Root?.FocusedElement == this;

    /// <summary>
    /// Gets whether keyboard focus is on this element or one of its descendants.
    /// </summary>
    public bool IsKeyboardFocusWithin => Root?.IsKeyboardFocusWithin(this) == true;

    /// <summary>
    /// Gets whether this element owns pointer capture.
    /// </summary>
    public bool IsPointerCaptured => Root?.CapturedElement == this;

    /// <summary>
    /// Gets the effective visual state used by built-in controls.
    /// </summary>
    public UiVisualState VisualState
        => !IsEffectivelyEnabled
            ? UiVisualState.Disabled
            : IsPressed || IsPointerCaptured
                ? UiVisualState.Pressed
                : IsMouseOver
                    ? UiVisualState.Hover
                    : IsFocused ? UiVisualState.Focused : UiVisualState.Normal;

    /// <summary>
    /// Gets the size requested during the last measure pass.
    /// </summary>
    public SizeF DesiredSize { get; private set; }

    /// <summary>
    /// Gets the arranged bounds in overlay-local DIPs.
    /// </summary>
    public RectF Bounds { get; private set; }

    /// <summary>
    /// Gets arranged bounds deflated by <see cref="Padding"/>.
    /// </summary>
    public RectF ContentBounds => UiGeometry.Deflate(Bounds, Padding);

    /// <summary>
    /// Invalidates measure, arrange, render, and input-region state.
    /// </summary>
    public void InvalidateMeasure() => Root?.Invalidate(UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion);

    /// <summary>
    /// Invalidates arrange, render, and input-region state.
    /// </summary>
    public void InvalidateArrange() => Root?.Invalidate(UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion);

    /// <summary>
    /// Invalidates render state.
    /// </summary>
    public void InvalidateRender() => Root?.Invalidate(UiInvalidation.Render);

    /// <summary>
    /// Moves keyboard focus to this element.
    /// </summary>
    public void Focus()
    {
        if (!Focusable)
        {
            throw new InvalidOperationException("Only focusable UI elements can receive focus.");
        }

        Root?.Focus(this);
    }

    /// <summary>
    /// Clears keyboard focus if this element currently owns it.
    /// </summary>
    public void Blur()
    {
        if (Root?.FocusedElement == this)
        {
            Root.Focus(null);
        }
    }

    /// <summary>
    /// Moves keyboard focus to the next focusable element in the root.
    /// </summary>
    public void MoveFocusNext() => Root?.MoveFocusNext();

    /// <summary>
    /// Moves keyboard focus to the previous focusable element in the root.
    /// </summary>
    public void MoveFocusPrevious() => Root?.MoveFocusPrevious();

    /// <summary>
    /// Captures pointer events for this element.
    /// </summary>
    public void CapturePointer() => Root?.CapturePointer(this);

    /// <summary>
    /// Releases pointer capture if this element owns it.
    /// </summary>
    public void ReleasePointerCapture() => Root?.ReleasePointerCapture(this);

    internal void Attach(UiPanel parent, OverlayUiRoot? root)
    {
        Parent = parent;
        SetRoot(root);
    }

    internal void Detach()
    {
        SetRoot(null);
        Parent = null;
    }

    internal void SetRoot(OverlayUiRoot? root)
    {
        if (Root == root)
        {
            return;
        }

        OverlayUiRoot? previousRoot = Root;
        if (previousRoot is not null)
        {
            previousRoot.NotifyElementDetached(this);
            OnDetached();
            Detached?.Invoke(this, EventArgs.Empty);
        }

        Root = root;
        if (Root is not null)
        {
            OnAttached();
            Attached?.Invoke(this, EventArgs.Empty);
        }

        if (this is UiPanel panel)
        {
            foreach (UiElement child in panel.Children)
            {
                child.SetRoot(root);
            }
        }
    }

    internal SizeF Measure(SizeF availableSize)
    {
        if (Visibility == UiVisibility.Collapsed)
        {
            DesiredSize = new SizeF(0f, 0f);
            return DesiredSize;
        }

        SizeF innerAvailable = UiGeometry.Deflate(availableSize, Margin);
        innerAvailable = ApplyExplicitConstraints(innerAvailable, applyingAvailableSize: true);
        SizeF measured = MeasureCore(innerAvailable);
        measured = ApplyExplicitConstraints(measured, applyingAvailableSize: false);
        DesiredSize = UiGeometry.Inflate(measured, Margin);
        return DesiredSize;
    }

    internal void Arrange(RectF finalRect)
    {
        if (Visibility == UiVisibility.Collapsed)
        {
            Bounds = new RectF(finalRect.X, finalRect.Y, 0f, 0f);
            return;
        }

        RectF contentSlot = UiGeometry.Deflate(finalRect, Margin);
        SizeF desired = constraints.Constrain(
            new SizeF(
                float.IsNaN(width) ? contentSlot.Width : width,
                float.IsNaN(height) ? contentSlot.Height : height));

        float arrangedWidth = horizontalAlignment == UiHorizontalAlignment.Stretch
            ? contentSlot.Width
            : MathF.Min(desired.Width, contentSlot.Width);
        float arrangedHeight = verticalAlignment == UiVerticalAlignment.Stretch
            ? contentSlot.Height
            : MathF.Min(desired.Height, contentSlot.Height);
        float x = AlignX(contentSlot, arrangedWidth);
        float y = AlignY(contentSlot, arrangedHeight);
        Bounds = new RectF(x, y, arrangedWidth, arrangedHeight);
        ArrangeCore(Bounds);
    }

    internal void Render(UiRenderContext context)
    {
        if (Visibility != UiVisibility.Visible || Opacity <= 0f || Bounds.IsEmpty)
        {
            return;
        }

        using ScopedClip _ = context.Draw.Clip(Bounds);
        RenderCore(context);
    }

    internal UiElement? ResolveInput(PointF point)
    {
        if (!CanParticipateInInput() || !UiGeometry.ContainsInputBand(Bounds, point))
        {
            return null;
        }

        if (this is UiPanel panel)
        {
            foreach (UiElement child in panel.ChildrenInReverseInputOrder())
            {
                UiElement? match = child.ResolveInput(point);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return ReceivesInput && ResolveInputRegion(point) ? this : null;
    }

    internal void RaisePointerMoved(UiPointerEventArgs args)
    {
        OnPointerMoved(args);
        PointerMoved?.Invoke(this, args);
    }

    internal void RaisePointerEntered(UiPointerEventArgs args)
    {
        OnPointerEntered(args);
        PointerEntered?.Invoke(this, args);
    }

    internal void RaisePointerExited(UiPointerEventArgs args)
    {
        OnPointerExited(args);
        PointerExited?.Invoke(this, args);
    }

    internal void RaisePointerPressed(UiPointerEventArgs args)
    {
        OnPointerPressed(args);
        PointerPressed?.Invoke(this, args);
    }

    internal void RaisePointerReleased(UiPointerEventArgs args)
    {
        OnPointerReleased(args);
        PointerReleased?.Invoke(this, args);
    }

    internal void RaisePointerWheel(UiPointerEventArgs args)
    {
        OnPointerWheel(args);
        PointerWheel?.Invoke(this, args);
    }

    internal void RaiseKeyPressed(UiKeyboardEventArgs args)
    {
        OnKeyPressed(args);
        KeyPressed?.Invoke(this, args);
    }

    internal void RaiseKeyReleased(UiKeyboardEventArgs args)
    {
        OnKeyReleased(args);
        KeyReleased?.Invoke(this, args);
    }

    internal void RaiseTextInput(UiTextInputEventArgs args)
    {
        OnTextInput(args);
        TextInput?.Invoke(this, args);
    }

    protected virtual SizeF MeasureCore(SizeF availableSize) => new(0f, 0f);

    protected virtual void ArrangeCore(RectF finalRect)
    {
    }

    protected virtual void RenderCore(UiRenderContext context)
    {
    }

    protected virtual bool HitTestCore(PointF point) => UiGeometry.ContainsInputBand(Bounds, point);

    protected virtual bool ResolveInputRegion(PointF point)
        => InputRegion?.Invoke(this, point) ?? HitTestCore(point);

    protected BrushHandle ResolveBackground(UiRenderContext context)
        => VisualState switch
        {
            UiVisualState.Disabled => ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled,
            UiVisualState.Pressed => ResolveBrushOverride(nameof(PressedBackground), PressedBackground)
                ?? ResolveBrushOverride(nameof(Background), Background)
                ?? context.Theme.SurfacePressed,
            UiVisualState.Hover => ResolveBrushOverride(nameof(HoverBackground), HoverBackground)
                ?? ResolveBrushOverride(nameof(Background), Background)
                ?? context.Theme.SurfaceHover,
            _ => ResolveBrushOverride(nameof(Background), Background) ?? context.Theme.Surface,
        };

    protected BrushHandle ResolveForeground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? ResolveBrushOverride(nameof(Foreground), Foreground) ?? context.Theme.Foreground
            : ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled;

    protected BrushHandle ResolveBorderBrush(UiRenderContext context)
        => ResolveBrushOverride(nameof(BorderBrush), BorderBrush) ?? context.Theme.Border;

    protected BrushHandle ResolveAccentBrush(UiRenderContext context)
        => IsEffectivelyEnabled
            ? ResolveBrushOverride(nameof(AccentBrush), AccentBrush) ?? context.Theme.Accent
            : ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled;

    protected BrushHandle ResolveDisabledBrush(UiRenderContext context)
        => ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled;

    protected BrushHandle ResolveFocusBrush(UiRenderContext context)
        => ResolveBrushOverride(nameof(FocusBrush), FocusBrush)
            ?? ResolveBrushOverride(nameof(AccentBrush), AccentBrush)
            ?? context.Theme.Accent;

    protected BrushHandle ResolvePopupBackground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? ResolveBrushOverride(nameof(PopupBackground), PopupBackground)
                ?? ResolveBrushOverride(nameof(Background), Background)
                ?? context.Theme.Surface
            : ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled;

    protected BrushHandle ResolveWindowChromeBackground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? ResolveBrushOverride(nameof(WindowChromeBackground), WindowChromeBackground)
                ?? ResolveBrushOverride(nameof(HoverBackground), HoverBackground)
                ?? context.Theme.SurfaceHover
            : ResolveBrushOverride(nameof(DisabledBrush), DisabledBrush) ?? context.Theme.Disabled;

    protected FontHandle ResolveFontOverride(string propertyName, FontHandle? font, FontHandle fallback)
    {
        if (font is null)
        {
            return fallback;
        }

        if (!font.IsDisposed)
        {
            return font;
        }

        LogDisposedResourceFallback(propertyName, font, "Font override is disposed; using theme fallback.");
        return fallback;
    }

    private BrushHandle? ResolveBrushOverride(string propertyName, BrushHandle? brush)
    {
        if (brush is null)
        {
            return null;
        }

        if (!brush.IsDisposed)
        {
            return brush;
        }

        LogDisposedResourceFallback(propertyName, brush, "Brush override is disposed; using theme fallback.");
        return null;
    }

    private void LogDisposedResourceFallback(string propertyName, OverlayResourceHandle resource, string message)
    {
        loggedDisposedResourceFallbacks ??= [];
        if (!loggedDisposedResourceFallbacks.Add(resource.Id))
        {
            return;
        }

        string elementName = string.IsNullOrWhiteSpace(Name)
            ? GetType().Name
            : Name!;
        OverlayEventSource.Log.UiResourceRealizationFailure(
            $"{elementName}.{propertyName}",
            nameof(ObjectDisposedException),
            message);
    }

    protected virtual void OnPointerMoved(UiPointerEventArgs args)
    {
    }

    protected virtual void OnPointerEntered(UiPointerEventArgs args)
    {
    }

    protected virtual void OnPointerExited(UiPointerEventArgs args)
    {
    }

    protected virtual void OnPointerPressed(UiPointerEventArgs args)
    {
    }

    protected virtual void OnPointerReleased(UiPointerEventArgs args)
    {
    }

    protected virtual void OnPointerWheel(UiPointerEventArgs args)
    {
    }

    protected virtual void OnKeyPressed(UiKeyboardEventArgs args)
    {
    }

    protected virtual void OnKeyReleased(UiKeyboardEventArgs args)
    {
    }

    protected virtual void OnTextInput(UiTextInputEventArgs args)
    {
    }

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }

    private protected bool SetProperty<T>(ref T field, T value, UiInvalidation invalidation)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Root?.Invalidate(invalidation);
        if ((invalidation & UiInvalidation.FocusState) != 0)
        {
            Root?.NotifyElementStateChanged(this);
        }

        return true;
    }

    private protected void SetLayoutBounds(RectF bounds)
    {
        Bounds = bounds;
    }

    private bool CanParticipateInInput()
        => Visibility == UiVisibility.Visible
            && IsEffectivelyEnabled
            && Opacity > 0f;

    private void SetLayoutDimension(ref float field, float value, bool allowAuto)
    {
        if (float.IsNaN(value))
        {
            if (!allowAuto)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "This layout dimension does not support auto sizing.");
            }
        }
        else if (value < 0f || (!float.IsFinite(value) && !float.IsPositiveInfinity(value)))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Layout dimensions must be non-negative finite values, positive infinity, or auto.");
        }

        SetProperty(ref field, value, UiInvalidation.Measure);
    }

    private void SetConstraints(UiConstraints value)
        => SetProperty(ref constraints, value, UiInvalidation.Measure);

    private SizeF ApplyExplicitConstraints(SizeF size, bool applyingAvailableSize)
    {
        float nextWidth = size.Width;
        float nextHeight = size.Height;
        if (!applyingAvailableSize)
        {
            if (!float.IsNaN(width))
            {
                nextWidth = width;
            }

            if (!float.IsNaN(height))
            {
                nextHeight = height;
            }
        }

        return constraints.Constrain(new SizeF(nextWidth, nextHeight));
    }

    private float AlignX(RectF slot, float arrangedWidth)
        => horizontalAlignment switch
        {
            UiHorizontalAlignment.Center => slot.X + (slot.Width - arrangedWidth) / 2f,
            UiHorizontalAlignment.Right => slot.X + slot.Width - arrangedWidth,
            _ => slot.X,
        };

    private float AlignY(RectF slot, float arrangedHeight)
        => verticalAlignment switch
        {
            UiVerticalAlignment.Center => slot.Y + (slot.Height - arrangedHeight) / 2f,
            UiVerticalAlignment.Bottom => slot.Y + slot.Height - arrangedHeight,
            _ => slot.Y,
        };
}
