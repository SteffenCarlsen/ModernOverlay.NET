namespace ModernOverlay.UI;

internal enum UiPopupDismissReason
{
    OutsidePointer,
    Escape,
    OwnerDetached,
    OwnerUnavailable,
    RootDisposed,
}

internal interface IUiPopup
{
    bool IsPopupOpen { get; }

    bool DismissOnOutsidePointer { get; }

    bool DismissOnEscape { get; }

    UiElement PopupElement { get; }

    UiElement? PopupOwner { get; }

    bool ContainsPopupPoint(PointF point);

    void DismissPopup(UiPopupDismissReason reason);
}

/// <summary>
/// Describes how popup placement coordinates are interpreted.
/// </summary>
public enum UiPopupPlacementMode
{
    /// <summary>
    /// Treat <see cref="Popup.Placement"/> or equivalent placement as an absolute overlay DIP coordinate.
    /// </summary>
    Absolute,

    /// <summary>
    /// Place the popup by aligning owner and popup anchors.
    /// </summary>
    OwnerAnchor,
}

internal static class UiPopupPlacement
{
    public static RectF Resolve(
        OverlayUiRoot? root,
        SizeF size,
        PointF absolutePlacement,
        UiElement? owner,
        UiPopupPlacementMode placementMode,
        OverlayAnchor ownerAnchor,
        OverlayAnchor popupAnchor,
        PointF offset,
        bool clampToOverlay)
    {
        PointF origin = absolutePlacement;
        if (placementMode == UiPopupPlacementMode.OwnerAnchor && owner is not null)
        {
            PointF ownerPoint = AnchorPoint(owner.Bounds, ownerAnchor);
            PointF popupPoint = AnchorOffset(size, popupAnchor);
            origin = new PointF(ownerPoint.X - popupPoint.X + offset.X, ownerPoint.Y - popupPoint.Y + offset.Y);
        }

        RectF bounds = new(origin.X, origin.Y, size.Width, size.Height);
        return clampToOverlay && root is not null ? Clamp(bounds, root.BoundsDips) : bounds;
    }

    public static RectF ResolveBelowOrAbove(OverlayUiRoot? root, RectF ownerBounds, SizeF size, float gap = 2f)
    {
        RectF below = new(ownerBounds.X, ownerBounds.Y + ownerBounds.Height + gap, ownerBounds.Width, size.Height);
        if (root is null)
        {
            return below;
        }

        RectF overlay = root.BoundsDips;
        if (below.Y + below.Height <= overlay.Height)
        {
            return Clamp(below, overlay);
        }

        RectF above = new(ownerBounds.X, ownerBounds.Y - size.Height - gap, ownerBounds.Width, size.Height);
        return above.Y >= 0f || ownerBounds.Y > overlay.Height - ownerBounds.Y - ownerBounds.Height
            ? Clamp(above, overlay)
            : Clamp(below, overlay);
    }

    private static RectF Clamp(RectF bounds, RectF overlay)
    {
        float maxX = MathF.Max(0f, overlay.Width - bounds.Width);
        float maxY = MathF.Max(0f, overlay.Height - bounds.Height);
        return bounds with
        {
            X = Math.Clamp(bounds.X, 0f, maxX),
            Y = Math.Clamp(bounds.Y, 0f, maxY),
        };
    }

