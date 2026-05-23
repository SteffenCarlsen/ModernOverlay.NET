namespace ModernOverlay.UI;

internal enum UiPopupDismissReason
{
    OutsidePointer,
    Escape,
    OwnerDetached,
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

public class Popup : UiPanel, IUiPopup
{
    private bool isOpen;
    private PointF placement;
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
        SetLayoutBounds(new RectF(Placement.X, Placement.Y, desired.Width, desired.Height));
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
        context.Draw.Fill.Rectangle(Bounds, context.Theme.Surface);
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

            context.Draw.Draw.Text(item.Text, context.Theme.Font, item.IsEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(itemRect.X + 8f, itemRect.Y + 3f));
            x += width;
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

    private void InvokeItem(int index)
    {
        UiMenuItem item = Items[index];
        if (item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true))
        {
            item.Command?.Execute(item.CommandParameter);
        }
    }
}

public sealed class ContextMenu : Menu, IUiPopup
{
    private const float ItemHeight = 26f;
    private bool isOpen;
    private PointF placement;
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
        SetLayoutBounds(new RectF(Placement.X, Placement.Y, DesiredSize.Width, DesiredSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Surface);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Border);
        RectF content = ContentBounds;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            RectF row = new(content.X, content.Y + index * ItemHeight, content.Width, ItemHeight);
            if (index == hotIndex)
            {
                context.Draw.Fill.Rectangle(row, context.Theme.SurfaceHover);
            }

            context.Draw.Draw.Text(item.Text, context.Theme.Font, item.IsEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(row.X + 8f, row.Y + 5f));
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
            UiMenuItem item = Items[index];
            if (item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true))
            {
                item.Command?.Execute(item.CommandParameter);
                IsOpen = false;
            }

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
}

public sealed class ToolTip : UiElement, IUiPopup
{
    private string text = string.Empty;
    private bool isOpen;
    private PointF placement;
    private UiElement? owner;

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
        set => SetProperty(ref isOpen, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
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
        SetLayoutBounds(new RectF(Placement.X, Placement.Y, DesiredSize.Width, DesiredSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!IsOpen || Text.Length == 0)
        {
            return;
        }

        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, context.Theme.SurfaceHover);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Border);
        context.Draw.Draw.Text(Text, context.Theme.Font, context.Theme.Foreground, new PointF(ContentBounds.X, ContentBounds.Y));
    }
}
