namespace ModernOverlay.UI;

public class UiControl : UiElement
{
}

public class UiItemCollection : System.Collections.ObjectModel.Collection<object?>
{
    private readonly Selector owner;

    internal UiItemCollection(Selector owner)
    {
        this.owner = owner;
    }

    protected override void InsertItem(int index, object? item)
    {
        base.InsertItem(index, item);
        owner.NotifyItemsChanged();
    }

    protected override void SetItem(int index, object? item)
    {
        base.SetItem(index, item);
        owner.NotifyItemsChanged();
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        owner.NotifyItemsChanged();
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        owner.NotifyItemsChanged();
    }
}

public abstract class Selector : UiControl
{
    private int selectedIndex = -1;
    private Func<object?, string>? displayTextSelector;

    protected Selector()
    {
        Items = new UiItemCollection(this);
    }

    public event EventHandler? SelectionChanged;

    public UiItemCollection Items { get; }

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int next = CoerceSelectedIndex(value);
            if (SetProperty(ref selectedIndex, next, SelectionInvalidation))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? SelectedItem => IsSelectedIndexValid ? Items[SelectedIndex] : null;

    public Func<object?, string>? DisplayTextSelector
    {
        get => displayTextSelector;
        set => SetProperty(ref displayTextSelector, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    private protected virtual UiInvalidation SelectionInvalidation => UiInvalidation.Render;

    protected bool IsSelectedIndexValid => SelectedIndex >= 0 && SelectedIndex < Items.Count;

    protected string SelectedText => IsSelectedIndexValid ? GetItemText(SelectedIndex) : string.Empty;

    protected string GetItemText(int index)
        => index >= 0 && index < Items.Count ? GetItemText(Items[index]) : string.Empty;

    protected void MoveSelection(int delta)
    {
        if (Items.Count == 0)
        {
            SelectedIndex = -1;
            return;
        }

        int current = SelectedIndex < 0 ? 0 : SelectedIndex;
        SelectedIndex = Math.Clamp(current + delta, 0, Items.Count - 1);
    }

    internal void NotifyItemsChanged()
    {
        int next = CoerceSelectedIndex(SelectedIndex);
        if (selectedIndex != next)
        {
            selectedIndex = next;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        Root?.Invalidate(UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    private string GetItemText(object? item)
        => DisplayTextSelector?.Invoke(item) ?? item?.ToString() ?? string.Empty;

    private int CoerceSelectedIndex(int value)
        => Items.Count == 0 ? -1 : Math.Clamp(value, -1, Items.Count - 1);
}

public class ContentControl : UiPanel
{
    private UiElement? content;
    private UiHorizontalAlignment contentHorizontalAlignment = UiHorizontalAlignment.Stretch;
    private UiVerticalAlignment contentVerticalAlignment = UiVerticalAlignment.Stretch;

    public UiElement? Content
    {
        get => content;
        set
        {
            if (ReferenceEquals(content, value))
            {
                return;
            }

            if (content is not null)
            {
                _ = Children.Remove(content);
            }

            content = value;
            if (content is not null)
            {
                _ = Children.Add(content);
            }

            InvalidateMeasure();
        }
    }

    public UiHorizontalAlignment ContentHorizontalAlignment
    {
        get => contentHorizontalAlignment;
        set => SetProperty(ref contentHorizontalAlignment, value, UiInvalidation.Arrange);
    }

    public UiVerticalAlignment ContentVerticalAlignment
    {
        get => contentVerticalAlignment;
        set => SetProperty(ref contentVerticalAlignment, value, UiInvalidation.Arrange);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentSize = Content?.Measure(UiGeometry.Deflate(availableSize, Padding)) ?? new SizeF(0f, 0f);
        return UiGeometry.Inflate(contentSize, Padding);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        ArrangeContent();
    }

    protected void ArrangeContent()
    {
        if (Content is null)
        {
            return;
        }

        RectF slot = ContentBounds;
        float width = ContentHorizontalAlignment == UiHorizontalAlignment.Stretch
            ? slot.Width
            : MathF.Min(Content.DesiredSize.Width, slot.Width);
        float height = ContentVerticalAlignment == UiVerticalAlignment.Stretch
            ? slot.Height
            : MathF.Min(Content.DesiredSize.Height, slot.Height);
        float x = ContentHorizontalAlignment switch
        {
            UiHorizontalAlignment.Center => slot.X + (slot.Width - width) / 2f,
            UiHorizontalAlignment.Right => slot.X + slot.Width - width,
            _ => slot.X,
        };
        float y = ContentVerticalAlignment switch
        {
            UiVerticalAlignment.Center => slot.Y + (slot.Height - height) / 2f,
            UiVerticalAlignment.Bottom => slot.Y + slot.Height - height,
            _ => slot.Y,
        };
        Content.Arrange(new RectF(x, y, width, height));
    }
}

public class HeaderedContentControl : ContentControl
{
    private string header = string.Empty;

    public string Header
    {
        get => header;
        set => SetProperty(ref header, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }
}
