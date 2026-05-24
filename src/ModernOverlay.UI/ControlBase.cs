namespace ModernOverlay.UI;

/// <summary>
/// Base class for interactive controls. It does not add behavior beyond <see cref="UiElement"/>; derived controls opt in to focus and hit testing.
/// </summary>
public class UiControl : UiElement
{
}

/// <summary>
/// Represents the item collection used by selector controls. Mutations automatically coerce selection and invalidate layout, render, and input regions.
/// </summary>
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

/// <summary>
/// Base class for controls that select one item from a collection.
/// </summary>
public abstract class Selector : UiControl
{
    private int selectedIndex = -1;
    private Func<object?, string>? displayTextSelector;
    private Func<object?, bool>? isItemEnabledSelector;

    protected Selector()
    {
        Items = new UiItemCollection(this);
    }

    /// <summary>
    /// Occurs when the selected item changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the selectable items. Items may be any object; display text is resolved through <see cref="DisplayTextSelector"/> or <see cref="object.ToString()"/>.
    /// </summary>
    public UiItemCollection Items { get; }

    /// <summary>
    /// Gets or sets the selected item index, or -1 when no item is selected. Values outside the current item range are coerced.
    /// </summary>
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

    /// <summary>
    /// Gets the selected item, or <see langword="null"/> when no item is selected.
    /// </summary>
    public object? SelectedItem => IsSelectedIndexValid ? Items[SelectedIndex] : null;

    /// <summary>
    /// Gets or sets a function used to convert items to display text for built-in selector renderers.
    /// </summary>
    public Func<object?, string>? DisplayTextSelector
    {
        get => displayTextSelector;
        set => SetProperty(ref displayTextSelector, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets a function used to decide whether an item can be selected through pointer or keyboard input. Disabled items remain visible but cannot become selected through built-in interaction.
    /// </summary>
    public Func<object?, bool>? IsItemEnabledSelector
    {
        get => isItemEnabledSelector;
        set => SetProperty(ref isItemEnabledSelector, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    private protected virtual UiInvalidation SelectionInvalidation => UiInvalidation.Render;

    protected bool IsSelectedIndexValid => SelectedIndex >= 0 && SelectedIndex < Items.Count;

    protected string SelectedText => IsSelectedIndexValid ? GetItemText(SelectedIndex) : string.Empty;

    protected string GetItemText(int index)
        => index >= 0 && index < Items.Count ? GetItemText(Items[index]) : string.Empty;

    protected void MoveSelection(int delta)
    {
        int next = FindEnabledIndex(SelectedIndex, delta);
        if (next >= 0)
        {
            SelectedIndex = next;
        }
    }

    protected bool IsItemEnabled(int index)
        => index >= 0
            && index < Items.Count
            && (IsItemEnabledSelector?.Invoke(Items[index]) ?? true);

    protected int FirstEnabledIndex()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (IsItemEnabled(index))
            {
                return index;
            }
        }

        return -1;
    }

    protected int LastEnabledIndex()
    {
        for (int index = Items.Count - 1; index >= 0; index--)
        {
            if (IsItemEnabled(index))
            {
                return index;
            }
        }

        return -1;
    }

    protected int FindEnabledIndex(int startIndex, int direction)
    {
        if (Items.Count == 0 || direction == 0)
        {
            return -1;
        }

        int current = startIndex < 0 ? (direction > 0 ? -1 : Items.Count) : startIndex;
        for (int index = current + direction; index >= 0 && index < Items.Count; index += direction)
        {
            if (IsItemEnabled(index))
            {
                return index;
            }
        }

        return IsItemEnabled(startIndex) ? startIndex : -1;
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

/// <summary>
/// Base class for controls that host a single content element.
/// </summary>
public class ContentControl : UiPanel
{
    private UiElement? content;
    private UiHorizontalAlignment contentHorizontalAlignment = UiHorizontalAlignment.Stretch;
    private UiVerticalAlignment contentVerticalAlignment = UiVerticalAlignment.Stretch;

    /// <summary>
    /// Gets or sets the hosted content element. Assigning a new element removes the previous content from the child tree.
    /// </summary>
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

    /// <summary>
    /// Gets or sets horizontal alignment for the hosted content within the content slot.
    /// </summary>
    public UiHorizontalAlignment ContentHorizontalAlignment
    {
        get => contentHorizontalAlignment;
        set => SetProperty(ref contentHorizontalAlignment, value, UiInvalidation.Arrange);
    }

    /// <summary>
    /// Gets or sets vertical alignment for the hosted content within the content slot.
    /// </summary>
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

/// <summary>
/// Base class for content controls that expose a header string.
/// </summary>
public class HeaderedContentControl : ContentControl
{
    private string header = string.Empty;

    /// <summary>
    /// Gets or sets the header text used by derived controls when they render a title or caption.
    /// </summary>
    public string Header
    {
        get => header;
        set => SetProperty(ref header, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }
}