    private static PointF AnchorPoint(RectF bounds, OverlayAnchor anchor)
        => anchor switch
        {
            OverlayAnchor.Top => new PointF(bounds.X + bounds.Width / 2f, bounds.Y),
            OverlayAnchor.TopRight => new PointF(bounds.X + bounds.Width, bounds.Y),
            OverlayAnchor.Left => new PointF(bounds.X, bounds.Y + bounds.Height / 2f),
            OverlayAnchor.Center => new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f),
            OverlayAnchor.Right => new PointF(bounds.X + bounds.Width, bounds.Y + bounds.Height / 2f),
            OverlayAnchor.BottomLeft => new PointF(bounds.X, bounds.Y + bounds.Height),
            OverlayAnchor.Bottom => new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height),
            OverlayAnchor.BottomRight => new PointF(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            _ => new PointF(bounds.X, bounds.Y),
        };

    private static PointF AnchorOffset(SizeF size, OverlayAnchor anchor)
        => anchor switch
        {
            OverlayAnchor.Top => new PointF(size.Width / 2f, 0f),
            OverlayAnchor.TopRight => new PointF(size.Width, 0f),
            OverlayAnchor.Left => new PointF(0f, size.Height / 2f),
            OverlayAnchor.Center => new PointF(size.Width / 2f, size.Height / 2f),
            OverlayAnchor.Right => new PointF(size.Width, size.Height / 2f),
            OverlayAnchor.BottomLeft => new PointF(0f, size.Height),
            OverlayAnchor.Bottom => new PointF(size.Width / 2f, size.Height),
            OverlayAnchor.BottomRight => new PointF(size.Width, size.Height),
            _ => new PointF(0f, 0f),
        };
}

/// <summary>
/// Provides a general popup container that can be placed absolutely or relative to an owner element.
/// </summary>
public class Popup : UiPanel, IUiPopup
{
    private bool isOpen;
    private PointF placement;
    private PointF placementOffset;
    private UiPopupPlacementMode placementMode;
    private OverlayAnchor ownerAnchor = OverlayAnchor.BottomLeft;
    private OverlayAnchor popupAnchor = OverlayAnchor.TopLeft;
    private bool clampToOverlay = true;
    private UiElement? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Popup"/> class.
    /// </summary>
    public Popup()
    {
        ZIndex = (int)UiLayer.Popup;
        ReceivesInput = true;
        Padding = new Thickness(6f);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the popup is visible and participates in layout.
    /// </summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (SetProperty(ref isOpen, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion))
            {
                Visibility = value ? UiVisibility.Visible : UiVisibility.Hidden;
            }
        }
    }

    /// <summary>
    /// Gets or sets the element that owns this popup.
    /// </summary>
    public UiElement? Owner
    {
        get => owner;
        set => SetProperty(ref owner, value, UiInvalidation.None);
    }

    /// <summary>
    /// Gets or sets a value indicating whether pointer input outside the popup dismisses it.
    /// </summary>
    public bool DismissOnOutsidePointer { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Escape dismisses the popup.
    /// </summary>
    public bool DismissOnEscape { get; set; } = true;

    /// <summary>
    /// Gets or sets the absolute placement point used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.Absolute"/>.
    /// </summary>
    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the additional placement offset applied after anchor resolution.
    /// </summary>
    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the popup placement mode.
    /// </summary>
    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the owner anchor used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the popup anchor aligned to <see cref="OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets a value indicating whether popup placement should be clamped to the overlay bounds.
    /// </summary>
    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

    /// <summary>
    /// Determines whether the specified point is inside the popup or its owner.
    /// </summary>
    /// <param name="point">The point to test in overlay DIPs.</param>
    /// <returns><see langword="true"/> when the point is inside the popup or owner bounds.</returns>
    public bool ContainsPopupPoint(PointF point)
        => UiGeometry.Contains(Bounds, point) || (Owner is not null && UiGeometry.Contains(Owner.Bounds, point));

    void IUiPopup.DismissPopup(UiPopupDismissReason reason) => IsOpen = false;

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        return IsOpen ? base.MeasureCore(availableSize) : new SizeF(0f, 0f);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        SizeF desired = DesiredSize;
        SetLayoutBounds(UiPopupPlacement.Resolve(Root, desired, Placement, Owner, PlacementMode, OwnerAnchor, PopupAnchor, PlacementOffset, ClampToOverlay));
        base.ArrangeCore(Bounds);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, ResolvePopupBackground(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveBorderBrush(context));
        base.RenderCore(context);
    }
}

/// <summary>
/// Represents a commandable item displayed by a menu or context menu.
/// </summary>
public sealed class UiMenuItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UiMenuItem"/> class.
    /// </summary>
    /// <param name="text">The item text.</param>
    /// <param name="command">The optional command executed by the item.</param>
    public UiMenuItem(string text, UiCommand? command = null)
    {
        Text = text ?? string.Empty;
        Command = command;
    }

    /// <summary>
    /// Gets or sets the item text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the optional command executed by the item.
    /// </summary>
    public UiCommand? Command { get; set; }

    /// <summary>
    /// Gets or sets the optional command parameter passed to <see cref="Command"/>.
    /// </summary>
    public object? CommandParameter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item can be invoked.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Displays a horizontal list of commandable menu items.
