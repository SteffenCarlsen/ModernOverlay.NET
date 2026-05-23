namespace ModernOverlay.UI;

public sealed class NumberBox : UiPanel
{
    private readonly TextBox textBox;
    private readonly Button decrementButton;
    private readonly Button incrementButton;
    private double minimum;
    private double maximum = 100d;
    private double value;
    private double step = 1d;
    private bool updatingText;

    public NumberBox()
    {
        Focusable = false;
        ReceivesInput = false;
        Height = 30f;
        MinWidth = 150f;

        textBox = Children.Add(new TextBox { Text = "0", MinWidth = 80f });
        decrementButton = Children.Add(new Button { Text = "-", Width = 28f });
        incrementButton = Children.Add(new Button { Text = "+", Width = 28f });
        decrementButton.Click += (_, _) => Value -= Step;
        incrementButton.Click += (_, _) => Value += Step;
        textBox.TextChanged += (_, _) => CommitText();
    }

    public event EventHandler? ValueChanged;

    public double Minimum
    {
        get => minimum;
        set
        {
            minimum = value;
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            Value = this.value;
        }
    }

    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(value, Minimum);
            Value = this.value;
        }
    }

    public double Step
    {
        get => step;
        set
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Step must be finite and greater than zero.");
            }

            step = value;
        }
    }

    public double Value
    {
        get => value;
        set
        {
            double next = Math.Clamp(value, Minimum, Maximum);
            if (this.value.Equals(next))
            {
                return;
            }

            this.value = next;
            UpdateText();
            ValueChanged?.Invoke(this, EventArgs.Empty);
            InvalidateRender();
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF buttonAvailable = new(28f, Height);
        decrementButton.Measure(buttonAvailable);
        incrementButton.Measure(buttonAvailable);
        textBox.Measure(new SizeF(MathF.Max(0f, availableSize.Width - 60f), Height));
        return new SizeF(MathF.Min(availableSize.Width, MathF.Max(MinWidth, textBox.DesiredSize.Width + 60f)), Height);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        float buttonWidth = 28f;
        decrementButton.Arrange(new RectF(content.X, content.Y, buttonWidth, content.Height));
        textBox.Arrange(new RectF(content.X + buttonWidth + 2f, content.Y, MathF.Max(0f, content.Width - buttonWidth * 2f - 4f), content.Height));
        incrementButton.Arrange(new RectF(content.X + content.Width - buttonWidth, content.Y, buttonWidth, content.Height));
    }

    private void CommitText()
    {
        if (updatingText)
        {
            return;
        }

        if (double.TryParse(textBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            Value = parsed;
        }
    }

    private void UpdateText()
    {
        updatingText = true;
        textBox.Text = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        textBox.CaretIndex = textBox.Text.Length;
        updatingText = false;
    }
}

public sealed class GroupBox : UiPanel
{
    private string header = string.Empty;
    private UiElement? content;

    public GroupBox()
    {
        Padding = new Thickness(10f, 24f, 10f, 10f);
        MinWidth = 120f;
        MinHeight = 60f;
    }

    public string Header
    {
        get => header;
        set => SetProperty(ref header, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public UiElement? Content
    {
        get => content;
        set
        {
            if (content == value)
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
        return new SizeF(MathF.Max(MinWidth, contentSize.Width + Padding.Horizontal), MathF.Max(MinHeight, contentSize.Height + Padding.Vertical));
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        Content?.Arrange(ContentBounds);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.Draw.Draw.RoundedRectangle(Bounds, 5f, 5f, context.Theme.Border);
        if (Header.Length > 0)
        {
            context.Draw.Draw.Text(Header, context.Theme.Font, context.Theme.Foreground, new PointF(Bounds.X + 10f, Bounds.Y + 4f));
        }

        base.RenderCore(context);
    }
}

public sealed class TabItem
{
    public TabItem(string header, UiElement content)
    {
        Header = header ?? string.Empty;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string Header { get; set; }

    public UiElement Content { get; }

    public bool IsEnabled { get; set; } = true;
}

public sealed class TabControl : UiPanel
{
    private const float HeaderHeight = 30f;
    private int selectedIndex = -1;

    public TabControl()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 180f;
        MinHeight = 100f;
    }

    public event EventHandler? SelectionChanged;

    public IList<TabItem> Items { get; } = [];

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int next = Items.Count == 0 ? -1 : Math.Clamp(value, 0, Items.Count - 1);
            if (selectedIndex == next)
            {
                return;
            }

            selectedIndex = next;
            SyncContent();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            InvalidateMeasure();
        }
    }

    public void Add(string header, UiElement content)
    {
        Items.Add(new TabItem(header, content));
        if (SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }
        else
        {
            SyncContent();
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SyncContent();
        SizeF contentAvailable = new(availableSize.Width, MathF.Max(0f, availableSize.Height - HeaderHeight));
        SizeF contentSize = ActiveContent?.Measure(contentAvailable) ?? new SizeF(0f, 0f);
        return new SizeF(MathF.Max(MinWidth, contentSize.Width), MathF.Max(MinHeight, contentSize.Height + HeaderHeight));
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        ActiveContent?.Arrange(new RectF(Bounds.X, Bounds.Y + HeaderHeight, Bounds.Width, MathF.Max(0f, Bounds.Height - HeaderHeight)));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (IsFocused)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Accent);
        }

        float x = Bounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            TabItem item = Items[index];
            float width = item.Header.Length * context.Theme.Theme.FontSize * 0.62f + 24f;
            RectF tab = new(x, Bounds.Y, width, HeaderHeight);
            context.Draw.Fill.RoundedRectangle(tab, 4f, 4f, index == SelectedIndex ? context.Theme.SurfaceHover : context.Theme.Surface);
            context.Draw.Draw.RoundedRectangle(tab, 4f, 4f, index == SelectedIndex ? context.Theme.Accent : context.Theme.Border);
            context.Draw.Draw.Text(item.Header, context.Theme.Font, item.IsEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(tab.X + 10f, tab.Y + 7f));
            x += width + 2f;
        }

        ActiveContent?.Render(context);
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Left:
            case UiVirtualKeys.Up:
                MoveSelection(-1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
            case UiVirtualKeys.Down:
                MoveSelection(1);
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
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        int index = HeaderIndexAt(args.Position);
        if (index >= 0 && Items[index].IsEnabled)
        {
            SelectedIndex = index;
            args.Handled = true;
        }
    }

    private UiElement? ActiveContent => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex].Content : null;

    private void SyncContent()
    {
        UiElement? active = ActiveContent;
        foreach (UiElement child in Children.ToArray())
        {
            if (!ReferenceEquals(child, active))
            {
                _ = Children.Remove(child);
            }
        }

        if (active is not null && active.Parent is null)
        {
            _ = Children.Add(active);
        }
    }

    private int HeaderIndexAt(PointF point)
    {
        float x = Bounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            float width = Items[index].Header.Length * (Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize) * 0.62f + 24f;
            if (point.X >= x && point.X < x + width && point.Y >= Bounds.Y && point.Y < Bounds.Y + HeaderHeight)
            {
                return index;
            }

            x += width + 2f;
        }

        return -1;
    }

    private void MoveSelection(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int current = SelectedIndex < 0 ? 0 : SelectedIndex;
        for (int step = 1; step <= Items.Count; step++)
        {
            int next = (current + direction * step + Items.Count) % Items.Count;
            if (Items[next].IsEnabled)
            {
                SelectedIndex = next;
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
                SelectedIndex = index;
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
                SelectedIndex = index;
                return;
            }
        }
    }
}

