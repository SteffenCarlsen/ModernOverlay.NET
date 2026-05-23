namespace ModernOverlay.UI;

public enum MinimizeBehavior
{
    CollapseToTitleBar,
    HideUntilRestored,
    Dock,
}

public sealed class UiWindow : UiPanel
{
    private const float HeaderHeight = 30f;
    private const float ChromeButtonSize = 18f;
    private string title = string.Empty;
    private UiElement? content;
    private bool canDrag = true;
    private bool canResize = true;
    private bool canClose = true;
    private bool canMinimize = true;
    private bool isMinimized;
    private MinimizeBehavior minimizeBehavior;
    private bool dragging;
    private bool resizing;
    private PointF dragOffset;
    private PointF resizeOrigin;
    private SizeF resizeStartSize;
    private UiPlacement? restorePlacement;
    private string? layoutKey;
    private IUiLayoutStore? layoutStore;

    public UiWindow()
    {
        ReceivesInput = true;
        Focusable = true;
        MinWidth = 180f;
        MinHeight = 80f;
        Width = 280f;
        Height = 180f;
        Padding = new Thickness(10f);
        ZIndex = (int)UiLayer.Floating;
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? MinimizedChanged;

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value ?? string.Empty, UiInvalidation.Measure | UiInvalidation.Render);
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

    public bool CanDrag
    {
        get => canDrag;
        set => SetProperty(ref canDrag, value, UiInvalidation.InputRegion);
    }

