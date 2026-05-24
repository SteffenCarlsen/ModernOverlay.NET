namespace ModernOverlay.UI;

/// <summary>
/// Provides a numeric input composed from a text box and increment/decrement buttons.
/// </summary>
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
    private string lastValidText = "0";

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberBox"/> class.
    /// </summary>
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
        textBox.KeyPressed += (_, args) =>
        {
            if (args.VirtualKey == UiVirtualKeys.Enter)
            {
                CommitOrRevertText();
                args.Handled = true;
            }
        };
    }

    /// <summary>
    /// Occurs when <see cref="Value"/> changes.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Gets or sets the minimum allowed value.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the maximum allowed value.
    /// </summary>
    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(value, Minimum);
            Value = this.value;
        }
    }

    /// <summary>
    /// Gets or sets the increment used by the step buttons.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
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

        if (IsPartialNumberText(textBox.Text))
        {
            return;
        }

        if (double.TryParse(textBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            Value = parsed;
            lastValidText = textBox.Text;
            return;
        }

        RevertText();
    }

    private void UpdateText()
    {
        updatingText = true;
        textBox.Text = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        textBox.CaretIndex = textBox.Text.Length;
        lastValidText = textBox.Text;
        updatingText = false;
    }

    private void CommitOrRevertText()
    {
        if (IsPartialNumberText(textBox.Text))
        {
            RevertText();
            return;
        }

        CommitText();
    }

    private void RevertText()
    {
        updatingText = true;
        textBox.Text = lastValidText;
        textBox.CaretIndex = textBox.Text.Length;
        updatingText = false;
    }

    private static bool IsPartialNumberText(string text)
        => text.Length == 0 || text is "-" or "+" or "." or "-." or "+.";
}

/// <summary>
/// Displays a titled border around a single content element.
/// </summary>
public sealed class GroupBox : UiPanel
{
    private string header = string.Empty;
    private UiElement? content;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupBox"/> class.
    /// </summary>
    public GroupBox()
    {
        Padding = new Thickness(10f, 24f, 10f, 10f);
        MinWidth = 120f;
        MinHeight = 60f;
    }