/// </summary>
public class Menu : UiControl
{
    private int hotIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Menu"/> class.
    /// </summary>
    public Menu()
    {
        ReceivesInput = true;
        Focusable = true;
        Height = 28f;
        Padding = new Thickness(8f, 4f);
    }

    /// <summary>
    /// Gets the items displayed by the menu.
    /// </summary>
    public IList<UiMenuItem> Items { get; } = [];

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        float width = Items.Sum(item => item.Text.Length * fontSize * 0.62f + 22f) + Padding.Horizontal;
        return new SizeF(MathF.Min(availableSize.Width, width), Height);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.Rectangle(Bounds, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        if (IsFocused && enabled)
        {
            context.Draw.Draw.Rectangle(Bounds, ResolveFocusBrush(context));
        }

        float x = ContentBounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            float width = item.Text.Length * context.Theme.Theme.FontSize * 0.62f + 22f;
            RectF itemRect = new(x, ContentBounds.Y, width, ContentBounds.Height);
            if (index == hotIndex)
            {
                context.Draw.Fill.RoundedRectangle(itemRect, 3f, 3f, HoverBackground ?? context.Theme.SurfaceHover);
            }

            context.Draw.Draw.Text(item.Text, context.Theme.Font, enabled && item.IsEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(itemRect.X + 8f, itemRect.Y + 3f));
            x += width;
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Left:
            case UiVirtualKeys.Up:
                MoveHotIndex(-1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
            case UiVirtualKeys.Down:
                MoveHotIndex(1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectFirstEnabled();
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectLastEnabled();
                args.Handled = true;
                break;
            case UiVirtualKeys.Enter:
            case UiVirtualKeys.Space:
                if (hotIndex >= 0)
                {
                    InvokeItem(hotIndex);
                    args.Handled = true;
                }

                break;
        }
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        hotIndex = ItemIndexAt(args.Position);
        InvalidateRender();
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        int index = ItemIndexAt(args.Position);
        if (index >= 0)
        {
            InvokeItem(index);
            args.Handled = true;
        }
    }

    private int ItemIndexAt(PointF point)
    {
        float x = ContentBounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            float width = Items[index].Text.Length * (Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize) * 0.62f + 22f;
            if (point.X >= x && point.X < x + width && point.Y >= ContentBounds.Y && point.Y < ContentBounds.Y + ContentBounds.Height)
            {
                return index;
            }

            x += width;
        }

        return -1;
    }

    protected void InvokeItem(int index)
    {
        UiMenuItem item = Items[index];
        if (item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true))
        {
            item.Command?.Execute(item.CommandParameter);
        }
    }

    private void MoveHotIndex(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int current = hotIndex < 0 ? 0 : hotIndex;
        for (int step = 1; step <= Items.Count; step++)
        {
            int next = (current + direction * step + Items.Count) % Items.Count;
            if (Items[next].IsEnabled)
            {
                hotIndex = next;
                InvalidateRender();
                return;
            }
        }
    }

    private void SelectFirstEnabled()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].IsEnabled)
            {
                hotIndex = index;
                InvalidateRender();
                return;
            }
        }
    }

    private void SelectLastEnabled()
    {
        for (int index = Items.Count - 1; index >= 0; index--)
        {
            if (Items[index].IsEnabled)
            {
                hotIndex = index;
                InvalidateRender();
                return;
            }
        }
    }
}

