namespace ModernOverlay.UI;

public class UiControl : UiElement
{
}

public class ContentControl : UiPanel
{
    private UiElement? content;

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

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentSize = Content?.Measure(UiGeometry.Deflate(availableSize, Padding)) ?? new SizeF(0f, 0f);
        return UiGeometry.Inflate(contentSize, Padding);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        Content?.Arrange(ContentBounds);
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