public sealed class SegmentedControl : UiElement
{
    private int selectedIndex = -1;

    public SegmentedControl()
    {
        ReceivesInput = true;
        Focusable = true;
        Height = 30f;
        MinWidth = 120f;
    }

    public event EventHandler? SelectionChanged;

    public IList<string> Items { get; } = [];

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int next = Items.Count == 0 ? -1 : Math.Clamp(value, 0, Items.Count - 1);
            if (SetProperty(ref selectedIndex, next, UiInvalidation.Render))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize) => new(MathF.Min(availableSize.Width, MathF.Max(MinWidth, Items.Count * 80f)), Height);

    protected override void RenderCore(UiRenderContext context)
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (IsFocused)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Accent);
        }

        float segmentWidth = Bounds.Width / Items.Count;
        for (int index = 0; index < Items.Count; index++)
        {
            RectF segment = new(Bounds.X + index * segmentWidth, Bounds.Y, segmentWidth, Bounds.Height);
            context.Draw.Fill.Rectangle(segment, index == SelectedIndex ? context.Theme.Accent : context.Theme.Surface);
            context.Draw.Draw.Rectangle(segment, context.Theme.Border);
            context.Draw.Draw.Text(Items[index], context.Theme.Font, context.Theme.Foreground, new PointF(segment.X + 8f, segment.Y + 7f));
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        if (Items.Count == 0)
        {
            return;
        }

        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Left:
            case UiVirtualKeys.Up:
                SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
            case UiVirtualKeys.Down:
                SelectedIndex = SelectedIndex < 0 || SelectedIndex >= Items.Count - 1 ? 0 : SelectedIndex + 1;
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectedIndex = 0;
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectedIndex = Items.Count - 1;
                args.Handled = true;
                break;
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (Items.Count == 0 || args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        int index = (int)((args.Position.X - Bounds.X) / MathF.Max(1f, Bounds.Width / Items.Count));
        if (index >= 0 && index < Items.Count)
        {
            SelectedIndex = index;
            args.Handled = true;
        }
    }
}