    public bool CanResize
    {
        get => canResize;
        set => SetProperty(ref canResize, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    public bool CanClose
    {
        get => canClose;
        set => SetProperty(ref canClose, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    public bool CanMinimize
    {
        get => canMinimize;
        set => SetProperty(ref canMinimize, value, UiInvalidation.Render | UiInvalidation.InputRegion);
    }

    public bool IsMinimized
    {
        get => isMinimized;
        set
        {
            if (SetProperty(ref isMinimized, value, UiInvalidation.Measure | UiInvalidation.Render | UiInvalidation.InputRegion))
            {
                MinimizedChanged?.Invoke(this, EventArgs.Empty);
                if (isMinimized && MinimizeBehavior == MinimizeBehavior.HideUntilRestored)
                {
                    Visibility = UiVisibility.Hidden;
                }
            }
        }
    }

    public MinimizeBehavior MinimizeBehavior
    {
        get => minimizeBehavior;
        set => SetProperty(ref minimizeBehavior, value, UiInvalidation.Measure | UiInvalidation.Render);
    }

    public string? LayoutKey
    {
        get => layoutKey;
        set => SetProperty(ref layoutKey, value, UiInvalidation.None);
    }

    public IUiLayoutStore? LayoutStore
    {
        get => layoutStore;
        set => SetProperty(ref layoutStore, value, UiInvalidation.None);
    }

    public void Restore()
    {
        Visibility = UiVisibility.Visible;
        IsMinimized = false;
        if (restorePlacement is { } placement && Parent is Canvas)
        {
            Canvas.SetLeft(this, placement.Bounds.X);
            Canvas.SetTop(this, placement.Bounds.Y);
            Width = placement.Bounds.Width;
            Height = placement.Bounds.Height;
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentSize = new(0f, 0f);
        if (Content is not null && !IsMinimized)
        {
            contentSize = Content.Measure(UiGeometry.Deflate(availableSize, Padding));
        }

        return new SizeF(
            MathF.Max(MinWidth, MathF.Max(Width, contentSize.Width + Padding.Horizontal)),
            IsMinimized && MinimizeBehavior == MinimizeBehavior.CollapseToTitleBar
                ? HeaderHeight
                : MathF.Max(MinHeight, MathF.Max(Height, contentSize.Height + HeaderHeight + Padding.Vertical)));
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        if (Content is null || IsMinimized)
        {
            return;
        }

        Content.Arrange(new RectF(
            Bounds.X + Padding.Left,
            Bounds.Y + HeaderHeight + Padding.Top,
            MathF.Max(0f, Bounds.Width - Padding.Horizontal),
            MathF.Max(0f, Bounds.Height - HeaderHeight - Padding.Vertical)));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.Draw.Fill.RoundedRectangle(Bounds, 6f, 6f, context.Theme.Surface);
        context.Draw.Draw.RoundedRectangle(Bounds, 6f, 6f, IsFocused ? context.Theme.Accent : context.Theme.Border);
        RectF header = HeaderBounds;
        context.Draw.Fill.RoundedRectangle(header, 6f, 6f, context.Theme.SurfaceHover);
        if (Title.Length > 0)
        {
            context.Draw.Draw.Text(Title, context.Theme.Font, context.Theme.Foreground, new PointF(header.X + 10f, header.Y + 7f));
        }

        DrawChromeButton(context, MinimizeButtonBounds, "-");
        DrawChromeButton(context, CloseButtonBounds, "x");

        if (!IsMinimized)
        {
            base.RenderCore(context);
            if (CanResize)
            {
                RectF grip = ResizeGripBounds;
                context.Draw.Draw.Line(new PointF(grip.X, grip.Y + grip.Height), new PointF(grip.X + grip.Width, grip.Y), context.Theme.Border);
                context.Draw.Draw.Line(new PointF(grip.X + 5f, grip.Y + grip.Height), new PointF(grip.X + grip.Width, grip.Y + 5f), context.Theme.Border);
            }
        }
    }

    protected override void OnPointerPressed(UiPointerEventArgs args)
    {
        if (args.Button != OverlayPointerButton.Left)
        {
            return;
        }

        if (CanClose && UiGeometry.Contains(CloseButtonBounds, args.Position))
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            Parent?.Children.Remove(this);
            args.Handled = true;
            return;
        }

        if (CanMinimize && UiGeometry.Contains(MinimizeButtonBounds, args.Position))
        {
            if (IsMinimized)
            {
                Restore();
            }
            else
            {
                Minimize();
            }

            args.Handled = true;
            return;
        }

        BringToFront();
        if (CanResize && UiGeometry.Contains(ResizeGripBounds, args.Position))
        {
            resizing = true;
            resizeOrigin = args.Position;
            resizeStartSize = new SizeF(Bounds.Width, Bounds.Height);
            CapturePointer();
            args.Handled = true;
            return;
        }

        if (CanDrag && UiGeometry.Contains(HeaderBounds, args.Position))
        {
            dragging = true;
            dragOffset = new PointF(args.Position.X - Bounds.X, args.Position.Y - Bounds.Y);
            CapturePointer();
            args.Handled = true;
        }
    }

    protected override void OnPointerMoved(UiPointerEventArgs args)
    {
        if (resizing)
        {
            Width = MathF.Max(MinWidth, resizeStartSize.Width + args.Position.X - resizeOrigin.X);
            Height = MathF.Max(MinHeight, resizeStartSize.Height + args.Position.Y - resizeOrigin.Y);
            InvalidateMeasure();
            args.Handled = true;
            return;
        }

        if (!dragging || Parent is not Canvas)
        {
            return;
        }

        Canvas.SetLeft(this, MathF.Max(0f, args.Position.X - dragOffset.X));
        Canvas.SetTop(this, MathF.Max(0f, args.Position.Y - dragOffset.Y));
        InvalidateArrange();
        args.Handled = true;
    }

    protected override void OnPointerReleased(UiPointerEventArgs args)
    {
        if (dragging || resizing)
        {
            dragging = false;
            resizing = false;
            ReleasePointerCapture();
            SavePlacement();
            args.Handled = true;
        }
    }

    private RectF HeaderBounds => new(Bounds.X, Bounds.Y, Bounds.Width, HeaderHeight);

    private RectF CloseButtonBounds => new(Bounds.X + Bounds.Width - 26f, Bounds.Y + 6f, ChromeButtonSize, ChromeButtonSize);

    private RectF MinimizeButtonBounds => new(Bounds.X + Bounds.Width - 50f, Bounds.Y + 6f, ChromeButtonSize, ChromeButtonSize);

    private RectF ResizeGripBounds => new(Bounds.X + Bounds.Width - 16f, Bounds.Y + Bounds.Height - 16f, 12f, 12f);

    private static void DrawChromeButton(UiRenderContext context, RectF bounds, string text)
    {
        context.Draw.Draw.RoundedRectangle(bounds, 4f, 4f, context.Theme.Border);
        context.Draw.Draw.Text(text, context.Theme.Font, context.Theme.Foreground, new PointF(bounds.X + 5f, bounds.Y));
    }

    private void Minimize()
    {
        CaptureRestorePlacement();
        IsMinimized = true;
        if (MinimizeBehavior == MinimizeBehavior.Dock && Parent is Canvas && Root is not null)
        {
            Width = 200f;
            Height = HeaderHeight;
            Canvas.SetLeft(this, 8f);
            Canvas.SetTop(this, MathF.Max(0f, Root.BoundsDips.Height - HeaderHeight - 8f));
        }
    }

    private void CaptureRestorePlacement()
    {
        if (Parent is not Canvas)
        {
            restorePlacement = UiPlacement.Manual(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
            return;
        }

        restorePlacement = UiPlacement.Manual(Canvas.GetLeft(this), Canvas.GetTop(this), Bounds.Width, Bounds.Height);
    }

    private void SavePlacement()
    {
        if (LayoutStore is null || string.IsNullOrWhiteSpace(LayoutKey))
        {
            return;
        }

        float x = Parent is Canvas ? Canvas.GetLeft(this) : Bounds.X;
        float y = Parent is Canvas ? Canvas.GetTop(this) : Bounds.Y;
        LayoutStore.Save(LayoutKey, UiPlacement.Manual(x, y, Bounds.Width, Bounds.Height));
    }

    private void BringToFront()
    {
        if (Parent is null)
        {
            return;
        }

        int maxZ = Parent.Children.Select(child => child.ZIndex).DefaultIfEmpty(ZIndex).Max();
        ZIndex = Math.Max(ZIndex, maxZ + 1);
    }
}
