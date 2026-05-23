using System.Collections;

namespace ModernOverlay.UI;

public class UiElementCollection : IEnumerable<UiElement>
{
    private readonly UiPanel owner;
    private readonly List<UiElement> items = [];
    private int nextInsertionOrder;

    internal UiElementCollection(UiPanel owner)
    {
        this.owner = owner;
    }

    public int Count => items.Count;

    public UiElement this[int index] => items[index];

    public T Add<T>(T element)
        where T : UiElement
    {
        ArgumentNullException.ThrowIfNull(element);
        owner.MutateChildren(() => AddCore(element));
        return element;
    }

    public bool Remove(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        bool removed = false;
        owner.MutateChildren(() => removed = RemoveCore(element));
        return removed;
    }

    public void Clear() => owner.MutateChildren(ClearCore);

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

public class UiPanel : UiElement
{
    public UiPanel()
    {
        Children = new UiElementCollection(this);
    }

    public UiElementCollection Children { get; }

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
