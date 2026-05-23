namespace ModernOverlay.UI;

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
    private float minWidth;
    private float minHeight;
    private float maxWidth = float.PositiveInfinity;
    private float maxHeight = float.PositiveInfinity;
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

    internal int InsertionOrder { get; set; }

    public event EventHandler<UiPointerEventArgs>? PointerMoved;

    public event EventHandler<UiPointerEventArgs>? PointerEntered;

    public event EventHandler<UiPointerEventArgs>? PointerExited;

    public event EventHandler<UiPointerEventArgs>? PointerPressed;

    public event EventHandler<UiPointerEventArgs>? PointerReleased;

    public event EventHandler<UiPointerEventArgs>? PointerWheel;

    public event EventHandler<UiKeyboardEventArgs>? KeyPressed;

    public event EventHandler<UiKeyboardEventArgs>? KeyReleased;

    public event EventHandler<UiTextInputEventArgs>? TextInput;

    public event EventHandler? Attached;

    public event EventHandler? Detached;

    public string? Name
    {
        get => name;
        set => SetProperty(ref name, value, UiInvalidation.None);
    }

    public object? Tag
    {
        get => tag;
        set => SetProperty(ref tag, value, UiInvalidation.None);
    }

    public UiPanel? Parent { get; private set; }

    public OverlayUiRoot? Root { get; private set; }

    public UiVisibility Visibility
    {
        get => visibility;
        set => SetProperty(ref visibility, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    public bool IsVisible
    {
        get => Visibility == UiVisibility.Visible;
        set => Visibility = value ? UiVisibility.Visible : UiVisibility.Collapsed;
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value, UiInvalidation.Render | UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    public bool IsEffectivelyEnabled => IsEnabled && (Parent?.IsEffectivelyEnabled ?? true);

    public bool ReceivesInput
    {
        get => receivesInput;
        set => SetProperty(ref receivesInput, value, UiInvalidation.InputRegion);
    }

    public UiInputRegionHandler? InputRegion
    {
        get => inputRegion;
        set => SetProperty(ref inputRegion, value, UiInvalidation.InputRegion);
    }

    public bool Focusable
    {
        get => focusable;
        set => SetProperty(ref focusable, value, UiInvalidation.InputRegion | UiInvalidation.FocusState);
    }

    public int TabIndex
    {
        get => tabIndex;
        set => SetProperty(ref tabIndex, value, UiInvalidation.FocusState);
    }

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

    public int ZIndex
    {
        get => zIndex;
        set => SetProperty(ref zIndex, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    public Thickness Margin
    {
        get => margin;
        set => SetProperty(ref margin, value, UiInvalidation.Measure);
    }

    public Thickness Padding
    {
        get => padding;
        set => SetProperty(ref padding, value, UiInvalidation.Measure);
    }

    public float Width
    {
        get => width;
        set => SetLayoutDimension(ref width, value, allowAuto: true);
    }

    public float Height
    {
        get => height;
        set => SetLayoutDimension(ref height, value, allowAuto: true);
    }

    public float MinWidth
    {
        get => minWidth;
        set => SetLayoutDimension(ref minWidth, value, allowAuto: false);
    }

    public float MinHeight
    {
        get => minHeight;
        set => SetLayoutDimension(ref minHeight, value, allowAuto: false);
    }

    public float MaxWidth
    {
        get => maxWidth;
        set => SetLayoutDimension(ref maxWidth, value, allowAuto: false);
    }

    public float MaxHeight
    {
        get => maxHeight;
        set => SetLayoutDimension(ref maxHeight, value, allowAuto: false);
    }

    public UiHorizontalAlignment HorizontalAlignment
    {
        get => horizontalAlignment;
        set => SetProperty(ref horizontalAlignment, value, UiInvalidation.Arrange);
    }

    public UiVerticalAlignment VerticalAlignment
    {
        get => verticalAlignment;
        set => SetProperty(ref verticalAlignment, value, UiInvalidation.Arrange);
    }

    public BrushHandle? Background
    {
        get => background;
        set => SetProperty(ref background, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? Foreground
    {
        get => foreground;
        set => SetProperty(ref foreground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? BorderBrush
    {
        get => borderBrush;
        set => SetProperty(ref borderBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? AccentBrush
    {
        get => accentBrush;
        set => SetProperty(ref accentBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? DisabledBrush
    {
        get => disabledBrush;
        set => SetProperty(ref disabledBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? HoverBackground
    {
        get => hoverBackground;
        set => SetProperty(ref hoverBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? PressedBackground
    {
        get => pressedBackground;
        set => SetProperty(ref pressedBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? FocusBrush
    {
        get => focusBrush;
        set => SetProperty(ref focusBrush, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? PopupBackground
    {
        get => popupBackground;
        set => SetProperty(ref popupBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public BrushHandle? WindowChromeBackground
    {
        get => windowChromeBackground;
        set => SetProperty(ref windowChromeBackground, value, UiInvalidation.Render | UiInvalidation.Resource);
    }

    public bool IsMouseOver { get; internal set; }

    public bool IsPressed { get; internal set; }

    public bool IsFocused => Root?.FocusedElement == this;

    public bool IsKeyboardFocusWithin => Root?.IsKeyboardFocusWithin(this) == true;

    public bool IsPointerCaptured => Root?.CapturedElement == this;

    public UiVisualState VisualState
        => !IsEffectivelyEnabled
            ? UiVisualState.Disabled
            : IsPressed || IsPointerCaptured
                ? UiVisualState.Pressed
                : IsMouseOver
                    ? UiVisualState.Hover
                    : IsFocused ? UiVisualState.Focused : UiVisualState.Normal;

    public SizeF DesiredSize { get; private set; }

    public RectF Bounds { get; private set; }

    public RectF ContentBounds => UiGeometry.Deflate(Bounds, Padding);

    public void InvalidateMeasure() => Root?.Invalidate(UiInvalidation.Measure | UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion);

    public void InvalidateArrange() => Root?.Invalidate(UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion);

    public void InvalidateRender() => Root?.Invalidate(UiInvalidation.Render);

    public void Focus()
    {
        if (!Focusable)
        {
            throw new InvalidOperationException("Only focusable UI elements can receive focus.");
        }

        Root?.Focus(this);
    }

    public void Blur()
    {
        if (Root?.FocusedElement == this)
        {
            Root.Focus(null);
        }
    }

    public void MoveFocusNext() => Root?.MoveFocusNext();

    public void MoveFocusPrevious() => Root?.MoveFocusPrevious();

    public void CapturePointer() => Root?.CapturePointer(this);

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
        SizeF desired = UiGeometry.Clamp(
            new SizeF(
                float.IsNaN(width) ? contentSlot.Width : width,
                float.IsNaN(height) ? contentSlot.Height : height),
            minWidth,
            minHeight,
            maxWidth,
            maxHeight);

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
        if (!CanParticipateInInput() || !UiGeometry.Contains(Bounds, point))
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

    protected virtual bool HitTestCore(PointF point) => UiGeometry.Contains(Bounds, point);

    protected virtual bool ResolveInputRegion(PointF point)
        => InputRegion?.Invoke(this, point) ?? HitTestCore(point);

    protected BrushHandle ResolveBackground(UiRenderContext context)
        => VisualState switch
        {
            UiVisualState.Disabled => DisabledBrush ?? context.Theme.Disabled,
            UiVisualState.Pressed => PressedBackground ?? Background ?? context.Theme.SurfacePressed,
            UiVisualState.Hover => HoverBackground ?? Background ?? context.Theme.SurfaceHover,
            _ => Background ?? context.Theme.Surface,
        };

    protected BrushHandle ResolveForeground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? Foreground ?? context.Theme.Foreground
            : DisabledBrush ?? context.Theme.Disabled;

    protected BrushHandle ResolveBorderBrush(UiRenderContext context)
        => BorderBrush ?? context.Theme.Border;

    protected BrushHandle ResolveAccentBrush(UiRenderContext context)
        => IsEffectivelyEnabled
            ? AccentBrush ?? context.Theme.Accent
            : DisabledBrush ?? context.Theme.Disabled;

    protected BrushHandle ResolveDisabledBrush(UiRenderContext context)
        => DisabledBrush ?? context.Theme.Disabled;

    protected BrushHandle ResolveFocusBrush(UiRenderContext context)
        => FocusBrush ?? AccentBrush ?? context.Theme.Accent;

    protected BrushHandle ResolvePopupBackground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? PopupBackground ?? Background ?? context.Theme.Surface
            : DisabledBrush ?? context.Theme.Disabled;

    protected BrushHandle ResolveWindowChromeBackground(UiRenderContext context)
        => IsEffectivelyEnabled
            ? WindowChromeBackground ?? HoverBackground ?? context.Theme.SurfaceHover
            : DisabledBrush ?? context.Theme.Disabled;

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

        return UiGeometry.Clamp(new SizeF(nextWidth, nextHeight), minWidth, minHeight, maxWidth, maxHeight);
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
