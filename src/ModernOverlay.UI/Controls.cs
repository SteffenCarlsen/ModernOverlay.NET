namespace ModernOverlay.UI;

/// <summary>
/// Displays read-only text using the current UI theme or an explicit font override.
/// </summary>
public class TextBlock : UiElement
{
    private string text = string.Empty;
    private FontHandle? font;
    private UiHorizontalAlignment textAlignment = UiHorizontalAlignment.Left;
    private UiTextWrapping textWrapping = UiTextWrapping.NoWrap;
    private UiTextTrimming textTrimming = UiTextTrimming.None;
    private int maxLines = int.MaxValue;
    private float lineSpacing = 1.35f;

    /// <summary>
    /// Initializes a text block.
    /// </summary>
    public TextBlock()
    {
        ReceivesInput = false;
    }

    /// <summary>
    /// Gets or sets the displayed text.
    /// </summary>
    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets an optional font override.
    /// </summary>
    public FontHandle? Font
    {
        get => font;
        set => SetProperty(ref font, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.Resource);
    }

    /// <summary>
    /// Gets or sets horizontal alignment for rendered text lines.
    /// </summary>
    public UiHorizontalAlignment TextAlignment
    {
        get => textAlignment;
        set => SetProperty(ref textAlignment, value, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets text wrapping behavior. Wrapping affects measurement and rendering but does not treat newline characters as separate paragraphs.
    /// </summary>
    public UiTextWrapping TextWrapping
    {
        get => textWrapping;
        set => SetProperty(ref textWrapping, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets text trimming behavior used when text cannot fit in the allowed line width.
    /// </summary>
    public UiTextTrimming TextTrimming
    {
        get => textTrimming;
        set => SetProperty(ref textTrimming, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets the maximum number of rendered text lines. Extra text is omitted or trimmed according to <see cref="TextTrimming"/>.
    /// </summary>
    public int MaxLines
    {
        get => maxLines;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref maxLines, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets line spacing as a multiplier of measured line height.
    /// </summary>
    public float LineSpacing
    {
        get => lineSpacing;
        set
        {
            if (value <= 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LineSpacing must be finite and greater than zero.");
            }

            SetProperty(ref lineSpacing, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float resolvedFontSize = ResolveFontSize();
        float charWidth = CharacterWidth(resolvedFontSize);
        string[] lines = BuildLines(availableSize.Width - Padding.Horizontal, charWidth);
        int lineCount = Math.Max(1, lines.Length);
        float measuredLineHeight = MeasureText("M", resolvedFontSize).Height;
        float naturalTextWidth = lines.Length == 0
            ? 0f
            : lines.Max(line => MeasureText(line, resolvedFontSize).Width);
        float naturalWidth = naturalTextWidth + Padding.Horizontal;
        float width = MathF.Min(availableSize.Width, naturalWidth);
        float height = measuredLineHeight * LineSpacing * lineCount + Padding.Vertical;
        return new SizeF(width, height);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (Text.Length == 0)
        {
            return;
        }

        RectF content = ContentBounds;
        float resolvedFontSize = ResolveFontSize(context.Theme.Theme);
        float charWidth = CharacterWidth(resolvedFontSize);
        BrushHandle brush = ResolveForeground(context);
        FontHandle font = ResolveFontOverride(nameof(Font), Font, context.Theme.Font);
        string[] lines = BuildLines(content.Width, charWidth);
        float lineHeight = context.Draw.Measure.Text("M", font).Height * LineSpacing;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            float lineWidth = context.Draw.Measure.Text(line, font).Width;
            float x = TextAlignment switch
            {
                UiHorizontalAlignment.Center => content.X + MathF.Max(0f, content.Width - lineWidth) / 2f,
                UiHorizontalAlignment.Right => content.X + MathF.Max(0f, content.Width - lineWidth),
                _ => content.X,
            };
            context.Draw.Draw.Text(line, font, brush, new PointF(x, content.Y + index * lineHeight));
        }
    }

    private string[] BuildLines(float availableWidth, float charWidth)
    {
        if (Text.Length == 0)
        {
            return [];
        }

        int lineCapacity = TextWrapping == UiTextWrapping.Wrap && availableWidth > 0f
            ? Math.Max(1, (int)MathF.Floor(availableWidth / charWidth))
            : int.MaxValue;
        var lines = new List<string>();
        for (int offset = 0; offset < Text.Length && lines.Count < MaxLines; offset += lineCapacity)
        {
            int length = Math.Min(lineCapacity, Text.Length - offset);
            bool hasMoreText = offset + length < Text.Length;
            bool isLastAllowedLine = lines.Count == MaxLines - 1;
            string line = Text.Substring(offset, length);
            if ((TextWrapping == UiTextWrapping.NoWrap || isLastAllowedLine) && hasMoreText)
            {
                line = TrimLine(line, lineCapacity);
                lines.Add(line);
                break;
            }

            lines.Add(line);
            if (TextWrapping == UiTextWrapping.NoWrap)
            {
                break;
            }
        }

        return lines.ToArray();
    }

    private string TrimLine(string line, int lineCapacity)
    {
        if (TextTrimming != UiTextTrimming.CharacterEllipsis || lineCapacity <= 1 || line.Length == 0)
        {
            return line;
        }

        int take = Math.Min(line.Length, lineCapacity - 1);
        return line[..take] + "\u2026";
    }

    private float ResolveFontSize(UiTheme? theme = null)
        => Font is { IsDisposed: false }
            ? Font.Options.Size
            : (theme ?? Root?.ThemeResources.Theme ?? UiTheme.Default).FontSize;

    private SizeF MeasureText(string value, float resolvedFontSize)
    {
        if (Root is { } root)
        {
            FontHandle font = ResolveFontOverride(nameof(Font), Font, root.ThemeResources.Font);
            if (root.TryMeasureText(value, font, out SizeF measured))
            {
                return measured;
            }
        }

        return new SizeF(value.Length * CharacterWidth(resolvedFontSize), resolvedFontSize);
    }

    private static float CharacterWidth(float fontSize) => MathF.Max(1f, fontSize * 0.56f);
}

/// <summary>
/// Displays text that can optionally focus a target element when clicked.
/// </summary>
public sealed class Label : TextBlock
{
    private UiElement? target;

    /// <summary>
    /// Initializes a label.
    /// </summary>
    public Label()
    {
        ReceivesInput = true;
    }

    /// <summary>
    /// Gets or sets the focus target activated by the label. The target must be focusable for the click to be handled.
    /// </summary>
    public UiElement? Target
    {
        get => target;
        set => SetProperty(ref target, value, UiInvalidation.None);
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        if (Target is { Focusable: true })
        {
            Target.Focus();
            args.Handled = true;
        }
    }
}

/// <summary>
/// Displays an image resource with optional source clipping, frame selection, scaling, and alignment.
/// </summary>
public sealed class Image : UiElement
{
    private ImageHandle? source;
    private RectF? sourceRect;
    private int frameIndex;
    private float imageOpacity = 1f;
    private UiImageStretch stretch = UiImageStretch.Uniform;
    private ImageInterpolationMode interpolationMode = ImageInterpolationMode.Linear;
    private UiHorizontalAlignment imageHorizontalAlignment = UiHorizontalAlignment.Center;
    private UiVerticalAlignment imageVerticalAlignment = UiVerticalAlignment.Center;

    /// <summary>
    /// Initializes an image element.
    /// </summary>
    public Image()
    {
        ReceivesInput = false;
        MinWidth = 0f;
        MinHeight = 0f;
    }

    /// <summary>
    /// Gets or sets the image resource to draw.
    /// </summary>
    public ImageHandle? Source
    {
        get => source;
        set => SetProperty(ref source, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets the optional source rectangle within the image, in source-image pixels. When set, measurement uses this rectangle size.
    /// </summary>
    public RectF? SourceRect
    {
        get => sourceRect;
        set
        {
            if (value is { } rect && (rect.Width < 0f || rect.Height < 0f || !float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height)))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "SourceRect must be finite and non-negative.");
            }

            SetProperty(ref sourceRect, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets the image frame index for multi-frame images. Single-frame images normally use zero.
    /// </summary>
    public int FrameIndex
    {
        get => frameIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SetProperty(ref frameIndex, value, UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets image opacity from 0 to 1, where 0 is fully transparent and 1 is fully opaque.
    /// </summary>
    public float ImageOpacity
    {
        get => imageOpacity;
        set
        {
            if (value is < 0f or > 1f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Image opacity must be finite and between 0 and 1.");
            }

            SetProperty(ref imageOpacity, value, UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets how the image is scaled inside content bounds before alignment is applied.
    /// </summary>
    public UiImageStretch Stretch
    {
        get => stretch;
        set => SetProperty(ref stretch, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets the interpolation mode used when drawing the image.
    /// </summary>
    public ImageInterpolationMode InterpolationMode
    {
        get => interpolationMode;
        set => SetProperty(ref interpolationMode, value, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets horizontal image alignment inside content bounds when <see cref="Stretch"/> leaves unused horizontal space.
    /// </summary>
    public UiHorizontalAlignment ImageHorizontalAlignment
    {
        get => imageHorizontalAlignment;
        set => SetProperty(ref imageHorizontalAlignment, value, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets vertical image alignment inside content bounds when <see cref="Stretch"/> leaves unused vertical space.
    /// </summary>
    public UiVerticalAlignment ImageVerticalAlignment
    {
        get => imageVerticalAlignment;
        set => SetProperty(ref imageVerticalAlignment, value, UiInvalidation.Render);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF sourceSize = GetSourceSize();
        if (sourceSize.Width <= 0f || sourceSize.Height <= 0f)
        {
            return new SizeF(Padding.Horizontal, Padding.Vertical);
        }

        SizeF contentAvailable = UiGeometry.Deflate(availableSize, Padding);
        SizeF imageSize = Stretch switch
        {
            UiImageStretch.None => sourceSize,
            UiImageStretch.Fill => contentAvailable,
            UiImageStretch.Uniform => ScaleToFit(sourceSize, contentAvailable, fill: false),
            UiImageStretch.UniformToFill => ScaleToFit(sourceSize, contentAvailable, fill: true),
            _ => sourceSize,
        };

        return UiGeometry.Inflate(imageSize, Padding);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (Source is null || ImageOpacity <= 0f)
        {
            return;
        }

        RectF destination = GetDestinationRect(ContentBounds);
        if (destination.Width <= 0f || destination.Height <= 0f)
        {
            return;
        }

        context.Draw.Draw.Image(Source, FrameIndex, destination, SourceRect, ImageOpacity, InterpolationMode);
    }

    private RectF GetDestinationRect(RectF content)
    {
        SizeF sourceSize = GetSourceSize();
        SizeF destinationSize = sourceSize.Width <= 0f || sourceSize.Height <= 0f
            ? new SizeF(content.Width, content.Height)
            : Stretch switch
            {
                UiImageStretch.None => new SizeF(MathF.Min(sourceSize.Width, content.Width), MathF.Min(sourceSize.Height, content.Height)),
                UiImageStretch.Fill => new SizeF(content.Width, content.Height),
                UiImageStretch.Uniform => ScaleToFit(sourceSize, new SizeF(content.Width, content.Height), fill: false),
                UiImageStretch.UniformToFill => ScaleToFit(sourceSize, new SizeF(content.Width, content.Height), fill: true),
                _ => new SizeF(content.Width, content.Height),
            };

        float x = AlignX(content, destinationSize.Width);
        float y = AlignY(content, destinationSize.Height);
        return new RectF(x, y, destinationSize.Width, destinationSize.Height);
    }

    private SizeF GetSourceSize()
        => SourceRect is { } rect
            ? new SizeF(rect.Width, rect.Height)
            : new SizeF(float.IsNaN(Width) ? 0f : Width, float.IsNaN(Height) ? 0f : Height);

    private static SizeF ScaleToFit(SizeF sourceSize, SizeF available, bool fill)
    {
        if (sourceSize.Width <= 0f || sourceSize.Height <= 0f || available.Width <= 0f || available.Height <= 0f)
        {
            return new SizeF(0f, 0f);
        }

        float xScale = available.Width / sourceSize.Width;
        float yScale = available.Height / sourceSize.Height;
        float scale = fill ? MathF.Max(xScale, yScale) : MathF.Min(xScale, yScale);
        return new SizeF(sourceSize.Width * scale, sourceSize.Height * scale);
    }

    private float AlignX(RectF content, float width)
        => ImageHorizontalAlignment switch
        {
            UiHorizontalAlignment.Center => content.X + (content.Width - width) / 2f,
            UiHorizontalAlignment.Right => content.X + content.Width - width,
            _ => content.X,
        };

    private float AlignY(RectF content, float height)
        => ImageVerticalAlignment switch
        {
            UiVerticalAlignment.Center => content.Y + (content.Height - height) / 2f,
            UiVerticalAlignment.Bottom => content.Y + content.Height - height,
            _ => content.Y,
        };
}

/// <summary>
/// Represents a clickable command or content button.
/// </summary>
public class Button : ContentControl
{
    private string text = string.Empty;
    private UiCommand? command;
    private object? commandParameter;
    private UiHorizontalAlignment textHorizontalAlignment = UiHorizontalAlignment.Center;
    private UiVerticalAlignment textVerticalAlignment = UiVerticalAlignment.Center;
    private bool commandSubscribed;

    /// <summary>
    /// Initializes a button.
    /// </summary>
    public Button()
    {
        ReceivesInput = true;
        Focusable = true;
        Padding = new Thickness(10f, 6f);
        MinHeight = 28f;
    }

    /// <summary>
    /// Occurs when the button is activated by a left pointer release inside the button, Enter, or Space.
    /// </summary>
    public event EventHandler<UiClickEventArgs>? Click;

    /// <summary>
    /// Gets or sets button text used when <see cref="ContentControl.Content"/> is not set. Custom content takes precedence over this text.
    /// </summary>
    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets horizontal text alignment used when <see cref="ContentControl.Content"/> is not set.
    /// </summary>
    public UiHorizontalAlignment TextHorizontalAlignment
    {
        get => textHorizontalAlignment;
        set => SetProperty(ref textHorizontalAlignment, value, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets vertical text alignment used when <see cref="ContentControl.Content"/> is not set.
    /// </summary>
    public UiVerticalAlignment TextVerticalAlignment
    {
        get => textVerticalAlignment;
        set => SetProperty(ref textVerticalAlignment, value, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets the command invoked when the button activates. If <see cref="UiCommand.CanExecute(object?)"/> returns <see langword="false"/>, the button renders disabled and is removed from hit testing.
    /// </summary>
    public UiCommand? Command
    {
        get => command;
        set
        {
            if (command == value)
            {
                return;
            }

            UnsubscribeCommand();

            command = value;
            SubscribeCommand();

            InvalidateRender();
        }
    }

    /// <summary>
    /// Gets or sets the parameter passed to <see cref="Command"/> for both can-execute checks and execution.
    /// </summary>
    public object? CommandParameter
    {
        get => commandParameter;
        set => SetProperty(ref commandParameter, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        if (Content is not null)
        {
            SizeF contentSize = base.MeasureCore(availableSize);
            return new SizeF(MathF.Max(MinWidth, contentSize.Width), MathF.Max(MinHeight, contentSize.Height));
        }

        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        float width = MathF.Min(availableSize.Width, Text.Length * fontSize * 0.6f + Padding.Horizontal);
        float height = fontSize * 1.35f + Padding.Vertical;
        return new SizeF(MathF.Max(MinWidth, width), MathF.Max(MinHeight, height));
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        ArrangeContent();
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BrushHandle background = CanExecute() ? ResolveBackground(context) : ResolveDisabledBrush(context);
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, background);
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, IsFocused && IsEffectivelyEnabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));

        if (Content is not null)
        {
            base.RenderCore(context);
        }
        else if (Text.Length > 0)
        {
            RectF content = ContentBounds;
            SizeF textSize = context.Draw.Measure.Text(Text, context.Theme.Font);
            float x = TextHorizontalAlignment switch
            {
                UiHorizontalAlignment.Center => content.X + MathF.Max(0f, content.Width - textSize.Width) / 2f,
                UiHorizontalAlignment.Right => content.X + MathF.Max(0f, content.Width - textSize.Width),
                _ => content.X,
            };
            float y = TextVerticalAlignment switch
            {
                UiVerticalAlignment.Center => content.Y + MathF.Max(0f, content.Height - textSize.Height) / 2f,
                UiVerticalAlignment.Bottom => content.Y + MathF.Max(0f, content.Height - textSize.Height),
                _ => content.Y,
            };
            context.Draw.Draw.Text(Text, context.Theme.Font, CanExecute() ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(x, y));
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left || !CanExecute())
        {
            return;
        }

        CapturePointer();
        args.Handled = true;
    }

    protected override void OnPointerReleased(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        ReleasePointerCapture();
        if (!args.IsDragGesture && UiGeometry.Contains(Bounds, args.Position) && CanExecute())
        {
            InvokeClick(args.Position, args.Button, args.ClickCount);
            args.Handled = true;
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        if ((args.VirtualKey is UiVirtualKeys.Enter or UiVirtualKeys.Space) && CanExecute())
        {
            InvokeClick(new PointF(Bounds.X, Bounds.Y), OverlayPointerButton.None, clickCount: 1);
            args.Handled = true;
        }
    }

    protected override bool HitTestCore(PointF point)
        => CanExecute() && base.HitTestCore(point);

    protected override void OnAttached()
    {
        base.OnAttached();
        SubscribeCommand();
    }

    protected override void OnDetached()
    {
        UnsubscribeCommand();
        base.OnDetached();
    }

    protected bool CanExecute() => IsEffectivelyEnabled && (Command?.CanExecute(CommandParameter) ?? true);

    protected virtual void InvokeClick(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        Click?.Invoke(this, new UiClickEventArgs(position, button, clickCount));
        Command?.Execute(CommandParameter);
    }

    private void HandleCanExecuteChanged(object? sender, EventArgs args)
    {
        InvalidateRender();
        Root?.Invalidate(UiInvalidation.InputRegion);
    }

    private void SubscribeCommand()
    {
        if (commandSubscribed || command is null || Root is null)
        {
            return;
        }

        command.CanExecuteChanged += HandleCanExecuteChanged;
        commandSubscribed = true;
    }

    private void UnsubscribeCommand()
    {
        if (!commandSubscribed || command is null)
        {
            return;
        }

        command.CanExecuteChanged -= HandleCanExecuteChanged;
        commandSubscribed = false;
    }
}

/// <summary>
/// Represents a button with checked, unchecked, and optional indeterminate state.
/// </summary>
public class ToggleButton : Button
{
    private UiToggleState checkState;
    private bool isThreeState;

    /// <summary>
    /// Occurs when the boolean checked state changes. This event also fires when the state moves to or from <see cref="UiToggleState.Indeterminate"/>.
    /// </summary>
    public event EventHandler? CheckedChanged;

    /// <summary>
    /// Occurs when the full <see cref="CheckState"/> value changes.
    /// </summary>
    public event EventHandler? CheckStateChanged;

    /// <summary>
    /// Gets or sets whether the button is checked. Setting <see langword="false"/> clears both checked and indeterminate states.
    /// </summary>
    public bool IsChecked
    {
        get => CheckState == UiToggleState.Checked;
        set => CheckState = value ? UiToggleState.Checked : UiToggleState.Unchecked;
    }

    /// <summary>
    /// Gets or sets whether the button is in the indeterminate state. If <see cref="IsThreeState"/> is <see langword="false"/>, setting this to <see langword="true"/> coerces the state to unchecked.
    /// </summary>
    public bool IsIndeterminate
    {
        get => CheckState == UiToggleState.Indeterminate;
        set => CheckState = value ? UiToggleState.Indeterminate : UiToggleState.Unchecked;
    }

    /// <summary>
    /// Gets or sets whether the indeterminate state is allowed during pointer or keyboard toggling.
    /// </summary>
    public bool IsThreeState
    {
        get => isThreeState;
        set
        {
            if (SetProperty(ref isThreeState, value, UiInvalidation.Render)
                && !value
                && CheckState == UiToggleState.Indeterminate)
            {
                CheckState = UiToggleState.Unchecked;
            }
        }
    }

    /// <summary>
    /// Gets or sets the full toggle state. Unsupported enum values throw, and indeterminate is coerced to unchecked unless <see cref="IsThreeState"/> is enabled.
    /// </summary>
    public UiToggleState CheckState
    {
        get => checkState;
        set => SetCheckState(value);
    }

    protected override void InvokeClick(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        CheckState = NextCheckState();
        base.InvokeClick(position, button, clickCount);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        base.RenderCore(context);
        if (CheckState == UiToggleState.Checked)
        {
            RectF mark = UiGeometry.Deflate(Bounds, new Thickness(4f));
            context.Draw.Draw.RoundedRectangle(mark, 3f, 3f, ResolveAccentBrush(context), 2f);
        }
        else if (CheckState == UiToggleState.Indeterminate)
        {
            RectF mark = UiGeometry.Deflate(Bounds, new Thickness(5f, Bounds.Height / 2f - 1f));
            context.Draw.Fill.Rectangle(mark, ResolveAccentBrush(context));
        }
    }

    private void SetCheckState(UiToggleState value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Unsupported toggle state.");
        }

        UiToggleState next = !IsThreeState && value == UiToggleState.Indeterminate
            ? UiToggleState.Unchecked
            : value;
        if (SetProperty(ref checkState, next, UiInvalidation.Render))
        {
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            CheckStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private UiToggleState NextCheckState()
        => CheckState switch
        {
            UiToggleState.Unchecked => UiToggleState.Checked,
            UiToggleState.Checked => IsThreeState ? UiToggleState.Indeterminate : UiToggleState.Unchecked,
            UiToggleState.Indeterminate => UiToggleState.Unchecked,
            _ => UiToggleState.Unchecked,
        };
}

/// <summary>
/// Represents a checkbox control that renders a square check target and optional text or content.
/// </summary>
public sealed class CheckBox : ToggleButton
{
    /// <summary>
    /// Initializes a checkbox.
    /// </summary>
    public CheckBox()
    {
        Padding = new Thickness(28f, 5f, 8f, 5f);
        MinHeight = 26f;
    }

    protected override void RenderCore(UiRenderContext context)
    {
        RectF box = new(Bounds.X + 6f, Bounds.Y + (Bounds.Height - 14f) / 2f, 14f, 14f);
        BrushHandle stateBrush = IsEffectivelyEnabled ? ResolveAccentBrush(context) : ResolveDisabledBrush(context);
        context.Draw.Draw.Rectangle(box, IsFocused && IsEffectivelyEnabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        if (CheckState == UiToggleState.Checked)
        {
            context.Draw.Fill.Rectangle(UiGeometry.Deflate(box, new Thickness(3f)), stateBrush);
        }
        else if (CheckState == UiToggleState.Indeterminate)
        {
            RectF mark = new(box.X + 3f, box.Y + box.Height / 2f - 1f, box.Width - 6f, 2f);
            context.Draw.Fill.Rectangle(mark, stateBrush);
        }

        if (Content is not null)
        {
            Content.Render(context);
        }
        else if (Text.Length > 0)
        {
            RectF content = ContentBounds;
            context.Draw.Draw.Text(Text, context.Theme.Font, IsEffectivelyEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(content.X, content.Y));
        }
    }
}

/// <summary>
/// Represents a mutually exclusive radio button.
/// </summary>
public sealed class RadioButton : ToggleButton
{
    private string? groupName;

    /// <summary>
    /// Initializes a radio button.
    /// </summary>
    public RadioButton()
    {
        Padding = new Thickness(28f, 5f, 8f, 5f);
        MinHeight = 26f;
    }

    /// <summary>
    /// Gets or sets the radio group name. Radio buttons clear checked peers with the same parent and group name.
    /// </summary>
    public string? GroupName
    {
        get => groupName;
        set => SetProperty(ref groupName, value, UiInvalidation.None);
    }

    protected override void InvokeClick(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        if (IsChecked)
        {
            base.InvokeClick(position, button, clickCount);
            return;
        }

        ClearPeers();
        base.InvokeClick(position, button, clickCount);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        PointF center = new(Bounds.X + 13f, Bounds.Y + Bounds.Height / 2f);
        BrushHandle stateBrush = IsEffectivelyEnabled ? ResolveAccentBrush(context) : ResolveDisabledBrush(context);
        context.Draw.Draw.Circle(center, 7f, IsFocused && IsEffectivelyEnabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        if (IsChecked)
        {
            context.Draw.Fill.Circle(center, 4f, stateBrush);
        }

        if (Content is not null)
        {
            Content.Render(context);
        }
        else if (Text.Length > 0)
        {
            RectF content = ContentBounds;
            context.Draw.Draw.Text(Text, context.Theme.Font, IsEffectivelyEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(content.X, content.Y));
        }
    }

    private void ClearPeers()
    {
        if (Parent is null)
        {
            return;
        }

        foreach (UiElement sibling in Parent.Children)
        {
            if (sibling is RadioButton radio
                && !ReferenceEquals(radio, this)
                && string.Equals(radio.GroupName, GroupName, StringComparison.Ordinal))
            {
                radio.IsChecked = false;
            }
        }
    }
}

/// <summary>
/// Base class for controls that expose a numeric range value.
/// </summary>
public abstract class RangeBase : UiControl
{
    private float minimum;
    private float maximum = 100f;
    private float value;
    private float smallChange = 1f;
    private float largeChange = 10f;

    /// <summary>
    /// Occurs when <see cref="Value"/> changes.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Gets or sets the minimum value. Raising it above <see cref="Maximum"/> also raises <see cref="Maximum"/>.
    /// </summary>
    public float Minimum
    {
        get => minimum;
        set
        {
            SetProperty(ref minimum, value, UiInvalidation.Render);
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            Value = this.value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum value. Values lower than <see cref="Minimum"/> are coerced to <see cref="Minimum"/>.
    /// </summary>
    public float Maximum
    {
        get => maximum;
        set
        {
            SetProperty(ref maximum, MathF.Max(value, minimum), UiInvalidation.Render);
            Value = this.value;
        }
    }

    /// <summary>
    /// Gets or sets the current value, clamped to <see cref="Minimum"/> and <see cref="Maximum"/>.
    /// </summary>
    public float Value
    {
        get => value;
        set
        {
            if (SetProperty(ref this.value, Math.Clamp(value, Minimum, Maximum), UiInvalidation.Render))
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the small keyboard increment used by arrow-key adjustments.
    /// </summary>
    public float SmallChange
    {
        get => smallChange;
        set
        {
            if (value <= 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "SmallChange must be finite and greater than zero.");
            }

            SetProperty(ref smallChange, value, UiInvalidation.None);
        }
    }

    /// <summary>
    /// Gets or sets the large keyboard increment used by Page Up and Page Down adjustments.
    /// </summary>
    public float LargeChange
    {
        get => largeChange;
        set
        {
            if (value <= 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LargeChange must be finite and greater than zero.");
            }

            SetProperty(ref largeChange, value, UiInvalidation.None);
        }
    }

    protected float ValueRatio
    {
        get
        {
            float range = MathF.Max(1f, Maximum - Minimum);
            return Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    protected void ChangeValueBy(float delta) => Value += delta;
}

/// <summary>
/// Displays read-only progress for a numeric range.
/// </summary>
public sealed class ProgressBar : RangeBase
{
    /// <summary>
    /// Initializes a progress bar.
    /// </summary>
    public ProgressBar()
    {
        MinHeight = 10f;
        Height = 10f;
    }

    protected override SizeF MeasureCore(SizeF availableSize) => new(MathF.Min(160f, availableSize.Width), Height);

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        float ratio = ValueRatio;
        if (ratio > 0f)
        {
            RectF fill = Bounds with { Width = Bounds.Width * ratio };
            context.Draw.Fill.RoundedRectangle(fill, 4f, 4f, enabled ? ResolveAccentBrush(context) : ResolveBorderBrush(context));
        }

        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveBorderBrush(context));
    }
}

/// <summary>
/// Allows pointer and keyboard editing of a numeric range value.
/// </summary>
public sealed class Slider : RangeBase
{
    private const float ThumbRadius = 7f;
    private UiOrientation orientation = UiOrientation.Horizontal;

    /// <summary>
    /// Initializes a slider.
    /// </summary>
    public Slider()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 120f;
        MinHeight = 22f;
        Height = 22f;
    }

    /// <summary>
    /// Gets or sets the slider orientation. Horizontal sliders increase left-to-right; vertical sliders increase bottom-to-top.
    /// </summary>
    public UiOrientation Orientation
    {
        get => orientation;
        set => SetProperty(ref orientation, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
        => Orientation == UiOrientation.Horizontal
            ? new SizeF(MathF.Min(160f, availableSize.Width), Height)
            : new SizeF(Height, MathF.Min(160f, availableSize.Height));

    protected override void RenderCore(UiRenderContext context)
    {
        RectF valueTrack = ValueTrackBounds;
        RectF track = Orientation == UiOrientation.Horizontal
            ? new RectF(valueTrack.X, Bounds.Y + Bounds.Height / 2f - 2f, valueTrack.Width, 4f)
            : new RectF(Bounds.X + Bounds.Width / 2f - 2f, valueTrack.Y, 4f, valueTrack.Height);
        bool enabled = IsEffectivelyEnabled;
        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveFocusBrush(context));
        }

        context.Draw.Fill.RoundedRectangle(track, 2f, 2f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(track, 2f, 2f, ResolveBorderBrush(context));

        float ratio = ValueRatio;
        PointF center = Orientation == UiOrientation.Horizontal
            ? new PointF(valueTrack.X + valueTrack.Width * ratio, Bounds.Y + Bounds.Height / 2f)
            : new PointF(Bounds.X + Bounds.Width / 2f, valueTrack.Y + valueTrack.Height * (1f - ratio));
        context.Draw.Fill.Circle(center, ThumbRadius, !enabled ? ResolveDisabledBrush(context) : IsPointerCaptured ? PressedBackground ?? context.Theme.SurfacePressed : ResolveAccentBrush(context));
        context.Draw.Draw.Circle(center, ThumbRadius, IsFocused && enabled ? ResolveForeground(context) : ResolveBorderBrush(context));
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        CapturePointer();
        UpdateValueFromPoint(args.Position);
        args.Handled = true;
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        if (!IsPointerCaptured)
        {
            return;
        }

        UpdateValueFromPoint(args.Position);
        args.Handled = true;
    }

    protected override void OnPointerReleased(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        if (IsPointerCaptured)
        {
            UpdateValueFromPoint(args.Position);
            ReleasePointerCapture();
            args.Handled = true;
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Left:
            case UiVirtualKeys.Down:
                ChangeValueBy(-SmallChange);
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
            case UiVirtualKeys.Up:
                ChangeValueBy(SmallChange);
                args.Handled = true;
                break;
            case UiVirtualKeys.PageDown:
                ChangeValueBy(-LargeChange);
                args.Handled = true;
                break;
            case UiVirtualKeys.PageUp:
                ChangeValueBy(LargeChange);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                Value = Minimum;
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                Value = Maximum;
                args.Handled = true;
                break;
        }
    }

    private void UpdateValueFromPoint(PointF point)
    {
        RectF track = ValueTrackBounds;
        float ratio = Orientation == UiOrientation.Horizontal
            ? (point.X - track.X) / MathF.Max(1f, track.Width)
            : 1f - (point.Y - track.Y) / MathF.Max(1f, track.Height);
        Value = Minimum + (Maximum - Minimum) * Math.Clamp(ratio, 0f, 1f);
    }

    private RectF ValueTrackBounds
    {
        get
        {
            if (Orientation == UiOrientation.Horizontal)
            {
                float inset = MathF.Min(ThumbRadius, Bounds.Width / 2f);
                return new RectF(Bounds.X + inset, Bounds.Y, MathF.Max(1f, Bounds.Width - inset * 2f), Bounds.Height);
            }

            float verticalInset = MathF.Min(ThumbRadius, Bounds.Height / 2f);
            return new RectF(Bounds.X, Bounds.Y + verticalInset, Bounds.Width, MathF.Max(1f, Bounds.Height - verticalInset * 2f));
        }
    }
}

/// <summary>
/// Provides text editing with single-line and multiline modes.
/// </summary>
public sealed class TextBox : UiControl
{
    private const float DefaultLineSpacing = 1.35f;

    private string text = string.Empty;
    private string placeholder = string.Empty;
    private TextBoxMode mode;
    private bool acceptsReturn;
    private bool acceptsReturnExplicit;
    private UiTextWrapping textWrapping = UiTextWrapping.NoWrap;
    private bool textWrappingExplicit;
    private int caretIndex;
    private int selectionStart;
    private int selectionLength;
    private int maxLength = int.MaxValue;
    private int maxLines = int.MaxValue;
    private float lineSpacing = DefaultLineSpacing;
    private bool isReadOnly;
    private bool selecting;
    private bool suppressNextReturnTextInput;
    private int selectionAnchor;
    private float horizontalOffset;
    private float verticalOffset;
    private float lineHeight = UiTheme.Default.FontSize * DefaultLineSpacing;
    private float preferredCaretX = float.NaN;
    private TextBoxLine[] textLines = [new(0, 0, 0f, [0f])];

    /// <summary>
    /// Initializes a text box.
    /// </summary>
    public TextBox()
    {
        ReceivesInput = true;
        Focusable = true;
        Padding = new Thickness(8f, 5f);
        MinWidth = 120f;
        MinHeight = 28f;
        Height = 30f;
    }

    /// <summary>
    /// Occurs when <see cref="Text"/> changes.
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Gets or sets the text value. Assigned text is clamped to <see cref="MaxLength"/> without splitting surrogate pairs.
    /// </summary>
    public string Text
    {
        get => text;
        set
        {
            string next = value ?? string.Empty;
            next = ClampTextToMaxLength(next);

            if (SetProperty(ref text, next, UiInvalidation.Measure | UiInvalidation.Render))
            {
                CaretIndex = CoerceTextBoundaryAtOrBefore(text, CaretIndex);
                selectionStart = CoerceTextBoundaryAtOrBefore(text, selectionStart);
                selectionLength = CoerceSelectionLength(selectionStart, selectionLength);
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets placeholder text displayed when <see cref="Text"/> is empty.
    /// </summary>
    public string Placeholder
    {
        get => placeholder;
        set => SetProperty(ref placeholder, value ?? string.Empty, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets whether the text box edits one horizontal line or multiple visual lines. Switching to multiline defaults <see cref="AcceptsReturn"/> to <see langword="true"/> and <see cref="TextWrapping"/> to <see cref="UiTextWrapping.Wrap"/> unless those properties were set explicitly.
    /// </summary>
    public TextBoxMode Mode
    {
        get => mode;
        set
        {
            if (SetProperty(ref mode, value, UiInvalidation.Measure | UiInvalidation.Render))
            {
                if (!acceptsReturnExplicit)
                {
                    acceptsReturn = mode == TextBoxMode.MultiLine;
                }

                if (!textWrappingExplicit)
                {
                    textWrapping = mode == TextBoxMode.MultiLine ? UiTextWrapping.Wrap : UiTextWrapping.NoWrap;
                }

                horizontalOffset = 0f;
                verticalOffset = 0f;
                preferredCaretX = float.NaN;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether Enter inserts a new line when <see cref="Mode"/> is <see cref="TextBoxMode.MultiLine"/>. When disabled, Enter is handled without modifying text.
    /// </summary>
    public bool AcceptsReturn
    {
        get => acceptsReturn;
        set
        {
            acceptsReturnExplicit = true;
            SetProperty(ref acceptsReturn, value, UiInvalidation.None);
        }
    }

    /// <summary>
    /// Gets or sets text wrapping behavior used by multiline text layout. Single-line text boxes always edit as one horizontal line.
    /// </summary>
    public UiTextWrapping TextWrapping
    {
        get => textWrapping;
        set
        {
            textWrappingExplicit = true;
            SetProperty(ref textWrapping, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets the zero-based caret index in UTF-16 code units. Values are clamped to valid text boundaries.
    /// </summary>
    public int CaretIndex
    {
        get => caretIndex;
        set
        {
            if (SetProperty(ref caretIndex, CoerceCaretIndex(value), UiInvalidation.Render))
            {
                EnsureCaretVisible();
                Root?.RestartCaretBlink();
            }
        }
    }

    /// <summary>
    /// Gets or sets the zero-based selection start index in UTF-16 code units. Values are clamped to valid text boundaries.
    /// </summary>
    public int SelectionStart
    {
        get => selectionStart;
        set
        {
            selectionStart = CoerceCaretIndex(value);
            selectionLength = CoerceSelectionLength(selectionStart, selectionLength);
            InvalidateRender();
        }
    }

    /// <summary>
    /// Gets or sets the selection length in UTF-16 code units. The range is clamped so it remains inside <see cref="Text"/>.
    /// </summary>
    public int SelectionLength
    {
        get => selectionLength;
        set
        {
            selectionLength = CoerceSelectionLength(SelectionStart, value);
            InvalidateRender();
        }
    }

    /// <summary>
    /// Gets or sets the maximum text length in UTF-16 code units. Existing text is truncated when the limit is reduced.
    /// </summary>
    public int MaxLength
    {
        get => maxLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (SetProperty(ref maxLength, value, UiInvalidation.None) && Text.Length > value)
            {
                Text = ClampTextToMaxLength(Text);
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of lines used for automatic multiline measurement. The value does not reject additional text; it limits measured height.
    /// </summary>
    public int MaxLines
    {
        get => maxLines;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref maxLines, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets the multiline line-spacing multiplier applied to measured font height.
    /// </summary>
    public float LineSpacing
    {
        get => lineSpacing;
        set
        {
            if (value <= 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LineSpacing must be finite and greater than zero.");
            }

            SetProperty(ref lineSpacing, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

    /// <summary>
    /// Gets or sets whether text editing is disabled while focus, caret movement, and selection remain available.
    /// </summary>
    public bool IsReadOnly
    {
        get => isReadOnly;
        set => SetProperty(ref isReadOnly, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        if (Mode == TextBoxMode.SingleLine)
        {
            return new SizeF(
                MathF.Min(availableSize.Width, MathF.Max(MinWidth, Text.Length * CharacterWidth(fontSize) + Padding.Horizontal)),
                MathF.Max(MinHeight, fontSize * DefaultLineSpacing + Padding.Vertical));
        }

        TextBoxLine[] lines = BuildTextLines(MathF.Max(0f, availableSize.Width - Padding.Horizontal), context: null, font: null);
        int visibleLineCount = Math.Max(1, Math.Min(MaxLines, lines.Length));
        float measuredLineHeight = ResolveLineHeight(context: null, font: null);
        float naturalWidth = lines.Length == 0 ? MinWidth : lines.Max(line => line.Width) + Padding.Horizontal;
        float width = TextWrapping == UiTextWrapping.Wrap && float.IsFinite(availableSize.Width)
            ? availableSize.Width
            : MathF.Min(availableSize.Width, MathF.Max(MinWidth, naturalWidth));
        float height = MathF.Max(MinHeight, measuredLineHeight * visibleLineCount + Padding.Vertical);
        return new SizeF(width, height);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        BrushHandle border = IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context);
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, border);

        RectF content = ContentBounds;
        float fontSize = context.Theme.Theme.FontSize;
        FontHandle font = context.Theme.Font;
        UpdateTextLayout(context, font);
        EnsureCaretVisible(useCurrentLayout: true);
        if (content.Width <= 0f || content.Height <= 0f)
        {
            return;
        }

        using ScopedClip _ = context.Draw.Clip(content);
        if (SelectionLength > 0 && Text.Length > 0)
        {
            RenderSelection(context, content);
        }

        string displayText = Text.Length == 0 ? Placeholder : Text;
        BrushHandle textBrush = !enabled ? ResolveDisabledBrush(context) : Text.Length == 0 ? context.Theme.MutedForeground : ResolveForeground(context);
        if (displayText.Length > 0)
        {
            BrushHandle brush = IsReadOnly || !enabled ? ResolveDisabledBrush(context) : textBrush;
            if (Text.Length == 0)
            {
                context.Draw.Draw.Text(displayText, font, brush, new PointF(content.X, content.Y));
            }
            else
            {
                RenderTextLines(context, content, font, brush);
            }
        }

        if (enabled && IsFocused && !IsReadOnly && (Root?.IsCaretVisible ?? true))
        {
            TextBoxCaret caret = CaretAt(CaretIndex);
            float caretX = content.X + caret.X - horizontalOffset;
            float caretY = content.Y + caret.Y - verticalOffset;
            context.Draw.Draw.Line(new PointF(caretX, caretY), new PointF(caretX, caretY + fontSize * 1.3f), ResolveAccentBrush(context));
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        Focus();
        CaretIndex = CaretIndexFromPoint(args.Position);
        preferredCaretX = float.NaN;
        selectionAnchor = CaretIndex;
        ClearSelection();
        selecting = true;
        CapturePointer();
        args.Handled = true;
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        if (!selecting || !IsPointerCaptured)
        {
            return;
        }

        CaretIndex = CaretIndexFromPoint(args.Position);
        preferredCaretX = float.NaN;
        SelectFromAnchor(selectionAnchor, CaretIndex);
        args.Handled = true;
    }

    protected override void OnPointerReleased(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left || !selecting)
        {
            return;
        }

        selecting = false;
        ReleasePointerCapture();
        args.Handled = true;
    }

    protected override void OnTextInput(UiTextInputEventArgs args)
    {
        if (args.Text.Length == 0)
        {
            return;
        }

        args.Handled = true;
        if (IsReadOnly)
        {
            return;
        }

        if (suppressNextReturnTextInput && IsOnlyReturnInput(args.Text))
        {
            suppressNextReturnTextInput = false;
            return;
        }

        suppressNextReturnTextInput = false;
        string filtered = FilterTextInput(args.Text);
        if (filtered.Length == 0)
        {
            return;
        }

        InsertText(filtered);
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        bool control = (args.Modifiers & OverlayModifierKeys.Control) != 0;
        bool shift = (args.Modifiers & OverlayModifierKeys.Shift) != 0;
        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Left:
                MoveCaret(PreviousTextBoundary(CaretIndex), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
                MoveCaret(NextTextBoundary(CaretIndex), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Up when Mode == TextBoxMode.MultiLine:
                MoveCaretVertical(-1, shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Down when Mode == TextBoxMode.MultiLine:
                MoveCaretVertical(1, shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                MoveCaret(control && Mode == TextBoxMode.MultiLine ? 0 : HomeIndex(), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                MoveCaret(control && Mode == TextBoxMode.MultiLine ? Text.Length : EndIndex(), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Enter when Mode == TextBoxMode.MultiLine && AcceptsReturn:
                if (!IsReadOnly)
                {
                    InsertText("\n");
                    suppressNextReturnTextInput = true;
                }

                args.Handled = true;
                break;
            case UiVirtualKeys.A when control:
                selectionAnchor = 0;
                SelectionStart = 0;
                SelectionLength = Text.Length;
                CaretIndex = Text.Length;
                args.Handled = true;
                break;
            case UiVirtualKeys.Backspace when !IsReadOnly:
                Backspace();
                args.Handled = true;
                break;
            case UiVirtualKeys.Delete when !IsReadOnly:
                Delete();
                args.Handled = true;
                break;
        }
    }

    private void InsertText(string value)
    {
        DeleteSelection();
        int available = MaxLength - Text.Length;
        if (available <= 0)
        {
            return;
        }

        string insert = value.Length > available ? value[..SafeTextBoundaryAtOrBefore(value, available)] : value;
        if (insert.Length == 0)
        {
            return;
        }

        Text = Text.Insert(CaretIndex, insert);
        CaretIndex += insert.Length;
        preferredCaretX = float.NaN;
        ClearSelection();
    }

    private void Backspace()
    {
        if (DeleteSelection())
        {
            return;
        }

        if (CaretIndex <= 0)
        {
            return;
        }

        int previous = PreviousTextBoundary(CaretIndex);
        Text = Text.Remove(previous, CaretIndex - previous);
        CaretIndex = previous;
        preferredCaretX = float.NaN;
    }

    private void Delete()
    {
        if (DeleteSelection())
        {
            return;
        }

        if (CaretIndex >= Text.Length)
        {
            return;
        }

        int next = NextTextBoundary(CaretIndex);
        Text = Text.Remove(CaretIndex, next - CaretIndex);
        preferredCaretX = float.NaN;
    }

    private bool DeleteSelection()
    {
        if (SelectionLength <= 0)
        {
            return false;
        }

        Text = Text.Remove(SelectionStart, SelectionLength);
        CaretIndex = SelectionStart;
        preferredCaretX = float.NaN;
        ClearSelection();
        return true;
    }

    private void MoveCaret(int nextIndex, bool extendSelection)
    {
        if (extendSelection)
        {
            if (SelectionLength == 0)
            {
                selectionAnchor = CaretIndex;
            }

            CaretIndex = nextIndex;
            preferredCaretX = float.NaN;
            SelectFromAnchor(selectionAnchor, CaretIndex);
            return;
        }

        CaretIndex = nextIndex;
        preferredCaretX = float.NaN;
        ClearSelection();
    }

    private void MoveCaretVertical(int delta, bool extendSelection)
    {
        UpdateTextLayout(context: null, font: null);
        int currentLine = LineIndexForCaret(CaretIndex);
        TextBoxCaret caret = CaretAt(CaretIndex);
        float targetX = float.IsNaN(preferredCaretX) ? caret.X : preferredCaretX;
        int targetLine = Math.Clamp(currentLine + delta, 0, textLines.Length - 1);
        int targetIndex = IndexFromLineAdvance(textLines[targetLine], targetX);
        MoveCaret(targetIndex, extendSelection);
        preferredCaretX = targetX;
    }

    private int CaretIndexFromPoint(PointF point)
    {
        UpdateTextLayout(context: null, font: null);
        RectF content = ContentBounds;
        if (Mode == TextBoxMode.MultiLine)
        {
            float relativeY = MathF.Max(0f, point.Y - content.Y + verticalOffset);
            int lineIndex = Math.Clamp((int)MathF.Floor(relativeY / MathF.Max(1f, lineHeight)), 0, textLines.Length - 1);
            float relativeX = MathF.Max(0f, point.X - content.X + horizontalOffset);
            return CoerceCaretIndex(IndexFromLineAdvance(textLines[lineIndex], relativeX));
        }

        float lineWidth = textLines.Length == 0 ? 0f : textLines[0].Width;
        float singleLineX = Math.Clamp(point.X - content.X + horizontalOffset, 0f, lineWidth);
        return CoerceCaretIndex(IndexFromLineAdvance(textLines[0], singleLineX));
    }

    private void SelectFromAnchor(int anchor, int caret)
    {
        int start = CoerceCaretIndex(Math.Min(anchor, caret));
        int end = CoerceCaretIndex(Math.Max(anchor, caret));
        selectionStart = start;
        selectionLength = end - start;
        InvalidateRender();
    }

    private void EnsureCaretVisible(bool useCurrentLayout = false)
    {
        float contentWidth = ContentBounds.Width;
        float contentHeight = ContentBounds.Height;
        if (contentWidth <= 0f || contentHeight <= 0f)
        {
            return;
        }

        if (!useCurrentLayout)
        {
            UpdateTextLayout(context: null, font: null);
        }

        TextBoxCaret caret = CaretAt(CaretIndex);
        float caretX = caret.X;
        if (caretX < horizontalOffset)
        {
            horizontalOffset = caretX;
        }
        else if (caretX > horizontalOffset + contentWidth)
        {
            horizontalOffset = caretX - contentWidth;
        }

        float maxHorizontalOffset = MathF.Max(0f, textLines.Max(line => line.Width) - contentWidth);
        horizontalOffset = TextWrapping == UiTextWrapping.Wrap && Mode == TextBoxMode.MultiLine
            ? 0f
            : Math.Clamp(horizontalOffset, 0f, maxHorizontalOffset);

        if (Mode == TextBoxMode.SingleLine)
        {
            verticalOffset = 0f;
            return;
        }

        if (caret.Y < verticalOffset)
        {
            verticalOffset = caret.Y;
        }
        else if (caret.Y + lineHeight > verticalOffset + contentHeight)
        {
            verticalOffset = caret.Y + lineHeight - contentHeight;
        }

        float maxVerticalOffset = MathF.Max(0f, textLines.Length * lineHeight - contentHeight);
        verticalOffset = Math.Clamp(verticalOffset, 0f, maxVerticalOffset);
    }

    private float ResolveFontSize()
        => Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;

    private static float CharacterWidth(float fontSize) => MathF.Max(1f, fontSize * 0.56f);

    private string FilterTextInput(string value)
    {
        bool allowNewLine = Mode == TextBoxMode.MultiLine && AcceptsReturn;
        var filtered = new List<char>(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character == '\r' || character == '\n') && allowNewLine)
            {
                if (character == '\r' && index + 1 < value.Length && value[index + 1] == '\n')
                {
                    index++;
                }

                filtered.Add('\n');
                continue;
            }

            if (!char.IsControl(character))
            {
                filtered.Add(character);
            }
        }

        return new string(filtered.ToArray());
    }

    private void UpdateTextLayout(UiRenderContext? context, FontHandle? font)
    {
        float contentWidth = ContentBounds.Width;
        textLines = BuildTextLines(contentWidth, context, font);
        lineHeight = ResolveLineHeight(context, font);
    }

    private TextBoxLine[] BuildTextLines(float availableWidth, UiRenderContext? context, FontHandle? font)
    {
        float wrapWidth = Mode == TextBoxMode.MultiLine && TextWrapping == UiTextWrapping.Wrap && availableWidth > 0f
            ? availableWidth
            : float.PositiveInfinity;
        float y = 0f;
        float resolvedLineHeight = ResolveLineHeight(context, font);
        var lines = new List<TextBoxLine>();

        if (Text.Length == 0)
        {
            lines.Add(CreateLine(0, 0, y, context, font));
            return lines.ToArray();
        }

        int logicalStart = 0;
        for (int index = 0; index <= Text.Length; index++)
        {
            bool atEnd = index == Text.Length;
            bool atBreak = !atEnd && IsLineBreak(Text[index]);
            if (!atEnd && !atBreak)
            {
                continue;
            }

            AddWrappedLines(lines, logicalStart, index, wrapWidth, ref y, resolvedLineHeight, context, font);
            if (atBreak)
            {
                bool isCarriageReturnPair = Text[index] == '\r' && index + 1 < Text.Length && Text[index + 1] == '\n';
                if (isCarriageReturnPair)
                {
                    index++;
                }

                logicalStart = index + 1;
                if (logicalStart == Text.Length)
                {
                    lines.Add(CreateLine(Text.Length, Text.Length, y, context, font));
                    y += resolvedLineHeight;
                }
            }
        }

        return lines.Count == 0 ? [CreateLine(0, 0, 0f, context, font)] : lines.ToArray();
    }

    private void AddWrappedLines(
        List<TextBoxLine> lines,
        int start,
        int end,
        float wrapWidth,
        ref float y,
        float resolvedLineHeight,
        UiRenderContext? context,
        FontHandle? font)
    {
        if (start == end)
        {
            lines.Add(CreateLine(start, end, y, context, font));
            y += resolvedLineHeight;
            return;
        }

        int lineStart = start;
        while (lineStart < end)
        {
            int lineEnd = end;
            if (float.IsFinite(wrapWidth))
            {
                lineEnd = FindWrappedLineEnd(lineStart, end, wrapWidth, context, font);
            }

            lines.Add(CreateLine(lineStart, lineEnd, y, context, font));
            y += resolvedLineHeight;
            lineStart = Math.Max(lineEnd, lineStart + 1);
        }
    }

    private int FindWrappedLineEnd(int start, int end, float wrapWidth, UiRenderContext? context, FontHandle? font)
    {
        int best = start;
        for (int index = start + 1; index <= end; index++)
        {
            int boundary = CoerceTextBoundaryAtOrBefore(Text, index);
            if (boundary != index)
            {
                continue;
            }

            float width = MeasureTextRange(start, index, context, font);
            if (width > wrapWidth && best > start)
            {
                return best;
            }

            best = index;
        }

        return Math.Max(best, start + 1);
    }

    private TextBoxLine CreateLine(int start, int end, float y, UiRenderContext? context, FontHandle? font)
    {
        int length = Math.Max(0, end - start);
        float[] advances = new float[length + 1];
        for (int offset = 1; offset <= length; offset++)
        {
            int absolute = start + offset;
            advances[offset] = CoerceTextBoundaryAtOrBefore(Text, absolute) == absolute
                ? MeasureTextRange(start, absolute, context, font)
                : advances[offset - 1];
        }

        return new TextBoxLine(start, end, y, advances);
    }

    private float MeasureTextRange(int start, int end, UiRenderContext? context, FontHandle? font)
    {
        int length = Math.Max(0, end - start);
        if (length == 0)
        {
            return 0f;
        }

        string value = Text.Substring(start, length);
        return context is not null && font is not null
            ? context.Draw.Measure.Text(value, font).Width
            : Root is { } root && root.TryMeasureText(value, font ?? root.ThemeResources.Font, out SizeF measured)
                ? measured.Width
                : length * CharacterWidth(ResolveFontSize());
    }

    private float ResolveLineHeight(UiRenderContext? context, FontHandle? font)
        => context is not null && font is not null
            ? context.Draw.Measure.Text("M", font).Height * LineSpacing
            : ResolveFontSize() * LineSpacing;

    private void RenderSelection(UiRenderContext context, RectF content)
    {
        int selectionEnd = SelectionStart + SelectionLength;
        foreach (TextBoxLine line in textLines)
        {
            float y = content.Y + line.Y - verticalOffset;
            if (y + lineHeight < content.Y || y > content.Y + content.Height)
            {
                continue;
            }

            int start = Math.Max(SelectionStart, line.Start);
            int end = Math.Min(selectionEnd, line.End);
            if (start >= end)
            {
                continue;
            }

            float selectionX = content.X + line.AdvanceAt(start) - horizontalOffset;
            float selectionWidth = line.AdvanceAt(end) - line.AdvanceAt(start);
            RectF selection = new(
                MathF.Max(content.X, selectionX),
                y,
                MathF.Min(content.X + content.Width, selectionX + selectionWidth) - MathF.Max(content.X, selectionX),
                lineHeight);
            if (!selection.IsEmpty)
            {
                context.Draw.Fill.Rectangle(selection, HoverBackground ?? context.Theme.SurfaceHover);
            }
        }
    }

    private void RenderTextLines(UiRenderContext context, RectF content, FontHandle font, BrushHandle brush)
    {
        foreach (TextBoxLine line in textLines)
        {
            float y = content.Y + line.Y - verticalOffset;
            if (y + lineHeight < content.Y || y > content.Y + content.Height || line.Start == line.End)
            {
                continue;
            }

            string lineText = Text[line.Start..line.End];
            context.Draw.Draw.Text(lineText, font, brush, new PointF(content.X - horizontalOffset, y));
        }
    }

    private TextBoxCaret CaretAt(int index)
    {
        int lineIndex = LineIndexForCaret(index);
        TextBoxLine line = textLines[lineIndex];
        return new TextBoxCaret(line.AdvanceAt(index), line.Y);
    }

    private int LineIndexForCaret(int index)
    {
        int coerced = CoerceCaretIndex(index);
        for (int lineIndex = 0; lineIndex < textLines.Length; lineIndex++)
        {
            TextBoxLine line = textLines[lineIndex];
            if (coerced >= line.Start && coerced <= line.End)
            {
                return lineIndex;
            }
        }

        return textLines.Length - 1;
    }

    private int IndexFromLineAdvance(TextBoxLine line, float advance)
    {
        int bestOffset = 0;
        float bestDistance = MathF.Abs(advance);
        for (int offset = 1; offset < line.Advances.Length; offset++)
        {
            int index = line.Start + offset;
            if (CoerceTextBoundaryAtOrBefore(Text, index) != index)
            {
                continue;
            }

            float distance = MathF.Abs(line.Advances[offset] - advance);
            if (distance <= bestDistance)
            {
                bestOffset = offset;
                bestDistance = distance;
            }
        }

        return line.Start + bestOffset;
    }

    private int HomeIndex()
    {
        if (Mode == TextBoxMode.SingleLine)
        {
            return 0;
        }

        UpdateTextLayout(context: null, font: null);
        return textLines[LineIndexForCaret(CaretIndex)].Start;
    }

    private int EndIndex()
    {
        if (Mode == TextBoxMode.SingleLine)
        {
            return Text.Length;
        }

        UpdateTextLayout(context: null, font: null);
        return textLines[LineIndexForCaret(CaretIndex)].End;
    }

    private string ClampTextToMaxLength(string value)
    {
        if (value.Length <= MaxLength)
        {
            return value;
        }

        int boundary = SafeTextBoundaryAtOrBefore(value, MaxLength);
        return boundary == value.Length ? value : value[..boundary];
    }

    private int CoerceCaretIndex(int value)
        => CoerceTextBoundaryAtOrBefore(Text, Math.Clamp(value, 0, Text.Length));

    private int CoerceSelectionLength(int start, int length)
    {
        int requestedEnd = length > Text.Length - start ? Text.Length : start + Math.Max(0, length);
        int end = CoerceTextBoundaryAtOrBefore(Text, requestedEnd);
        return end - start;
    }

    private int PreviousTextBoundary(int index)
    {
        int current = CoerceCaretIndex(index);
        if (current <= 0)
        {
            return 0;
        }

        int previous = current - 1;
        return CoerceTextBoundaryAtOrBefore(Text, previous);
    }

    private int NextTextBoundary(int index)
    {
        int current = CoerceCaretIndex(index);
        if (current >= Text.Length)
        {
            return Text.Length;
        }

        int next = current + 1;
        if (next < Text.Length && char.IsLowSurrogate(Text[next]) && char.IsHighSurrogate(Text[next - 1]))
        {
            next++;
        }

        return next;
    }

    private static int SafeTextBoundaryAtOrBefore(string value, int index)
    {
        int boundary = Math.Clamp(index, 0, value.Length);
        if (boundary > 0
            && boundary < value.Length
            && char.IsHighSurrogate(value[boundary - 1])
            && char.IsLowSurrogate(value[boundary]))
        {
            boundary--;
        }

        return boundary;
    }

    private static int CoerceTextBoundaryAtOrBefore(string value, int index)
        => SafeTextBoundaryAtOrBefore(value, index);

    private void ClearSelection()
    {
        selectionAnchor = CaretIndex;
        selectionStart = CaretIndex;
        selectionLength = 0;
        InvalidateRender();
    }

    private static bool IsLineBreak(char value) => value is '\r' or '\n';

    private static bool IsOnlyReturnInput(string value)
        => value is "\r" or "\n" or "\r\n";

    private sealed class TextBoxLine
    {
        public TextBoxLine(int start, int end, float y, float[] advances)
        {
            Start = start;
            End = end;
            Y = y;
            Advances = advances;
            Width = advances.Length == 0 ? 0f : advances[^1];
        }

        public int Start { get; }

        public int End { get; }

        public float Y { get; }

        public float Width { get; }

        public float[] Advances { get; }

        public float AdvanceAt(int index)
        {
            int offset = Math.Clamp(index - Start, 0, Advances.Length - 1);
            return Advances[offset];
        }
    }

    private readonly record struct TextBoxCaret(float X, float Y);
}

/// <summary>
/// Displays selectable items in a vertical list.
/// </summary>
public sealed class ListBox : Selector
{
    private const float TitleHeight = 24f;
    private float itemHeight = 24f;
    private string title = string.Empty;

    /// <summary>
    /// Initializes a list box.
    /// </summary>
    public ListBox()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 140f;
        MinHeight = 72f;
        Height = 120f;
    }

    /// <summary>
    /// Gets or sets the row height in DIPs. Hit testing and keyboard movement use the same uniform row height.
    /// </summary>
    public float ItemHeight
    {
        get => itemHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref itemHeight, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
        }
    }

    /// <summary>
    /// Gets or sets the optional title rendered above the selectable item rows. The title is not selectable and reduces the visible item area.
    /// </summary>
    public string Title
    {
        get => title;
        set => SetProperty(ref title, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
        => new(
            MathF.Min(availableSize.Width, MathF.Max(MinWidth, 160f)),
            MathF.Min(availableSize.Height, MathF.Max(MinHeight, Items.Count * ItemHeight + Padding.Vertical + HeaderHeight)));

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        RectF content = ItemsBounds;
        RenderTitle(context, enabled);

        int visibleCount = UiGeometry.VisibleUniformBandCount(content.Height, ItemHeight, Items.Count);
        for (int index = 0; index < visibleCount; index++)
        {
            RectF row = new(content.X, content.Y + index * ItemHeight, content.Width, ItemHeight);
            bool itemEnabled = IsItemEnabled(index);
            if (index == SelectedIndex)
            {
                context.Draw.Fill.Rectangle(row, enabled ? ResolveAccentBrush(context) : ResolveBorderBrush(context));
            }

            BrushHandle itemBrush = enabled && itemEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context);
            context.Draw.Draw.Text(GetItemText(index), context.Theme.Font, itemBrush, new PointF(row.X + 6f, row.Y + 4f));
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        Focus();
        RectF content = ItemsBounds;
        int visibleCount = UiGeometry.VisibleUniformBandCount(content.Height, ItemHeight, Items.Count);
        int index = UiGeometry.UniformBandIndex(args.Position.Y, content.Y, ItemHeight, visibleCount);
        if (IsItemEnabled(index))
        {
            SelectedIndex = index;
            args.Handled = true;
        }
    }

    protected override void OnPointerWheel(UiPointerEventArgs args)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int direction = args.WheelDelta > 0 ? -1 : 1;
        int next = FindEnabledIndex(SelectedIndex, direction);
        if (next >= 0)
        {
            SelectedIndex = next;
        }

        args.Handled = true;
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        if (Items.Count == 0)
        {
            return;
        }

        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Up:
                MoveSelection(-1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Down:
                MoveSelection(1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectedIndex = FirstEnabledIndex();
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectedIndex = LastEnabledIndex();
                args.Handled = true;
                break;
        }
    }

    private bool HasTitle => Title.Length > 0;

    private float HeaderHeight => HasTitle ? TitleHeight : 0f;

    private RectF TitleBounds => HasTitle ? new RectF(ContentBounds.X, ContentBounds.Y, ContentBounds.Width, TitleHeight) : new RectF(0f, 0f, 0f, 0f);

    private RectF ItemsBounds
    {
        get
        {
            RectF content = ContentBounds;
            float headerHeight = HeaderHeight;
            return new RectF(content.X, content.Y + headerHeight, content.Width, MathF.Max(0f, content.Height - headerHeight));
        }
    }

    private void RenderTitle(UiRenderContext context, bool enabled)
    {
        if (!HasTitle)
        {
            return;
        }

        RectF titleBounds = TitleBounds;
        context.Draw.Fill.Rectangle(titleBounds, enabled ? context.Theme.SurfaceHover : ResolveDisabledBrush(context));
        context.Draw.Draw.Line(
            new PointF(titleBounds.X, titleBounds.Y + titleBounds.Height),
            new PointF(titleBounds.X + titleBounds.Width, titleBounds.Y + titleBounds.Height),
            ResolveBorderBrush(context));
        context.Draw.Draw.Text(Title, context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(titleBounds.X + 6f, titleBounds.Y + 4f));
    }
}

/// <summary>
/// Displays a selected value and an expandable popup list.
/// </summary>
public sealed class ComboBox : Selector, IUiPopup
{
    private bool isDropDownOpen;
    private string placeholder = string.Empty;
    private readonly float itemHeight = 24f;
    private float maxDropDownHeight = 160f;
    private bool clampDropDownToOverlay = true;
    private RectF headerBounds;
    private int hoveredDropDownIndex = -1;

    /// <summary>
    /// Initializes a combo box.
    /// </summary>
    public ComboBox()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 140f;
        Height = 30f;
        Padding = new Thickness(8f, 5f);
        ZIndex = (int)UiLayer.Popup;
    }

    /// <summary>
    /// Gets or sets placeholder text displayed when no item is selected.
    /// </summary>
    public string Placeholder
    {
        get => placeholder;
        set => SetProperty(ref placeholder, value ?? string.Empty, UiInvalidation.Render);
    }

    /// <summary>
    /// Gets or sets whether the dropdown list is open. Opening expands the input region to include the dropdown rows.
    /// </summary>
    public bool IsDropDownOpen
    {
        get => isDropDownOpen;
        set
        {
            if (SetProperty(ref isDropDownOpen, value, UiInvalidation.Arrange | UiInvalidation.Render | UiInvalidation.InputRegion) && !value)
            {
                hoveredDropDownIndex = -1;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether pointer input outside the header or dropdown closes the dropdown.
    /// </summary>
    public bool DismissOnOutsidePointer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Escape closes the dropdown.
    /// </summary>
    public bool DismissOnEscape { get; set; } = true;

    bool IUiPopup.IsPopupOpen => IsDropDownOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => this;

    /// <summary>
    /// Gets or sets the maximum dropdown height in DIPs. Items beyond the visible height are not virtualized or scrolled in this MVP control.
    /// </summary>
    public float MaxDropDownHeight
    {
        get => maxDropDownHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref maxDropDownHeight, value, UiInvalidation.Render | UiInvalidation.InputRegion);
        }
    }

    /// <summary>
    /// Gets or sets whether the dropdown is clamped to overlay bounds and may open above the header when there is not enough space below.
    /// </summary>
    public bool ClampDropDownToOverlay
    {
        get => clampDropDownToOverlay;
        set => SetProperty(ref clampDropDownToOverlay, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize) => new(MathF.Min(availableSize.Width, MathF.Max(MinWidth, 160f)), Height);

    protected override void ArrangeCore(RectF finalRect)
    {
        headerBounds = finalRect;
        if (IsDropDownOpen)
        {
            RectF dropDown = DropDownBounds;
            float left = MathF.Min(headerBounds.X, dropDown.X);
            float top = MathF.Min(headerBounds.Y, dropDown.Y);
            float right = MathF.Max(headerBounds.X + headerBounds.Width, dropDown.X + dropDown.Width);
            float bottom = MathF.Max(headerBounds.Y + headerBounds.Height, dropDown.Y + dropDown.Height);
            SetLayoutBounds(new RectF(left, top, right - left, bottom - top));
        }
    }

    private protected override UiInvalidation SelectionInvalidation => UiInvalidation.Render | UiInvalidation.InputRegion;

    protected override bool HitTestCore(PointF point)
        => UiGeometry.Contains(HeaderBounds, point) || (IsDropDownOpen && UiGeometry.Contains(DropDownBounds, point));

    /// <summary>
    /// Determines whether a point is within the combo box or its open dropdown.
    /// </summary>
    /// <param name="point">The overlay-local point in DIPs.</param>
    /// <returns><see langword="true"/> when the point is within the combo box popup region.</returns>
    public bool ContainsPopupPoint(PointF point) => HitTestCore(point);

    void IUiPopup.DismissPopup(UiPopupDismissReason reason) => IsDropDownOpen = false;

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        RectF header = HeaderBounds;
        RectF headerContent = UiGeometry.Deflate(header, Padding);
        context.Draw.Fill.RoundedRectangle(header, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(header, 4f, 4f, IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        string display = IsSelectedIndexValid ? SelectedText : Placeholder;
        if (display.Length > 0)
        {
            BrushHandle brush = !enabled ? ResolveDisabledBrush(context) : IsSelectedIndexValid ? ResolveForeground(context) : context.Theme.MutedForeground;
            context.Draw.Draw.Text(display, context.Theme.Font, brush, new PointF(headerContent.X, headerContent.Y));
        }

        PointF arrowA = new(header.X + header.Width - 20f, header.Y + 12f);
        PointF arrowB = new(header.X + header.Width - 14f, header.Y + 18f);
        PointF arrowC = new(header.X + header.Width - 8f, header.Y + 12f);
        BrushHandle arrowBrush = enabled ? ResolveForeground(context) : ResolveDisabledBrush(context);
        context.Draw.Draw.Line(arrowA, arrowB, arrowBrush);
        context.Draw.Draw.Line(arrowB, arrowC, arrowBrush);

        if (IsDropDownOpen)
        {
            RenderDropDown(context);
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        Focus();
        if (IsDropDownOpen && UiGeometry.Contains(DropDownBounds, args.Position))
        {
            RectF dropDown = DropDownBounds;
            int visibleCount = UiGeometry.VisibleUniformBandCount(dropDown.Height, itemHeight, Items.Count);
            int index = UiGeometry.UniformBandIndex(args.Position.Y, dropDown.Y, itemHeight, visibleCount);
            if (IsItemEnabled(index))
            {
                SelectedIndex = index;
            }

            IsDropDownOpen = false;
            args.Handled = true;
            return;
        }

        IsDropDownOpen = !IsDropDownOpen;
        args.Handled = true;
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        if (!IsDropDownOpen)
        {
            return;
        }

        int next = DropDownIndexAt(args.Position);
        if (hoveredDropDownIndex != next)
        {
            hoveredDropDownIndex = next;
            InvalidateRender();
        }

        if (next >= 0)
        {
            args.Handled = true;
        }
    }

    protected override void OnKeyPressed(UiKeyboardEventArgs args)
    {
        switch (args.VirtualKey)
        {
            case UiVirtualKeys.Enter:
            case UiVirtualKeys.Space:
                IsDropDownOpen = !IsDropDownOpen;
                args.Handled = true;
                break;
            case UiVirtualKeys.Escape:
                IsDropDownOpen = false;
                args.Handled = true;
                break;
            case UiVirtualKeys.Down:
                MoveSelection(1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Up:
                MoveSelection(-1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectedIndex = FirstEnabledIndex();
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectedIndex = LastEnabledIndex();
                args.Handled = true;
                break;
        }
    }

    private RectF DropDownBounds
    {
        get
        {
            float height = MathF.Min(MaxDropDownHeight, Items.Count * itemHeight);
            RectF header = HeaderBounds;
            SizeF size = new(header.Width, height);
            return ClampDropDownToOverlay
                ? UiPopupPlacement.ResolveBelowOrAbove(Root, header, size)
                : new RectF(header.X, header.Y + header.Height + 2f, size.Width, size.Height);
        }
    }

    private RectF HeaderBounds => headerBounds.IsEmpty ? Bounds : headerBounds;

    private void RenderDropDown(UiRenderContext context)
    {
        RectF dropdown = DropDownBounds;
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(dropdown, 4f, 4f, enabled ? ResolvePopupBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(dropdown, 4f, 4f, ResolveBorderBrush(context));
        int visibleCount = UiGeometry.VisibleUniformBandCount(dropdown.Height, itemHeight, Items.Count);
        for (int index = 0; index < visibleCount; index++)
        {
            RectF row = new(dropdown.X, dropdown.Y + index * itemHeight, dropdown.Width, itemHeight);
            bool itemEnabled = IsItemEnabled(index);
            if (index == SelectedIndex)
            {
                context.Draw.Fill.Rectangle(row, enabled ? ResolveAccentBrush(context) : ResolveBorderBrush(context));
            }
            else if (index == hoveredDropDownIndex && itemEnabled)
            {
                context.Draw.Fill.Rectangle(row, enabled ? context.Theme.SurfaceHover : ResolveBorderBrush(context));
            }

            BrushHandle itemBrush = enabled && itemEnabled ? ResolveForeground(context) : ResolveDisabledBrush(context);
            string itemText = GetItemText(index);
            PointF origin = new(row.X + 8f, row.Y + 4f);
            context.Draw.Draw.Text(itemText, context.Theme.Font, itemBrush, origin);
            if (index == hoveredDropDownIndex && itemEnabled && enabled)
            {
                context.Draw.Draw.Text(itemText, context.Theme.Font, itemBrush, new PointF(origin.X + 0.75f, origin.Y));
            }
        }
    }

    private int DropDownIndexAt(PointF point)
    {
        if (!UiGeometry.Contains(DropDownBounds, point))
        {
            return -1;
        }

        RectF dropDown = DropDownBounds;
        int visibleCount = UiGeometry.VisibleUniformBandCount(dropDown.Height, itemHeight, Items.Count);
        int index = UiGeometry.UniformBandIndex(point.Y, dropDown.Y, itemHeight, visibleCount);
        return IsItemEnabled(index) ? index : -1;
    }
}
