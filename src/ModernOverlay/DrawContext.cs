namespace ModernOverlay;

public sealed class DrawContext
{
    private readonly Stack<RectF> clips = new();
    private readonly Stack<Matrix3x2F> transforms = new();

    public DrawContext()
    {
        Draw = new DrawOperations();
        Fill = new FillOperations();
        Measure = new MeasureOperations();
    }

    public DrawOperations Draw { get; }

    public FillOperations Fill { get; }

    public MeasureOperations Measure { get; }

    public ColorRgba? LastClearColor { get; private set; }

    public void Clear(ColorRgba color) => LastClearColor = color;

    public void PushClip(RectF clip)
    {
        if (clip.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(clip), "Clip must have positive width and height.");
        }

        clips.Push(clip);
    }

    public void PopClip()
    {
        if (!clips.TryPop(out _))
        {
            throw new InvalidOperationException("Cannot pop a clip because the clip stack is empty.");
        }
    }

    public void PushTransform(Matrix3x2F transform) => transforms.Push(transform);

    public void PopTransform()
    {
        if (!transforms.TryPop(out _))
        {
            throw new InvalidOperationException("Cannot pop a transform because the transform stack is empty.");
        }
    }

    public ScopedClip Clip(RectF clip)
    {
        PushClip(clip);
        return new ScopedClip(this);
    }

    public ScopedTransform Transform(Matrix3x2F transform)
    {
        PushTransform(transform);
        return new ScopedTransform(this);
    }

    internal void Reset()
    {
        clips.Clear();
        transforms.Clear();
        LastClearColor = null;
    }
}

public readonly struct ScopedClip : IDisposable
{
    private readonly DrawContext? context;

    internal ScopedClip(DrawContext context)
    {
        this.context = context;
    }

    public void Dispose() => context?.PopClip();
}

public readonly struct ScopedTransform : IDisposable
{
    private readonly DrawContext? context;

    internal ScopedTransform(DrawContext context)
    {
        this.context = context;
    }

    public void Dispose() => context?.PopTransform();
}

public sealed class DrawOperations
{
    public void Line(PointF start, PointF end, SolidBrushHandle brush, float strokeWidth = 1f) => ValidateBrush(brush);

    public void Rectangle(RectF rect, SolidBrushHandle brush, float strokeWidth = 1f) => ValidateBrush(brush);

    public void RoundedRectangle(RectF rect, float radiusX, float radiusY, SolidBrushHandle brush, float strokeWidth = 1f)
        => ValidateBrush(brush);

    public void Circle(PointF center, float radius, SolidBrushHandle brush, float strokeWidth = 1f) => ValidateBrush(brush);

    public void Ellipse(RectF bounds, SolidBrushHandle brush, float strokeWidth = 1f) => ValidateBrush(brush);

    public void Triangle(PointF a, PointF b, PointF c, SolidBrushHandle brush, float strokeWidth = 1f) => ValidateBrush(brush);

    public void Geometry(GeometryPath geometry, SolidBrushHandle brush, float strokeWidth = 1f)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateBrush(brush);
    }

    public void Image(ImageHandle image, RectF destination)
    {
        ArgumentNullException.ThrowIfNull(image);
    }

    public void Text(string text, FontHandle font, SolidBrushHandle brush, PointF origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ValidateBrush(brush);
    }

    private static void ValidateBrush(SolidBrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (brush.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(SolidBrushHandle));
        }
    }
}

public sealed class FillOperations
{
    public void Rectangle(RectF rect, SolidBrushHandle brush) => ValidateBrush(brush);

    public void RoundedRectangle(RectF rect, float radiusX, float radiusY, SolidBrushHandle brush) => ValidateBrush(brush);

    public void Circle(PointF center, float radius, SolidBrushHandle brush) => ValidateBrush(brush);

    public void Ellipse(RectF bounds, SolidBrushHandle brush) => ValidateBrush(brush);

    public void Triangle(PointF a, PointF b, PointF c, SolidBrushHandle brush) => ValidateBrush(brush);

    public void Geometry(GeometryPath geometry, SolidBrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateBrush(brush);
    }

    private static void ValidateBrush(SolidBrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (brush.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(SolidBrushHandle));
        }
    }
}

public sealed class MeasureOperations
{
    public SizeF Text(string text, FontHandle font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        return new SizeF(text.Length * font.Options.Size * 0.5f, font.Options.Size);
    }
}