/// <summary>
/// Displays a vertical popup menu that can be placed absolutely or relative to an owner element.
/// </summary>
public sealed class ContextMenu : Menu, IUiPopup
{
    private const float ItemHeight = 26f;
    private bool isOpen;
    private PointF placement;
    private PointF placementOffset;
    private UiPopupPlacementMode placementMode;
    private OverlayAnchor ownerAnchor = OverlayAnchor.BottomLeft;
    private OverlayAnchor popupAnchor = OverlayAnchor.TopLeft;
    private bool clampToOverlay = true;
    private int hotIndex = -1;
    private UiElement? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMenu"/> class.
    /// </summary>
    public ContextMenu()
    {
        ZIndex = (int)UiLayer.Popup;
        Width = 180f;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the context menu is visible and participates in layout.
    /// </summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (SetProperty(ref isOpen, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion))
            {
                Visibility = value ? UiVisibility.Visible : UiVisibility.Hidden;
            }
        }
    }

    /// <summary>
    /// Gets or sets the absolute placement point used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.Absolute"/>.
    /// </summary>
    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the additional placement offset applied after anchor resolution.
    /// </summary>
    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the context menu placement mode.
    /// </summary>
    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the owner anchor used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the popup anchor aligned to <see cref="OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets a value indicating whether placement should be clamped to the overlay bounds.
    /// </summary>
    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the element that owns this context menu.
    /// </summary>
    public UiElement? Owner
    {
        get => owner;
        set => SetProperty(ref owner, value, UiInvalidation.None);
    }

    /// <summary>
    /// Gets or sets a value indicating whether pointer input outside the menu dismisses it.
    /// </summary>
    public bool DismissOnOutsidePointer { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Escape dismisses the menu.
    /// </summary>
    public bool DismissOnEscape { get; set; } = true;

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

    /// <summary>
    /// Determines whether the specified point is inside the menu or its owner.
    /// </summary>
    /// <param name="point">The point to test in overlay DIPs.</param>
    /// <returns><see langword="true"/> when the point is inside the menu or owner bounds.</returns>
    public bool ContainsPopupPoint(PointF point)
        => UiGeometry.Contains(Bounds, point) || (Owner is not null && UiGeometry.Contains(Owner.Bounds, point));

    void IUiPopup.DismissPopup(UiPopupDismissReason reason) => IsOpen = false;

    protected override SizeF MeasureCore(SizeF availableSize)
        => IsOpen ? new SizeF(Width, Items.Count * ItemHeight + Padding.Vertical) : new SizeF(0f, 0f);

    protected override void ArrangeCore(RectF finalRect)
    {
        SetLayoutBounds(UiPopupPlacement.Resolve(Root, DesiredSize, Placement, Owner, PlacementMode, OwnerAnchor, PopupAnchor, PlacementOffset, ClampToOverlay));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolvePopupBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveBorderBrush(context));
        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveFocusBrush(context), 2f);
        }

        RectF content = ContentBounds;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            RectF row = new(content.X, content.Y + index * ItemHeight, content.Width, ItemHeight);
            if (index == hotIndex)
            {
                context.Draw.Fill.Rectangle(row, HoverBackground ?? context.Theme.SurfaceHover);
            }

            context.Draw.Draw.Text(item.Text, context.Theme.Font, enabled && item.IsEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(row.X + 8f, row.Y + 5f));
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        if (!IsOpen)
        {
            return;
        }

        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Up:
                MoveContextHotIndex(-1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Down:
                MoveContextHotIndex(1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectFirstContextItem();
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectLastContextItem();
                args.Handled = true;
                break;
            case UiVirtualKeys.Enter:
            case UiVirtualKeys.Space:
                if (hotIndex >= 0)
                {
                    InvokeContextItem(hotIndex);
                    args.Handled = true;
                }

                break;
            case UiVirtualKeys.Escape:
                IsOpen = false;
                args.Handled = true;
                break;
        }
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        hotIndex = ContextItemIndexAt(args.Position);
        InvalidateRender();
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        int index = ContextItemIndexAt(args.Position);
        if (index >= 0)
        {
            InvokeContextItem(index);
            args.Handled = true;
        }
    }

    private int ContextItemIndexAt(PointF point)
    {
        if (!UiGeometry.Contains(Bounds, point))
        {
            return -1;
        }

        int index = (int)((point.Y - ContentBounds.Y) / ItemHeight);
        return index >= 0 && index < Items.Count ? index : -1;
    }

    private void MoveContextHotIndex(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int current = hotIndex < 0 ? 0 : hotIndex;
        for (int step = 1; step <= Items.Count; step++)
        {
            int next = (current + direction * step + Items.Count) % Items.Count;
            if (Items[next].IsEnabled)
            {
                hotIndex = next;
                InvalidateRender();
                return;
            }
        }
    }

    private void SelectFirstContextItem()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].IsEnabled)
            {
                hotIndex = index;
                InvalidateRender();
                return;
            }
        }
    }

    private void SelectLastContextItem()
    {
        for (int index = Items.Count - 1; index >= 0; index--)
        {
            if (Items[index].IsEnabled)
            {
                hotIndex = index;
                InvalidateRender();
                return;
            }
        }
    }

    private void InvokeContextItem(int index)
    {
        UiMenuItem item = Items[index];
        if (item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true))
        {
            item.Command?.Execute(item.CommandParameter);
            IsOpen = false;
        }
    }
}

