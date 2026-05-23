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

public enum UiPopupPlacementMode
{
    Absolute,
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

    public Popup()
    {
        ZIndex = (int)UiLayer.Popup;
        ReceivesInput = true;
        Padding = new Thickness(6f);
    }

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

    public UiElement? Owner
    {
        get => owner;
        set => SetProperty(ref owner, value, UiInvalidation.None);
    }

    public bool DismissOnOutsidePointer { get; set; } = true;

    public bool DismissOnEscape { get; set; } = true;

    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

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

        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Surface);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Border);
        base.RenderCore(context);
    }
}

public sealed class UiMenuItem
{
    public UiMenuItem(string text, UiCommand? command = null)
    {
        Text = text ?? string.Empty;
        Command = command;
    }

    public string Text { get; set; }

    public UiCommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public class Menu : UiElement
{
    private int hotIndex = -1;

    public Menu()
    {
        ReceivesInput = true;
        Focusable = true;
        Height = 28f;
        Padding = new Thickness(8f, 4f);
    }

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
        context.Draw.Fill.Rectangle(Bounds, enabled ? context.Theme.Surface : context.Theme.Disabled);
        if (IsFocused && enabled)
        {
            context.Draw.Draw.Rectangle(Bounds, context.Theme.Accent);
        }

        float x = ContentBounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            float width = item.Text.Length * context.Theme.Theme.FontSize * 0.62f + 22f;
            RectF itemRect = new(x, ContentBounds.Y, width, ContentBounds.Height);
            if (index == hotIndex)
            {
                context.Draw.Fill.RoundedRectangle(itemRect, 3f, 3f, context.Theme.SurfaceHover);
            }

            context.Draw.Draw.Text(item.Text, context.Theme.Font, enabled && item.IsEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(itemRect.X + 8f, itemRect.Y + 3f));
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

    public ContextMenu()
    {
        ZIndex = (int)UiLayer.Popup;
        Width = 180f;
    }

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

    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public UiElement? Owner
    {
        get => owner;
        set => SetProperty(ref owner, value, UiInvalidation.None);
    }

    public bool DismissOnOutsidePointer { get; set; } = true;

    public bool DismissOnEscape { get; set; } = true;

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

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
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? context.Theme.Surface : context.Theme.Disabled);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Border);
        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Accent, 2f);
        }

        RectF content = ContentBounds;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            RectF row = new(content.X, content.Y + index * ItemHeight, content.Width, ItemHeight);
            if (index == hotIndex)
            {
                context.Draw.Fill.Rectangle(row, context.Theme.SurfaceHover);
            }

            context.Draw.Draw.Text(item.Text, context.Theme.Font, enabled && item.IsEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(row.X + 8f, row.Y + 5f));
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

public sealed class ToolTip : UiElement, IUiPopup
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

    public ToolTip()
    {
        ZIndex = (int)UiLayer.Popup;
        Padding = new Thickness(8f, 5f);
    }

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

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

    public bool DismissOnOutsidePointer { get; set; }

    public bool DismissOnEscape { get; set; } = true;

    public PointF Placement
    {
        get => placement;
        set => SetProperty(ref placement, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public PointF PlacementOffset
    {
        get => placementOffset;
        set => SetProperty(ref placementOffset, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public UiPopupPlacementMode PlacementMode
    {
        get => placementMode;
        set => SetProperty(ref placementMode, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor OwnerAnchor
    {
        get => ownerAnchor;
        set => SetProperty(ref ownerAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public OverlayAnchor PopupAnchor
    {
        get => popupAnchor;
        set => SetProperty(ref popupAnchor, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    public bool ClampToOverlay
    {
        get => clampToOverlay;
        set => SetProperty(ref clampToOverlay, value, UiInvalidation.Arrange | UiInvalidation.InputRegion);
    }

    bool IUiPopup.IsPopupOpen => IsOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => Owner;

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
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? context.Theme.SurfaceHover : context.Theme.Disabled);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Border);
        context.Draw.Draw.Text(Text, context.Theme.Font, enabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(ContentBounds.X, ContentBounds.Y));
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