public sealed class ColorPicker : UiPanel
{
    private readonly Slider red;
    private readonly Slider green;
    private readonly Slider blue;
    private readonly Slider alpha;
    private ColorRgba value = ColorRgba.White;
    private bool updatingSliders;
    private SolidBrushHandle? swatchBrush;

    public ColorPicker()
    {
        red = CreateChannelSlider();
        green = CreateChannelSlider();
        blue = CreateChannelSlider();
        alpha = CreateChannelSlider();
        Children.Add(red);
        Children.Add(green);
        Children.Add(blue);
        Children.Add(alpha);
        red.ValueChanged += (_, _) => CommitSliders();
        green.ValueChanged += (_, _) => CommitSliders();
        blue.ValueChanged += (_, _) => CommitSliders();
        alpha.ValueChanged += (_, _) => CommitSliders();
        SetSliders(ColorRgba.White);
    }

    public event EventHandler? ColorChanged;

    public ColorRgba Value
    {
        get => value;
        set
        {
            if (this.value.Equals(value))
            {
                return;
            }

            this.value = value;
            SetSliders(value);
            RecreateSwatchBrush();
            ColorChanged?.Invoke(this, EventArgs.Empty);
            InvalidateRender();
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF sliderAvailable = new(availableSize.Width, 24f);
        red.Measure(sliderAvailable);
        green.Measure(sliderAvailable);
        blue.Measure(sliderAvailable);
        alpha.Measure(sliderAvailable);
        return new SizeF(MathF.Min(availableSize.Width, MathF.Max(180f, red.DesiredSize.Width)), 118f);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        float y = content.Y + 34f;
        red.Arrange(new RectF(content.X, y, content.Width, 22f));
        green.Arrange(new RectF(content.X, y + 24f, content.Width, 22f));
        blue.Arrange(new RectF(content.X, y + 48f, content.Width, 22f));
        alpha.Arrange(new RectF(content.X, y + 72f, content.Width, 22f));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        RectF swatch = new(ContentBounds.X, ContentBounds.Y, ContentBounds.Width, 26f);
        context.Draw.Fill.RoundedRectangle(swatch, 4f, 4f, swatchBrush ?? context.Theme.Accent);
        context.Draw.Draw.RoundedRectangle(swatch, 4f, 4f, context.Theme.Border);
        base.RenderCore(context);
    }

    protected override void OnAttached()
    {
        RecreateSwatchBrush();
    }

    protected override void OnDetached()
    {
        swatchBrush?.Dispose();
        swatchBrush = null;
    }

    private static Slider CreateChannelSlider() => new()
    {
        Minimum = 0,
        Maximum = 255,
        Value = 255,
        Height = 22f,
    };

    private void CommitSliders()
    {
        if (updatingSliders)
        {
            return;
        }

        value = ColorRgba.FromBytes((byte)red.Value, (byte)green.Value, (byte)blue.Value, (byte)alpha.Value);
        RecreateSwatchBrush();
        ColorChanged?.Invoke(this, EventArgs.Empty);
        InvalidateRender();
    }

    private void SetSliders(ColorRgba color)
    {
        updatingSliders = true;
        red.Value = Math.Clamp(color.R * 255f, 0f, 255f);
        green.Value = Math.Clamp(color.G * 255f, 0f, 255f);
        blue.Value = Math.Clamp(color.B * 255f, 0f, 255f);
        alpha.Value = Math.Clamp(color.A * 255f, 0f, 255f);
        updatingSliders = false;
    }

    private void RecreateSwatchBrush()
    {
        if (Root is null)
        {
            return;
        }

        swatchBrush?.Dispose();
        swatchBrush = Root.ThemeResources.CreateSolidBrush(Value);
    }
}