/// <summary>
/// Displays transient text near an owner element, optionally opening after hover delay.
/// </summary>
public sealed class ToolTip : UiControl, IUiPopup
{
    private string text = string.Empty;
    private bool isOpen;
    private TimeSpan initialDelay = TimeSpan.FromMilliseconds(650);
    private TimeSpan showDuration = TimeSpan.Zero;
    private bool opensOnHover = true;
    private PointF placement;
    private PointF placementOffset;
    private UiPopupPlacementMode placementMode = UiPopupPlacementMode.OwnerAnchor;
    private OverlayAnchor ownerAnchor = OverlayAnchor.TopRight;
    private OverlayAnchor popupAnchor = OverlayAnchor.BottomLeft;
    private bool clampToOverlay = true;
    private UiElement? owner;
    private UiElement? subscribedOwner;
    private bool hoverPending;
    private bool ownerHovering;
    private long hoverStartedTimestamp;
    private long openedTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolTip"/> class.
    /// </summary>
    public ToolTip()
    {
        ZIndex = (int)UiLayer.Popup;
        Padding = new Thickness(8f, 5f);
    }

    /// <summary>
    /// Gets or sets the tooltip text.
    /// </summary>
    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the tooltip is visible and participates in layout.
    /// </summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (SetProperty(ref isOpen, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion) && value)
            {
                openedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
    }

    /// <summary>
    /// Gets or sets the element that owns this tooltip.
    /// </summary>
    public UiElement? Owner
    {
        get => owner;
        set
        {
            if (!SetProperty(ref owner, value, UiInvalidation.Arrange | UiInvalidation.InputRegion))
            {
                return;
            }

            SubscribeOwner(owner);
            CancelHover(closeTooltip: true);
        }
    }

    /// <summary>
    /// Gets or sets the hover delay before the tooltip opens.
    /// </summary>
    public TimeSpan InitialDelay
    {
        get => initialDelay;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip initial delay cannot be negative.");
            }

