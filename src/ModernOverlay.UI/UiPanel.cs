using System.Collections;

namespace ModernOverlay.UI;

/// <summary>
/// Represents the ordered child collection for a <see cref="UiPanel"/>.
/// </summary>
public class UiElementCollection : IEnumerable<UiElement>
{
    private readonly UiPanel owner;
    private readonly List<UiElement> items = [];
    private int nextInsertionOrder;

    internal UiElementCollection(UiPanel owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// Gets the number of child elements.
    /// </summary>
    public int Count => items.Count;

    /// <summary>
    /// Gets the child element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based child index.</param>
    /// <returns>The child element at <paramref name="index"/>.</returns>
    public UiElement this[int index] => items[index];

    /// <summary>
    /// Adds an element to the collection.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="element">The element to add.</param>
    /// <returns>The added element.</returns>
    public T Add<T>(T element)
        where T : UiElement
    {
        ArgumentNullException.ThrowIfNull(element);
        owner.MutateChildren(() => AddCore(element));
        return element;
    }

    /// <summary>
    /// Removes an element from the collection.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <returns><see langword="true"/> when the element was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        bool removed = false;
        owner.MutateChildren(() => removed = RemoveCore(element));
        return removed;
    }

    /// <summary>
    /// Removes all child elements.
    /// </summary>
    public void Clear() => owner.MutateChildren(ClearCore);

    /// <summary>
    /// Returns an enumerator over the child elements in insertion order.
    /// </summary>
    /// <returns>An enumerator over the child elements.</returns>
    public IEnumerator<UiElement> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal IEnumerable<UiElement> InRenderOrder()
        => items.OrderBy(item => item.ZIndex).ThenBy(item => item.InsertionOrder);

    internal IEnumerable<UiElement> InReverseInputOrder()
        => items.OrderByDescending(item => item.ZIndex).ThenByDescending(item => item.InsertionOrder);

    private void AddCore(UiElement element)
    {
        if (element.Parent is not null)
        {
            throw new InvalidOperationException("A UI element cannot be added to multiple parents.");
        }

        for (UiElement? current = owner; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, element))
            {
                throw new InvalidOperationException("A UI element cannot be added as a descendant of itself.");
            }
        }

        element.InsertionOrder = nextInsertionOrder++;
        items.Add(element);
        element.Attach(owner, owner.Root);
        owner.InvalidateMeasure();
    }

    private bool RemoveCore(UiElement element)
    {
        if (!items.Remove(element))
        {
            return false;
        }

        element.Detach();
        owner.InvalidateMeasure();
        return true;
    }

    private void ClearCore()
    {
        foreach (UiElement item in items.ToArray())
        {
            _ = RemoveCore(item);
        }
    }
}

/// <summary>
/// Base class for UI elements that contain child elements.
/// </summary>
public class UiPanel : UiControl
{
    /// <summary>
    /// Initializes a new panel.
    /// </summary>
    public UiPanel()
    {
        Children = new UiElementCollection(this);
    }

    /// <summary>
    /// Gets the panel child collection.
    /// </summary>
    public UiElementCollection Children { get; }

    /// <summary>
    /// Adds a child element to this panel.
    /// </summary>
    /// <typeparam name="T">The child element type.</typeparam>
    /// <param name="element">The child element to add.</param>
    /// <returns>The added child element.</returns>
    public T Add<T>(T element)
        where T : UiElement
        => Children.Add(element);

    internal IEnumerable<UiElement> ChildrenInReverseInputOrder() => Children.InReverseInputOrder();

    internal IEnumerable<UiElement> DescendantsAndSelf()
    {
        yield return this;
        foreach (UiElement child in Children.InRenderOrder())
        {
            if (child is UiPanel panel)
            {
                foreach (UiElement descendant in panel.DescendantsAndSelf())
                {
                    yield return descendant;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    internal void MutateChildren(Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (Root?.IsInProtectedPhase == true)
        {
            Root.Defer(mutation);
            return;
        }

        mutation();
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float width = 0f;
        float height = 0f;
        foreach (UiElement child in Children)
        {
            SizeF childSize = child.Measure(availableSize);
            width = MathF.Max(width, childSize.Width);
            height = MathF.Max(height, childSize.Height);
        }

        return new SizeF(width + Padding.Horizontal, height + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        foreach (UiElement child in Children)
        {
            child.Arrange(content);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        foreach (UiElement child in Children.InRenderOrder())
        {
            child.Render(context);
        }
    }
}