    /// <summary>
    /// Gets or sets the group header text.
    /// </summary>
    public string Header
    {
        get => header;
        set => SetProperty(ref header, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets the group content element.
    /// </summary>
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
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Draw.RoundedRectangle(Bounds, 5f, 5f, enabled ? ResolveBorderBrush(context) : ResolveDisabledBrush(context));
        if (Header.Length > 0)
        {
            context.Draw.Draw.Text(Header, context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(Bounds.X + 10f, Bounds.Y + 4f));
        }

        base.RenderCore(context);
    }
}

/// <summary>
/// Represents a single tab and its content in a <see cref="TabControl"/>.
/// </summary>
public sealed class TabItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TabItem"/> class.
    /// </summary>
    /// <param name="header">The tab header text.</param>
    /// <param name="content">The tab content element.</param>
    public TabItem(string header, UiElement content)
    {
        Header = header ?? string.Empty;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// Gets or sets the tab header text.
    /// </summary>
    public string Header { get; set; }

    /// <summary>
    /// Gets the tab content element.
    /// </summary>
    public UiElement Content { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab can be selected.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Displays selectable tab headers with one active content element.
/// </summary>
public sealed class TabControl : UiPanel
{
    private const float HeaderHeight = 30f;
    private int selectedIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabControl"/> class.
    /// </summary>
    public TabControl()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 180f;
        MinHeight = 100f;
    }

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the tab items displayed by the control.
    /// </summary>
    public IList<TabItem> Items { get; } = [];

    /// <summary>
    /// Gets or sets the selected tab index, or -1 when no item is selected.
    /// </summary>
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

    /// <summary>
    /// Adds a tab with the specified header and content element.
    /// </summary>
    /// <param name="header">The tab header text.</param>
    /// <param name="content">The tab content element.</param>
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
        bool enabled = IsEffectivelyEnabled;
        if (IsFocused && enabled)
        {
            context.Draw.Draw.Line(new PointF(Bounds.X, Bounds.Y + HeaderHeight - 1f), new PointF(Bounds.X + Bounds.Width, Bounds.Y + HeaderHeight - 1f), context.Theme.Accent);
        }

        float x = Bounds.X;
        for (int index = 0; index < Items.Count; index++)
        {
            TabItem item = Items[index];
            float width = item.Header.Length * context.Theme.Theme.FontSize * 0.62f + 24f;
            RectF tab = new(x, Bounds.Y, width, HeaderHeight);
            bool itemEnabled = enabled && item.IsEnabled;
            if (index == SelectedIndex && itemEnabled)
            {
                context.Draw.Fill.Rectangle(new RectF(tab.X + 8f, tab.Y + tab.Height - 4f, MathF.Max(0f, tab.Width - 16f), 4f), context.Theme.Accent);
            }

            context.Draw.Draw.Text(item.Header, context.Theme.Font, itemEnabled ? context.Theme.Foreground : context.Theme.Disabled, new PointF(tab.X + 10f, tab.Y + 7f));
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
            RectF header = new(x, Bounds.Y, width, HeaderHeight);
            if (UiGeometry.ContainsInputBand(header, point))
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

/// <summary>
/// Displays a compact segmented selector backed by a list of text options.
/// </summary>
public sealed class SegmentedControl : UiControl
{
    private int selectedIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="SegmentedControl"/> class.
    /// </summary>
    public SegmentedControl()
    {
        ReceivesInput = true;
        Focusable = true;
        Height = 30f;
        MinWidth = 120f;
    }

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the segment labels displayed by the control.
    /// </summary>
    public IList<string> Items { get; } = [];

    /// <summary>
    /// Gets or sets the selected segment index, or -1 when no item is selected.
    /// </summary>
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

        bool enabled = IsEffectivelyEnabled;
        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, context.Theme.Accent);
        }

        float segmentWidth = Bounds.Width / Items.Count;
        for (int index = 0; index < Items.Count; index++)
        {
            RectF segment = new(Bounds.X + index * segmentWidth, Bounds.Y, segmentWidth, Bounds.Height);
            context.Draw.Fill.Rectangle(segment, !enabled ? ResolveDisabledBrush(context) : index == SelectedIndex ? ResolveAccentBrush(context) : ResolveBackground(context));
            context.Draw.Draw.Rectangle(segment, ResolveBorderBrush(context));
            context.Draw.Draw.Text(Items[index], context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(segment.X + 8f, segment.Y + 7f));
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

        float segmentWidth = MathF.Max(1f, Bounds.Width / Items.Count);
        int index = UiGeometry.UniformBandIndex(args.Position.X, Bounds.X, segmentWidth, Items.Count);
        if (index >= 0 && index < Items.Count)
        {
            SelectedIndex = index;
            args.Handled = true;
        }
    }
}

/// <summary>
/// Provides an HSV color picker with hue, alpha, preview, and hexadecimal readout.
/// </summary>
public sealed class ColorPicker : UiPanel
{
    private const int ColorFieldColumns = 14;
    private const int ColorFieldRows = 14;
    private const int HueSteps = 18;
    private const int AlphaSteps = 14;
    private const float StripWidth = 16f;
    private const float Gap = 8f;
    private const float AlphaHeight = 12f;
    private const float PreviewSize = 24f;
    private const float HeaderHeight = 28f;

    private ColorRgba value = ColorRgba.White;
    private string label = string.Empty;
    private bool isExpanded;
    private float hue;
    private float saturation;
    private float brightness = 1f;
    private float alpha = 1f;
    private DragPart dragPart;
    private SolidBrushHandle?[]? colorFieldBrushes;
    private SolidBrushHandle?[]? hueBrushes;
    private SolidBrushHandle?[]? alphaBrushes;
    private SolidBrushHandle? checkerLightBrush;
    private SolidBrushHandle? checkerDarkBrush;
    private SolidBrushHandle? previewBrush;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorPicker"/> class.
    /// </summary>
    public ColorPicker()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 170f;
        MinHeight = HeaderHeight;
        UpdateHsvFromColor(value);
    }

    /// <summary>
    /// Occurs when <see cref="Value"/> changes.
    /// </summary>
    public event EventHandler? ColorChanged;

    /// <summary>
    /// Gets or sets optional label text rendered next to the selected color preview.
    /// </summary>
    public string Label
    {
        get => label;
        set => SetProperty(ref label, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets whether the expanded HSV picker is visible.
    /// </summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    /// <summary>
    /// Gets or sets the selected color value.
    /// </summary>
    public ColorRgba Value
    {
        get => value;
        set => SetValue(value, updateHsv: true, raiseChanged: true);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float height = IsExpanded ? 174f : HeaderHeight;
        return new SizeF(MathF.Min(availableSize.Width, MathF.Max(MinWidth, 190f)), MathF.Max(MinHeight, height));
    }

    protected override void ArrangeCore(RectF finalRect)
    {
    }

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        RectF preview = ValuePreviewBounds;
        if (Label.Length > 0)
        {
            context.Draw.Draw.Text(Label, context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(ContentBounds.X, ContentBounds.Y + 5f));
        }

        DrawPreview(context, preview, enabled);
        context.Draw.Draw.Text(HexText(), context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(preview.X + PreviewSize + 8f, preview.Y + 4f));
        if (!IsExpanded)
        {
            if (IsFocused && enabled)
            {
                context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveFocusBrush(context));
            }

            return;
        }

        RectF field = ColorFieldBounds;
        RectF hueStrip = HueStripBounds;
        RectF alphaStrip = AlphaStripBounds;
        RectF expandedPreview = ExpandedPreviewBounds;

        DrawChecker(context, field);
        DrawColorField(context, field, enabled);
        DrawHueStrip(context, hueStrip, enabled);
        DrawAlphaStrip(context, alphaStrip, enabled);

        DrawSelectionCircle(context, new PointF(field.X + saturation * field.Width, field.Y + (1f - brightness) * field.Height), enabled);
        float hueY = hueStrip.Y + hue / 360f * hueStrip.Height;
        context.Draw.Draw.Rectangle(new RectF(hueStrip.X - 2f, hueY - 2f, hueStrip.Width + 4f, 4f), enabled ? ResolveForeground(context) : ResolveDisabledBrush(context));
        float alphaX = alphaStrip.X + alpha * alphaStrip.Width;
        context.Draw.Draw.Rectangle(new RectF(alphaX - 2f, alphaStrip.Y - 2f, 4f, alphaStrip.Height + 4f), enabled ? ResolveForeground(context) : ResolveDisabledBrush(context));

        DrawPreview(context, expandedPreview, enabled);
        context.Draw.Draw.Text(HexText(), context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(expandedPreview.X + PreviewSize + 8f, expandedPreview.Y + 4f));

        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveFocusBrush(context));
        }
    }

    protected override void OnAttached()
    {
        RecreateColorResources();
    }

    protected override void OnDetached()
    {
        DisposeColorResources();
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        if (UiGeometry.Contains(ValuePreviewBounds, args.Position))
        {
            Focus();
            IsExpanded = !IsExpanded;
            args.Handled = true;
            return;
        }

        if (!IsExpanded)
        {
            return;
        }

        DragPart part = PartAt(args.Position);
        if (part == DragPart.None)
        {
            return;
        }

        Focus();
        dragPart = part;
        CapturePointer();
        UpdateFromPointer(args.Position);
        args.Handled = true;
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        if (!IsPointerCaptured || dragPart == DragPart.None)
        {
            return;
        }

        UpdateFromPointer(args.Position);
        args.Handled = true;
    }

    protected override void OnPointerReleased(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left || dragPart == DragPart.None)
        {
            return;
        }

        dragPart = DragPart.None;
        ReleasePointerCapture();
        args.Handled = true;
    }

    private RectF ColorFieldBounds
    {
        get
        {
            RectF picker = PickerBounds;
            float size = MathF.Min(MathF.Max(1f, picker.Width - StripWidth - Gap), MathF.Max(1f, picker.Height - AlphaHeight - PreviewSize - Gap * 3f));
            return new RectF(picker.X, picker.Y, size, size);
        }
    }

    private RectF HueStripBounds
    {
        get
        {
            RectF colorField = ColorFieldBounds;
            return new RectF(colorField.X + colorField.Width + Gap, colorField.Y, StripWidth, colorField.Height);
        }
    }

    private RectF AlphaStripBounds
    {
        get
        {
            RectF colorField = ColorFieldBounds;
            return new RectF(colorField.X, colorField.Y + colorField.Height + Gap, colorField.Width, AlphaHeight);
        }
    }

    private RectF ValuePreviewBounds
    {
        get
        {
            RectF content = ContentBounds;
            float x = Label.Length > 0
                ? MathF.Max(content.X, content.X + content.Width - PreviewSize - 88f)
                : content.X;
            return new RectF(x, content.Y + MathF.Max(0f, HeaderHeight - PreviewSize) / 2f, PreviewSize, PreviewSize);
        }
    }

    private RectF ExpandedPreviewBounds
    {
        get
        {
            RectF alphaBounds = AlphaStripBounds;
            return new RectF(alphaBounds.X, alphaBounds.Y + alphaBounds.Height + Gap, PreviewSize, PreviewSize);
        }
    }

    private RectF PickerBounds
    {
        get
        {
            RectF content = ContentBounds;
            float y = content.Y + HeaderHeight + Gap;
            return new RectF(content.X, y, content.Width, MathF.Max(0f, content.Y + content.Height - y));
        }
    }

    private DragPart PartAt(PointF point)
        => UiGeometry.Contains(ColorFieldBounds, point)
            ? DragPart.ColorField
            : UiGeometry.Contains(HueStripBounds, point)
                ? DragPart.Hue
                : UiGeometry.Contains(AlphaStripBounds, point) ? DragPart.Alpha : DragPart.None;

    private void UpdateFromPointer(PointF point)
    {
        switch (dragPart)
        {
            case DragPart.ColorField:
                RectF field = ColorFieldBounds;
                saturation = Math.Clamp((point.X - field.X) / MathF.Max(1f, field.Width), 0f, 1f);
                brightness = 1f - Math.Clamp((point.Y - field.Y) / MathF.Max(1f, field.Height), 0f, 1f);
                break;
            case DragPart.Hue:
                RectF hueBounds = HueStripBounds;
                hue = Math.Clamp((point.Y - hueBounds.Y) / MathF.Max(1f, hueBounds.Height), 0f, 1f) * 360f;
                break;
            case DragPart.Alpha:
                RectF alphaBounds = AlphaStripBounds;
                alpha = Math.Clamp((point.X - alphaBounds.X) / MathF.Max(1f, alphaBounds.Width), 0f, 1f);
                break;
        }

        SetValue(FromHsv(hue, saturation, brightness, alpha), updateHsv: false, raiseChanged: true);
    }

    private void SetValue(ColorRgba next, bool updateHsv, bool raiseChanged)
    {
        if (value.Equals(next))
        {
            return;
        }

        value = next;
        if (updateHsv)
        {
            UpdateHsvFromColor(next);
        }

        RecreateColorResources();
        if (raiseChanged)
        {
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        InvalidateRender();
    }

    private void UpdateHsvFromColor(ColorRgba color)
    {
        ToHsv(color, out hue, out saturation, out brightness);
        alpha = Math.Clamp(color.A, 0f, 1f);
    }

    private void DrawColorField(UiRenderContext context, RectF field, bool enabled)
    {
        float cellWidth = field.Width / ColorFieldColumns;
        float cellHeight = field.Height / ColorFieldRows;
        for (int row = 0; row < ColorFieldRows; row++)
        {
            for (int column = 0; column < ColorFieldColumns; column++)
            {
                RectF cell = new(field.X + column * cellWidth, field.Y + row * cellHeight, cellWidth + 0.5f, cellHeight + 0.5f);
                BrushHandle brush = enabled
                    ? colorFieldBrushes?[row * ColorFieldColumns + column] ?? ResolveAccentBrush(context)
                    : ResolveDisabledBrush(context);
                context.Draw.Fill.Rectangle(cell, brush);
            }
        }

        context.Draw.Draw.Rectangle(field, ResolveBorderBrush(context));
    }

    private void DrawHueStrip(UiRenderContext context, RectF bounds, bool enabled)
    {
        float stepHeight = bounds.Height / HueSteps;
        for (int index = 0; index < HueSteps; index++)
        {
            RectF stepBounds = new(bounds.X, bounds.Y + index * stepHeight, bounds.Width, stepHeight + 0.5f);
            BrushHandle brush = enabled ? hueBrushes?[index] ?? ResolveAccentBrush(context) : ResolveDisabledBrush(context);
            context.Draw.Fill.Rectangle(stepBounds, brush);
        }

        context.Draw.Draw.Rectangle(bounds, ResolveBorderBrush(context));
    }

    private void DrawAlphaStrip(UiRenderContext context, RectF bounds, bool enabled)
    {
        DrawChecker(context, bounds);
        float stepWidth = bounds.Width / AlphaSteps;
        for (int index = 0; index < AlphaSteps; index++)
        {
            RectF stepBounds = new(bounds.X + index * stepWidth, bounds.Y, stepWidth + 0.5f, bounds.Height);
            BrushHandle brush = enabled ? alphaBrushes?[index] ?? ResolveAccentBrush(context) : ResolveDisabledBrush(context);
            context.Draw.Fill.Rectangle(stepBounds, brush);
        }

        context.Draw.Draw.Rectangle(bounds, ResolveBorderBrush(context));
    }

    private void DrawChecker(UiRenderContext context, RectF bounds)
    {
        const float size = 6f;
        for (float y = bounds.Y; y < bounds.Y + bounds.Height; y += size)
        {
            for (float x = bounds.X; x < bounds.X + bounds.Width; x += size)
            {
                bool alternate = (((int)((x - bounds.X) / size) + (int)((y - bounds.Y) / size)) & 1) == 0;
                RectF tile = new(x, y, MathF.Min(size, bounds.X + bounds.Width - x), MathF.Min(size, bounds.Y + bounds.Height - y));
                context.Draw.Fill.Rectangle(tile, alternate ? checkerLightBrush ?? context.Theme.SurfaceHover : checkerDarkBrush ?? context.Theme.Surface);
            }
        }
    }

    private void DrawSelectionCircle(UiRenderContext context, PointF center, bool enabled)
    {
        BrushHandle brush = enabled ? ResolveForeground(context) : ResolveDisabledBrush(context);
        context.Draw.Draw.Circle(center, 4f, brush);
        context.Draw.Draw.Circle(center, 5f, ResolveBorderBrush(context));
    }

    private void DrawPreview(UiRenderContext context, RectF bounds, bool enabled)
    {
        DrawChecker(context, bounds);
        context.Draw.Fill.RoundedRectangle(bounds, 3f, 3f, enabled ? previewBrush ?? ResolveAccentBrush(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(bounds, 3f, 3f, ResolveBorderBrush(context));
    }

    private void RecreateColorResources()
    {
        if (Root is null)
        {
            return;
        }

        DisposeColorResources();
        colorFieldBrushes = new SolidBrushHandle?[ColorFieldColumns * ColorFieldRows];
        for (int row = 0; row < ColorFieldRows; row++)
        {
            float nextBrightness = 1f - (row / (float)(ColorFieldRows - 1));
            for (int column = 0; column < ColorFieldColumns; column++)
            {
                float nextSaturation = column / (float)(ColorFieldColumns - 1);
                colorFieldBrushes[row * ColorFieldColumns + column] = Root.ThemeResources.CreateSolidBrush(FromHsv(hue, nextSaturation, nextBrightness, 1f));
            }
        }

        hueBrushes = new SolidBrushHandle?[HueSteps];
        for (int index = 0; index < HueSteps; index++)
        {
            hueBrushes[index] = Root.ThemeResources.CreateSolidBrush(FromHsv(index / (float)(HueSteps - 1) * 360f, 1f, 1f, 1f));
        }

        alphaBrushes = new SolidBrushHandle?[AlphaSteps];
        for (int index = 0; index < AlphaSteps; index++)
        {
            float nextAlpha = index / (float)(AlphaSteps - 1);
            alphaBrushes[index] = Root.ThemeResources.CreateSolidBrush(new ColorRgba(value.R, value.G, value.B, nextAlpha));
        }

        checkerLightBrush = Root.ThemeResources.CreateSolidBrush(ColorRgba.FromBytes(232, 236, 240));
        checkerDarkBrush = Root.ThemeResources.CreateSolidBrush(ColorRgba.FromBytes(155, 164, 172));
        previewBrush = Root.ThemeResources.CreateSolidBrush(Value);
    }

    private void DisposeColorResources()
    {
        DisposeAll(colorFieldBrushes);
        DisposeAll(hueBrushes);
        DisposeAll(alphaBrushes);
        colorFieldBrushes = null;
        hueBrushes = null;
        alphaBrushes = null;
        checkerLightBrush?.Dispose();
        checkerDarkBrush?.Dispose();
        previewBrush?.Dispose();
        checkerLightBrush = null;
        checkerDarkBrush = null;
        previewBrush = null;
    }

    private static void DisposeAll(SolidBrushHandle?[]? brushes)
    {
        if (brushes is null)
        {
            return;
        }

        foreach (SolidBrushHandle? brush in brushes)
        {
            brush?.Dispose();
        }
    }

    private string HexText()
    {
        byte r = ToByte(Value.R);
        byte g = ToByte(Value.G);
        byte b = ToByte(Value.B);
        byte a = ToByte(Value.A);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2} ({a})");
    }

    private static byte ToByte(float value)
        => (byte)Math.Clamp(MathF.Round(value * 255f), 0f, 255f);

    private static ColorRgba FromHsv(float hue, float saturation, float value, float alpha)
    {
        float normalizedHue = ((hue % 360f) + 360f) % 360f;
        float c = value * saturation;
        float x = c * (1f - MathF.Abs((normalizedHue / 60f % 2f) - 1f));
        float m = value - c;
        (float r, float g, float b) = normalizedHue switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return new ColorRgba(Math.Clamp(r + m, 0f, 1f), Math.Clamp(g + m, 0f, 1f), Math.Clamp(b + m, 0f, 1f), Math.Clamp(alpha, 0f, 1f));
    }

    private static void ToHsv(ColorRgba color, out float hue, out float saturation, out float value)
    {
        float max = MathF.Max(color.R, MathF.Max(color.G, color.B));
        float min = MathF.Min(color.R, MathF.Min(color.G, color.B));
        float delta = max - min;

        hue = delta == 0f
            ? 0f
            : max == color.R
                ? 60f * ((color.G - color.B) / delta % 6f)
                : max == color.G
                    ? 60f * (((color.B - color.R) / delta) + 2f)
                    : 60f * (((color.R - color.G) / delta) + 4f);

        if (hue < 0f)
        {
            hue += 360f;
        }

        saturation = max == 0f ? 0f : delta / max;
        value = max;
    }

    private enum DragPart
    {
        None,
        ColorField,
        Hue,
        Alpha,
    }
}