            SetProperty(ref initialDelay, value, UiInvalidation.None);
        }
    }

    /// <summary>
    /// Gets or sets how long the tooltip remains open after showing. Zero means no automatic timeout.
    /// </summary>
    public TimeSpan ShowDuration
    {
        get => showDuration;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip show duration cannot be negative.");
            }

            SetProperty(ref showDuration, value, UiInvalidation.None);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether owner hover can open the tooltip.
    /// </summary>
    public bool OpensOnHover
    {
        get => opensOnHover;
        set
        {
            if (SetProperty(ref opensOnHover, value, UiInvalidation.None) && !value)
            {
                CancelHover(closeTooltip: true);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether pointer input outside the tooltip dismisses it.
    /// </summary>
    public bool DismissOnOutsidePointer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Escape dismisses the tooltip.
    /// </summary>
    public bool DismissOnEscape { get; set; } = true;

    /// <summary>
    /// Gets or sets the absolute placement point used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.Absolute"/>.
    /// </summary>
    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the additional placement offset applied after anchor resolution.
    /// </summary>
    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the tooltip placement mode.
    /// </summary>
    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the owner anchor used when <see cref="PlacementMode"/> is <see cref="UiPopupPlacementMode.OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the popup anchor aligned to <see cref="OwnerAnchor"/>.
    /// </summary>
    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets a value indicating whether placement should be clamped to the overlay bounds.
    /// </summary>
    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

    /// <summary>
    /// Determines whether the specified point is inside the tooltip or its owner.
    /// </summary>
    /// <param name="point">The point to test in overlay DIPs.</param>
    /// <returns><see langword="true"/> when the point is inside the tooltip or owner bounds.</returns>
    public bool ContainsPopupPoint(PointF point)
        => UiGeometry.Contains(Bounds, point) || (Owner is not null && UiGeometry.Contains(Owner.Bounds, point));

    void IUiPopup.DismissPopup(UiPopupDismissReason reason) => IsOpen = false;

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        if (!IsOpen || Text.Length == 0)
        {
            return new SizeF(0f, 0f);
        }

        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        return new SizeF(MathF.Min(availableSize.Width, Text.Length * fontSize * 0.56f + Padding.Horizontal), fontSize * 1.35f + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        SetLayoutBounds(UiPopupPlacement.Resolve(Root, DesiredSize, Placement, Owner, PlacementMode, OwnerAnchor, PopupAnchor, PlacementOffset, ClampToOverlay));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!IsOpen || Text.Length == 0)
        {
            return;
        }

        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? HoverBackground ?? ResolvePopupBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveBorderBrush(context));
        context.Draw.Draw.Text(Text, context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(ContentBounds.X, ContentBounds.Y));
    }

    protected override void OnAttached()
    {
        SubscribeOwner(Owner);
    }

    protected override void OnDetached()
    {
        UnsubscribeOwner(Owner);
        CancelHover(closeTooltip: true);
    }

    internal void UpdateFrame(long timestamp)
    {
        if (!OpensOnHover || Owner is null || Owner.Root != Root || Text.Length == 0 || !Owner.IsVisible || !Owner.IsEffectivelyEnabled)
        {
            CancelHover(closeTooltip: true);
            return;
        }

        if (IsOpen)
        {
            if (ShowDuration > TimeSpan.Zero && Elapsed(timestamp, openedTimestamp) >= ShowDuration)
            {
                CancelHover(closeTooltip: true);
            }

            return;
        }

        if (hoverPending && ownerHovering && Elapsed(timestamp, hoverStartedTimestamp) >= InitialDelay)
        {
            hoverPending = false;
            IsOpen = true;
        }
    }

    private void HandleOwnerPointerEntered(object? sender, UiPointerEventArgs args)
    {
        if (!OpensOnHover || Text.Length == 0)
        {
            return;
        }

        ownerHovering = true;
        hoverPending = true;
        hoverStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        IsOpen = false;
    }

    private void HandleOwnerPointerExited(object? sender, UiPointerEventArgs args)
    {
        CancelHover(closeTooltip: true);
    }

    private void HandleOwnerPointerPressed(object? sender, UiPointerEventArgs args)
    {
        CancelHover(closeTooltip: true);
    }

    private void SubscribeOwner(UiElement? element)
    {
        if (ReferenceEquals(subscribedOwner, element))
        {
            return;
        }

        UnsubscribeOwner(subscribedOwner);
        if (element is null)
        {
            return;
        }

        element.PointerEntered += HandleOwnerPointerEntered;
        element.PointerExited += HandleOwnerPointerExited;
        element.PointerPressed += HandleOwnerPointerPressed;
        subscribedOwner = element;
    }

    private void UnsubscribeOwner(UiElement? element)
    {
        if (element is null || !ReferenceEquals(subscribedOwner, element))
        {
            return;
        }

        element.PointerEntered -= HandleOwnerPointerEntered;
        element.PointerExited -= HandleOwnerPointerExited;
        element.PointerPressed -= HandleOwnerPointerPressed;
        subscribedOwner = null;
    }

    private void CancelHover(bool closeTooltip)
    {
        ownerHovering = false;
        hoverPending = false;
        if (closeTooltip)
        {
            IsOpen = false;
        }
    }

    private static TimeSpan Elapsed(long currentTimestamp, long startTimestamp)
        => TimeSpan.FromSeconds((currentTimestamp - startTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency);
}
