namespace ModernOverlay.UI;

public class TextBlock : UiElement
{
    private string text = string.Empty;
    private FontHandle? font;
    private UiHorizontalAlignment textAlignment = UiHorizontalAlignment.Left;
    private UiTextWrapping textWrapping = UiTextWrapping.NoWrap;
    private UiTextTrimming textTrimming = UiTextTrimming.None;
    private int maxLines = int.MaxValue;
    private float lineSpacing = 1.35f;

    public TextBlock()
    {
        ReceivesInput = false;
    }

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public FontHandle? Font
    {
        get => font;
        set => SetProperty(ref font, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.Resource);
    }

    public UiHorizontalAlignment TextAlignment
    {
        get => textAlignment;
        set => SetProperty(ref textAlignment, value, UiInvalidation.Render);
    }

    public UiTextWrapping TextWrapping
    {
        get => textWrapping;
        set => SetProperty(ref textWrapping, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public UiTextTrimming TextTrimming
    {
        get => textTrimming;
        set => SetProperty(ref textTrimming, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public int MaxLines
    {
        get => maxLines;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref maxLines, value, UiInvalidation.Measure | UiInvalidation.Render);
        }
    }

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
        string[] lines = BuildLines(content.Width, charWidth);
        float lineHeight = context.Draw.Measure.Text("M", Font ?? context.Theme.Font).Height * LineSpacing;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            float lineWidth = context.Draw.Measure.Text(line, Font ?? context.Theme.Font).Width;
            float x = TextAlignment switch
            {
                UiHorizontalAlignment.Center => content.X + MathF.Max(0f, content.Width - lineWidth) / 2f,
                UiHorizontalAlignment.Right => content.X + MathF.Max(0f, content.Width - lineWidth),
                _ => content.X,
            };
            context.Draw.Draw.Text(line, Font ?? context.Theme.Font, brush, new PointF(x, content.Y + index * lineHeight));
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
        => Font?.Options.Size ?? (theme ?? Root?.ThemeResources.Theme ?? UiTheme.Default).FontSize;

    private SizeF MeasureText(string value, float resolvedFontSize)
        => Root?.TryMeasureText(value, Font, out SizeF measured) == true
            ? measured
            : new SizeF(value.Length * CharacterWidth(resolvedFontSize), resolvedFontSize);

    private static float CharacterWidth(float fontSize) => MathF.Max(1f, fontSize * 0.56f);
}

public sealed class Label : TextBlock
{
    private UiElement? target;

    public Label()
    {
        ReceivesInput = true;
    }

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

    public Image()
    {
        ReceivesInput = false;
        MinWidth = 0f;
        MinHeight = 0f;
    }

    public ImageHandle? Source
    {
        get => source;
        set => SetProperty(ref source, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

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

    public int FrameIndex
    {
        get => frameIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SetProperty(ref frameIndex, value, UiInvalidation.Render);
        }
    }

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

    public UiImageStretch Stretch
    {
        get => stretch;
        set => SetProperty(ref stretch, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public ImageInterpolationMode InterpolationMode
    {
        get => interpolationMode;
        set => SetProperty(ref interpolationMode, value, UiInvalidation.Render);
    }

    public UiHorizontalAlignment ImageHorizontalAlignment
    {
        get => imageHorizontalAlignment;
        set => SetProperty(ref imageHorizontalAlignment, value, UiInvalidation.Render);
    }

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

public class Button : ContentControl
{
    private string text = string.Empty;
    private UiCommand? command;
    private object? commandParameter;

    public Button()
    {
        ReceivesInput = true;
        Focusable = true;
        Padding = new Thickness(10f, 6f);
        MinHeight = 28f;
    }

    public event EventHandler<UiClickEventArgs>? Click;

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public UiCommand? Command
    {
        get => command;
        set
        {
            if (command == value)
            {
                return;
            }

            command?.CanExecuteChanged -= HandleCanExecuteChanged;

            command = value;
            command?.CanExecuteChanged += HandleCanExecuteChanged;

            InvalidateRender();
        }
    }

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
            context.Draw.Draw.Text(Text, context.Theme.Font, CanExecute() ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(content.X, content.Y));
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
}

public class ToggleButton : Button
{
    private bool isChecked;

    public event EventHandler? CheckedChanged;

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (SetProperty(ref isChecked, value, UiInvalidation.Render))
            {
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected override void InvokeClick(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        IsChecked = !IsChecked;
        base.InvokeClick(position, button, clickCount);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        base.RenderCore(context);
        if (IsChecked)
        {
            RectF mark = UiGeometry.Deflate(Bounds, new Thickness(4f));
            context.Draw.Draw.RoundedRectangle(mark, 3f, 3f, ResolveAccentBrush(context), 2f);
        }
    }
}

public sealed class CheckBox : ToggleButton
{
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
        if (IsChecked)
        {
            context.Draw.Fill.Rectangle(UiGeometry.Deflate(box, new Thickness(3f)), stateBrush);
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

public sealed class RadioButton : ToggleButton
{
    private string? groupName;

    public RadioButton()
    {
        Padding = new Thickness(28f, 5f, 8f, 5f);
        MinHeight = 26f;
    }

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

public abstract class RangeBase : UiControl
{
    private float minimum;
    private float maximum = 100f;
    private float value;
    private float smallChange = 1f;
    private float largeChange = 10f;

    public event EventHandler? ValueChanged;

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

    public float Maximum
    {
        get => maximum;
        set
        {
            SetProperty(ref maximum, MathF.Max(value, minimum), UiInvalidation.Render);
            Value = this.value;
        }
    }

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

public sealed class ProgressBar : RangeBase
{
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

public sealed class Slider : RangeBase
{
    private UiOrientation orientation = UiOrientation.Horizontal;

    public Slider()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 120f;
        MinHeight = 22f;
        Height = 22f;
    }

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
        RectF track = Orientation == UiOrientation.Horizontal
            ? new RectF(Bounds.X, Bounds.Y + Bounds.Height / 2f - 2f, Bounds.Width, 4f)
            : new RectF(Bounds.X + Bounds.Width / 2f - 2f, Bounds.Y, 4f, Bounds.Height);
        bool enabled = IsEffectivelyEnabled;
        if (IsFocused && enabled)
        {
            context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, ResolveFocusBrush(context));
        }

        context.Draw.Fill.RoundedRectangle(track, 2f, 2f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(track, 2f, 2f, ResolveBorderBrush(context));

        float ratio = ValueRatio;
        PointF center = Orientation == UiOrientation.Horizontal
            ? new PointF(Bounds.X + Bounds.Width * ratio, Bounds.Y + Bounds.Height / 2f)
            : new PointF(Bounds.X + Bounds.Width / 2f, Bounds.Y + Bounds.Height * (1f - ratio));
        context.Draw.Fill.Circle(center, 7f, !enabled ? ResolveDisabledBrush(context) : IsPointerCaptured ? PressedBackground ?? context.Theme.SurfacePressed : ResolveAccentBrush(context));
        context.Draw.Draw.Circle(center, 7f, IsFocused && enabled ? ResolveForeground(context) : ResolveBorderBrush(context));
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
        float ratio = Orientation == UiOrientation.Horizontal
            ? (point.X - Bounds.X) / MathF.Max(1f, Bounds.Width)
            : 1f - (point.Y - Bounds.Y) / MathF.Max(1f, Bounds.Height);
        Value = Minimum + (Maximum - Minimum) * Math.Clamp(ratio, 0f, 1f);
    }
}

public sealed class TextBox : UiControl
{
    private string text = string.Empty;
    private string placeholder = string.Empty;
    private int caretIndex;
    private int selectionStart;
    private int selectionLength;
    private int maxLength = int.MaxValue;
    private bool isReadOnly;
    private bool selecting;
    private int selectionAnchor;
    private float horizontalOffset;

    public TextBox()
    {
        ReceivesInput = true;
        Focusable = true;
        Padding = new Thickness(8f, 5f);
        MinWidth = 120f;
        MinHeight = 28f;
        Height = 30f;
    }

    public event EventHandler? TextChanged;

    public string Text
    {
        get => text;
        set
        {
            string next = value ?? string.Empty;
            if (next.Length > MaxLength)
            {
                next = next[..MaxLength];
            }

            if (SetProperty(ref text, next, UiInvalidation.Measure | UiInvalidation.Render))
            {
                CaretIndex = Math.Min(CaretIndex, text.Length);
                selectionStart = Math.Min(selectionStart, text.Length);
                selectionLength = Math.Min(selectionLength, text.Length - selectionStart);
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Placeholder
    {
        get => placeholder;
        set => SetProperty(ref placeholder, value ?? string.Empty, UiInvalidation.Render);
    }

    public int CaretIndex
    {
        get => caretIndex;
        set
        {
            if (SetProperty(ref caretIndex, Math.Clamp(value, 0, Text.Length), UiInvalidation.Render))
            {
                EnsureCaretVisible();
                Root?.RestartCaretBlink();
            }
        }
    }

    public int SelectionStart
    {
        get => selectionStart;
        set
        {
            selectionStart = Math.Clamp(value, 0, Text.Length);
            selectionLength = Math.Min(selectionLength, Text.Length - selectionStart);
            InvalidateRender();
        }
    }

    public int SelectionLength
    {
        get => selectionLength;
        set
        {
            selectionLength = Math.Clamp(value, 0, Text.Length - SelectionStart);
            InvalidateRender();
        }
    }

    public int MaxLength
    {
        get => maxLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (SetProperty(ref maxLength, value, UiInvalidation.None) && Text.Length > value)
            {
                Text = Text[..value];
            }
        }
    }

    public bool IsReadOnly
    {
        get => isReadOnly;
        set => SetProperty(ref isReadOnly, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        return new SizeF(MathF.Min(availableSize.Width, MathF.Max(MinWidth, Text.Length * fontSize * 0.56f + Padding.Horizontal)), MathF.Max(MinHeight, fontSize * 1.35f + Padding.Vertical));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        BrushHandle border = IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context);
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, border);

        RectF content = ContentBounds;
        float fontSize = context.Theme.Theme.FontSize;
        float charWidth = CharacterWidth(fontSize);
        if (SelectionLength > 0 && Text.Length > 0)
        {
            float selectionX = content.X + SelectionStart * charWidth - horizontalOffset;
            float selectionWidth = SelectionLength * charWidth;
            RectF selection = new(
                MathF.Max(content.X, selectionX),
                content.Y,
                MathF.Min(content.X + content.Width, selectionX + selectionWidth) - MathF.Max(content.X, selectionX),
                fontSize * 1.35f);
            if (!selection.IsEmpty)
            {
                context.Draw.Fill.Rectangle(selection, HoverBackground ?? context.Theme.SurfaceHover);
            }
        }

        string displayText = Text.Length == 0 ? Placeholder : Text;
        BrushHandle textBrush = !enabled ? ResolveDisabledBrush(context) : Text.Length == 0 ? context.Theme.MutedForeground : ResolveForeground(context);
        if (displayText.Length > 0)
        {
            float offset = Text.Length == 0 ? 0f : horizontalOffset;
            context.Draw.Draw.Text(displayText, context.Theme.Font, IsReadOnly || !enabled ? ResolveDisabledBrush(context) : textBrush, new PointF(content.X - offset, content.Y));
        }

        if (enabled && IsFocused && !IsReadOnly && (Root?.IsCaretVisible ?? true))
        {
            float caretX = content.X + CaretIndex * charWidth - horizontalOffset;
            context.Draw.Draw.Line(new PointF(caretX, content.Y), new PointF(caretX, content.Y + fontSize * 1.3f), ResolveAccentBrush(context));
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

        string filtered = new(args.Text.Where(character => !char.IsControl(character)).ToArray());
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
                MoveCaret(Math.Max(0, CaretIndex - 1), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Right:
                MoveCaret(Math.Min(Text.Length, CaretIndex + 1), shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                MoveCaret(0, shift);
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                MoveCaret(Text.Length, shift);
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

        string insert = value.Length > available ? value[..available] : value;
        Text = Text.Insert(CaretIndex, insert);
        CaretIndex += insert.Length;
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

        Text = Text.Remove(CaretIndex - 1, 1);
        CaretIndex--;
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

        Text = Text.Remove(CaretIndex, 1);
    }

    private bool DeleteSelection()
    {
        if (SelectionLength <= 0)
        {
            return false;
        }

        Text = Text.Remove(SelectionStart, SelectionLength);
        CaretIndex = SelectionStart;
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
            SelectFromAnchor(selectionAnchor, CaretIndex);
            return;
        }

        CaretIndex = nextIndex;
        ClearSelection();
    }

    private int CaretIndexFromPoint(PointF point)
    {
        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        float charWidth = CharacterWidth(fontSize);
        return (int)Math.Clamp(MathF.Round((point.X - ContentBounds.X + horizontalOffset) / charWidth), 0, Text.Length);
    }

    private void SelectFromAnchor(int anchor, int caret)
    {
        selectionStart = Math.Min(anchor, caret);
        selectionLength = Math.Abs(caret - anchor);
        InvalidateRender();
    }

    private void EnsureCaretVisible()
    {
        float contentWidth = ContentBounds.Width;
        if (contentWidth <= 0f)
        {
            return;
        }

        float fontSize = Root?.ThemeResources.Theme.FontSize ?? UiTheme.Default.FontSize;
        float charWidth = CharacterWidth(fontSize);
        float caretX = CaretIndex * charWidth;
        if (caretX < horizontalOffset)
        {
            horizontalOffset = caretX;
        }
        else if (caretX > horizontalOffset + contentWidth)
        {
            horizontalOffset = caretX - contentWidth;
        }

        float maxOffset = MathF.Max(0f, Text.Length * charWidth - contentWidth);
        horizontalOffset = Math.Clamp(horizontalOffset, 0f, maxOffset);
    }

    private static float CharacterWidth(float fontSize) => MathF.Max(1f, fontSize * 0.56f);

    private void ClearSelection()
    {
        selectionAnchor = CaretIndex;
        selectionStart = CaretIndex;
        selectionLength = 0;
        InvalidateRender();
    }
}

public sealed class ListBox : Selector
{
    private float itemHeight = 24f;

    public ListBox()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 140f;
        MinHeight = 72f;
        Height = 120f;
    }

    public float ItemHeight
    {
        get => itemHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref itemHeight, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion);
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
        => new(MathF.Min(availableSize.Width, MathF.Max(MinWidth, 160f)), MathF.Min(availableSize.Height, MathF.Max(MinHeight, Items.Count * ItemHeight + Padding.Vertical)));

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        RectF content = ContentBounds;
        int visibleCount = Math.Min(Items.Count, (int)MathF.Floor(content.Height / ItemHeight));
        for (int index = 0; index < visibleCount; index++)
        {
            RectF row = new(content.X, content.Y + index * ItemHeight, content.Width, ItemHeight);
            if (index == SelectedIndex)
            {
                context.Draw.Fill.Rectangle(row, enabled ? ResolveAccentBrush(context) : ResolveBorderBrush(context));
            }

            context.Draw.Draw.Text(GetItemText(index), context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(row.X + 6f, row.Y + 4f));
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        Focus();
        int index = (int)((args.Position.Y - ContentBounds.Y) / ItemHeight);
        if (index >= 0 && index < Items.Count)
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
        SelectedIndex = Math.Clamp(SelectedIndex < 0 ? 0 : SelectedIndex + direction, 0, Items.Count - 1);
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
                SelectedIndex = Math.Clamp(SelectedIndex - 1, 0, Items.Count - 1);
                args.Handled = true;
                break;
            case UiVirtualKeys.Down:
                SelectedIndex = Math.Clamp(SelectedIndex + 1, 0, Items.Count - 1);
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
}

public sealed class ComboBox : Selector, IUiPopup
{
    private bool isDropDownOpen;
    private string placeholder = string.Empty;
    private readonly float itemHeight = 24f;
    private float maxDropDownHeight = 160f;
    private bool clampDropDownToOverlay = true;

    public ComboBox()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 140f;
        Height = 30f;
        Padding = new Thickness(8f, 5f);
        ZIndex = (int)UiLayer.Popup;
    }

    public string Placeholder
    {
        get => placeholder;
        set => SetProperty(ref placeholder, value ?? string.Empty, UiInvalidation.Render);
    }

    public bool IsDropDownOpen
    {
        get => isDropDownOpen;
        set => SetProperty(ref isDropDownOpen, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    public bool DismissOnOutsidePointer { get; set; } = true;

    public bool DismissOnEscape { get; set; } = true;

    bool IUiPopup.IsPopupOpen => IsDropDownOpen;

    UiElement IUiPopup.PopupElement => this;

    UiElement? IUiPopup.PopupOwner => this;

    public float MaxDropDownHeight
    {
        get => maxDropDownHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SetProperty(ref maxDropDownHeight, value, UiInvalidation.Render | UiInvalidation.InputRegion);
        }
    }

    public bool ClampDropDownToOverlay
    {
        get => clampDropDownToOverlay;
        set => SetProperty(ref clampDropDownToOverlay, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    protected override SizeF MeasureCore(SizeF availableSize) => new(MathF.Min(availableSize.Width, MathF.Max(MinWidth, 160f)), Height);

    private protected override UiInvalidation SelectionInvalidation => UiInvalidation.Render | UiInvalidation.InputRegion;

    protected override bool HitTestCore(PointF point)
        => UiGeometry.Contains(Bounds, point) || (IsDropDownOpen && UiGeometry.Contains(DropDownBounds, point));

    public bool ContainsPopupPoint(PointF point) => HitTestCore(point);

    void IUiPopup.DismissPopup(UiPopupDismissReason reason) => IsDropDownOpen = false;

    protected override void RenderCore(UiRenderContext context)
    {
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(Bounds, 4f, 4f, enabled ? ResolveBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(Bounds, 4f, 4f, IsFocused && enabled ? ResolveFocusBrush(context) : ResolveBorderBrush(context));
        string display = IsSelectedIndexValid ? SelectedText : Placeholder;
        if (display.Length > 0)
        {
            BrushHandle brush = !enabled ? ResolveDisabledBrush(context) : IsSelectedIndexValid ? ResolveForeground(context) : context.Theme.MutedForeground;
            context.Draw.Draw.Text(display, context.Theme.Font, brush, new PointF(ContentBounds.X, ContentBounds.Y));
        }

        PointF arrowA = new(Bounds.X + Bounds.Width - 20f, Bounds.Y + 12f);
        PointF arrowB = new(Bounds.X + Bounds.Width - 14f, Bounds.Y + 18f);
        PointF arrowC = new(Bounds.X + Bounds.Width - 8f, Bounds.Y + 12f);
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
            int index = (int)((args.Position.Y - DropDownBounds.Y) / itemHeight);
            if (index >= 0 && index < Items.Count)
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
                SelectedIndex = Math.Clamp(SelectedIndex + 1, 0, Math.Max(0, Items.Count - 1));
                args.Handled = true;
                break;
            case UiVirtualKeys.Up:
                SelectedIndex = Math.Clamp(SelectedIndex - 1, 0, Math.Max(0, Items.Count - 1));
                args.Handled = true;
                break;
            case UiVirtualKeys.Home:
                SelectedIndex = Items.Count == 0 ? -1 : 0;
                args.Handled = true;
                break;
            case UiVirtualKeys.End:
                SelectedIndex = Items.Count == 0 ? -1 : Items.Count - 1;
                args.Handled = true;
                break;
        }
    }

    private RectF DropDownBounds
    {
        get
        {
            float height = MathF.Min(MaxDropDownHeight, Items.Count * itemHeight);
            SizeF size = new(Bounds.Width, height);
            return ClampDropDownToOverlay
                ? UiPopupPlacement.ResolveBelowOrAbove(Root, Bounds, size)
                : new RectF(Bounds.X, Bounds.Y + Bounds.Height + 2f, size.Width, size.Height);
        }
    }

    private void RenderDropDown(UiRenderContext context)
    {
        RectF dropdown = DropDownBounds;
        bool enabled = IsEffectivelyEnabled;
        context.Draw.Fill.RoundedRectangle(dropdown, 4f, 4f, enabled ? ResolvePopupBackground(context) : ResolveDisabledBrush(context));
        context.Draw.Draw.RoundedRectangle(dropdown, 4f, 4f, ResolveBorderBrush(context));
        int visibleCount = Math.Min(Items.Count, (int)MathF.Floor(dropdown.Height / itemHeight));
        for (int index = 0; index < visibleCount; index++)
        {
            RectF row = new(dropdown.X, dropdown.Y + index * itemHeight, dropdown.Width, itemHeight);
            if (index == SelectedIndex)
            {
                context.Draw.Fill.Rectangle(row, enabled ? ResolveAccentBrush(context) : ResolveBorderBrush(context));
            }

            context.Draw.Draw.Text(GetItemText(index), context.Theme.Font, enabled ? ResolveForeground(context) : ResolveDisabledBrush(context), new PointF(row.X + 8f, row.Y + 4f));
        }
    }
}
