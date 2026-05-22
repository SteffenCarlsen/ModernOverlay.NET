using ModernOverlay.Rendering;

namespace ModernOverlay;

public sealed class DrawContext
{
    private readonly Stack<RectF> clips = new();
    private readonly Stack<Matrix3x2F> transforms = new();
    private readonly IDrawCommandSink sink;
    private readonly bool enforceFrameScope;
    private bool isFrameActive;

    public DrawContext()
        : this(new NoOpDrawCommandSink())
    {
    }

    internal DrawContext(IDrawCommandSink sink)
        : this(sink, enforceFrameScope: false)
    {
    }

    internal DrawContext(IDrawCommandSink sink, bool enforceFrameScope)
    {
        this.enforceFrameScope = enforceFrameScope;
        this.sink = enforceFrameScope
            ? new FrameScopeDrawCommandSink(sink, EnsureFrameActive)
            : sink;
        Draw = new DrawOperations(this.sink);
        Fill = new FillOperations(this.sink);
        Measure = new MeasureOperations(this.sink);
    }

    public DrawOperations Draw { get; }

    public FillOperations Fill { get; }

    public MeasureOperations Measure { get; }

    public ColorRgba? LastClearColor { get; private set; }

    public void Clear(ColorRgba color)
    {
        EnsureFrameActive();
        LastClearColor = color;
        sink.Clear(color);
    }

    public void PushClip(RectF clip)
    {
        EnsureFrameActive();
        if (clip.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(clip), "Clip must have positive width and height.");
        }

        clips.Push(clip);
        sink.PushClip(clip);
    }

    public void PopClip()
    {
        EnsureFrameActive();
        if (!clips.TryPop(out _))
        {
            throw new InvalidOperationException("Cannot pop a clip because the clip stack is empty.");
        }

        sink.PopClip();
    }

    public void PushTransform(Matrix3x2F transform)
    {
        EnsureFrameActive();
        transforms.Push(transform);
        sink.PushTransform(transform);
    }

    public void PopTransform()
    {
        EnsureFrameActive();
        if (!transforms.TryPop(out _))
        {
            throw new InvalidOperationException("Cannot pop a transform because the transform stack is empty.");
        }

        sink.PopTransform();
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
        isFrameActive = false;
    }

    internal void BeginFrame()
    {
        isFrameActive = true;
    }

    internal void CompleteFrame()
    {
        if (clips.Count == 0 && transforms.Count == 0)
        {
            EndFrame();
            return;
        }

        int clipCount = clips.Count;
        int transformCount = transforms.Count;
        UnwindFrameState();
        EndFrame();
        throw new InvalidOperationException(
            $"The draw frame ended with {clipCount} clip scope(s) and {transformCount} transform scope(s) still active.");
    }

    internal void UnwindFrameState()
    {
        while (clips.TryPop(out _))
        {
            sink.PopClip();
        }

        while (transforms.TryPop(out _))
        {
            sink.PopTransform();
        }
    }

    internal void EndFrame()
    {
        isFrameActive = false;
    }

    private void EnsureFrameActive()
    {
        if (enforceFrameScope && !isFrameActive)
        {
            throw new InvalidOperationException("DrawContext commands are only valid during the overlay render callback.");
        }
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

internal sealed class FrameScopeDrawCommandSink : IDrawCommandSink
{
    private readonly IDrawCommandSink inner;
    private readonly Action ensureFrameActive;

    public FrameScopeDrawCommandSink(IDrawCommandSink inner, Action ensureFrameActive)
    {
        this.inner = inner;
        this.ensureFrameActive = ensureFrameActive;
    }

    public int CommandCount => inner.CommandCount;

    public int PrimitiveCount => inner.PrimitiveCount;

    public int TransientTextLayoutCount => inner.TransientTextLayoutCount;

    public int NativeResourceCount => inner.NativeResourceCount;

    public void Clear(ColorRgba color) => Invoke(() => inner.Clear(color));

    public void PushClip(RectF clip) => Invoke(() => inner.PushClip(clip));

    public void PopClip() => Invoke(inner.PopClip);

    public void PushTransform(Matrix3x2F transform) => Invoke(() => inner.PushTransform(transform));

    public void PopTransform() => Invoke(inner.PopTransform);

    public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawLine(start, end, brush, strokeWidth, strokeStyle));

    public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawRectangle(rect, brush, strokeWidth, strokeStyle));

    public void FillRectangle(RectF rect, BrushHandle brush)
        => Invoke(() => inner.FillRectangle(rect, brush));

    public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawRoundedRectangle(rect, radiusX, radiusY, brush, strokeWidth, strokeStyle));

    public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
        => Invoke(() => inner.FillRoundedRectangle(rect, radiusX, radiusY, brush));

    public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawCircle(center, radius, brush, strokeWidth, strokeStyle));

    public void FillCircle(PointF center, float radius, BrushHandle brush)
        => Invoke(() => inner.FillCircle(center, radius, brush));

    public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawEllipse(bounds, brush, strokeWidth, strokeStyle));

    public void FillEllipse(RectF bounds, BrushHandle brush)
        => Invoke(() => inner.FillEllipse(bounds, brush));

    public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawTriangle(a, b, c, brush, strokeWidth, strokeStyle));

    public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
        => Invoke(() => inner.FillTriangle(a, b, c, brush));

    public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        => Invoke(() => inner.DrawGeometry(geometry, brush, strokeWidth, strokeStyle));

    public void FillGeometry(GeometryPath geometry, BrushHandle brush)
        => Invoke(() => inner.FillGeometry(geometry, brush));

    public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
        => Invoke(() => inner.DrawImage(image, frameIndex, destination, source, opacity, interpolationMode));

    public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
        => Invoke(() => inner.DrawText(text, font, brush, origin));

    public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
        => Invoke(() => inner.DrawTextLayout(layout, brush, origin));

    public SizeF MeasureText(string text, FontHandle font)
    {
        ensureFrameActive();
        return inner.MeasureText(text, font);
    }

    public SizeF MeasureTextLayout(TextLayoutHandle layout)
    {
        ensureFrameActive();
        return inner.MeasureTextLayout(layout);
    }

    private void Invoke(Action action)
    {
        ensureFrameActive();
        action();
    }
}

public sealed class DrawOperations
{
    private readonly IDrawCommandSink sink;

    internal DrawOperations(IDrawCommandSink sink)
    {
        this.sink = sink;
    }

    public void Line(PointF start, PointF end, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawLine(start, end, brush, strokeWidth, null);
    }

    public void Line(PointF start, PointF end, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawLine(start, end, brush, strokeWidth, strokeStyle);
    }

    public void Arrow(PointF start, PointF end, BrushHandle brush, float strokeWidth = 1f, float headLength = 10f, float headAngleDegrees = 30f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        ValidateArrowHead(headLength, headAngleDegrees);
        DrawArrow(start, end, brush, null, strokeWidth, headLength, headAngleDegrees);
    }

    public void Arrow(PointF start, PointF end, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f, float headLength = 10f, float headAngleDegrees = 30f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        ValidateArrowHead(headLength, headAngleDegrees);
        DrawArrow(start, end, brush, strokeStyle, strokeWidth, headLength, headAngleDegrees);
    }

    public void Rectangle(RectF rect, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawRectangle(rect, brush, strokeWidth, null);
    }

    public void Rectangle(RectF rect, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawRectangle(rect, brush, strokeWidth, strokeStyle);
    }

    public void Box(RectF rect, BrushHandle brush, float strokeWidth = 1f)
    {
        Rectangle(rect, brush, strokeWidth);
    }

    public void Box(RectF rect, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        Rectangle(rect, brush, strokeStyle, strokeWidth);
    }

    public void Crosshair(PointF center, float size, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateCrosshair(size);
        Line(new PointF(center.X - size, center.Y), new PointF(center.X + size, center.Y), brush, strokeWidth);
        Line(new PointF(center.X, center.Y - size), new PointF(center.X, center.Y + size), brush, strokeWidth);
    }

    public void Crosshair(PointF center, float size, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateCrosshair(size);
        Line(new PointF(center.X - size, center.Y), new PointF(center.X + size, center.Y), brush, strokeStyle, strokeWidth);
        Line(new PointF(center.X, center.Y - size), new PointF(center.X, center.Y + size), brush, strokeStyle, strokeWidth);
    }

    public void RoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawRoundedRectangle(rect, radiusX, radiusY, brush, strokeWidth, null);
    }

    public void RoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawRoundedRectangle(rect, radiusX, radiusY, brush, strokeWidth, strokeStyle);
    }

    public void Circle(PointF center, float radius, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawCircle(center, radius, brush, strokeWidth, null);
    }

    public void Circle(PointF center, float radius, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawCircle(center, radius, brush, strokeWidth, strokeStyle);
    }

    public void Ellipse(RectF bounds, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawEllipse(bounds, brush, strokeWidth, null);
    }

    public void Ellipse(RectF bounds, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawEllipse(bounds, brush, strokeWidth, strokeStyle);
    }

    public void Triangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawTriangle(a, b, c, brush, strokeWidth, null);
    }

    public void Triangle(PointF a, PointF b, PointF c, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawTriangle(a, b, c, brush, strokeWidth, strokeStyle);
    }

    public void Geometry(GeometryPath geometry, BrushHandle brush, float strokeWidth = 1f)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateBrush(brush);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawGeometry(geometry, brush, strokeWidth, null);
    }

    public void Geometry(GeometryPath geometry, BrushHandle brush, StrokeStyleHandle strokeStyle, float strokeWidth = 1f)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateBrush(brush);
        ValidateStrokeStyle(strokeStyle);
        ValidateStrokeWidth(strokeWidth);
        sink.DrawGeometry(geometry, brush, strokeWidth, strokeStyle);
    }

    public void Image(
        ImageHandle image,
        RectF destination,
        RectF? source = null,
        float opacity = 1f,
        ImageInterpolationMode interpolationMode = ImageInterpolationMode.Linear)
        => Image(image, frameIndex: 0, destination, source, opacity, interpolationMode);

    public void Image(
        ImageHandle image,
        int frameIndex,
        RectF destination,
        RectF? source = null,
        float opacity = 1f,
        ImageInterpolationMode interpolationMode = ImageInterpolationMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImageHandle));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ValidateRect(destination, nameof(destination));
        if (source is { } sourceValue)
        {
            ValidateRect(sourceValue, nameof(source));
        }

        ValidateOpacity(opacity);
        sink.DrawImage(image, frameIndex, destination, source, opacity, interpolationMode);
    }

    public void Text(string text, FontHandle font, BrushHandle brush, PointF origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ValidateBrush(brush);
        sink.DrawText(text, font, brush, origin);
    }

    public void Text(TextLayoutHandle layout, BrushHandle brush, PointF origin)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ValidateBrush(brush);
        if (layout.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(TextLayoutHandle));
        }

        sink.DrawTextLayout(layout, brush, origin);
    }

    private static void ValidateBrush(BrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (brush.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(BrushHandle));
        }
    }

    private static void ValidateStrokeStyle(StrokeStyleHandle strokeStyle)
    {
        ArgumentNullException.ThrowIfNull(strokeStyle);
        if (strokeStyle.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(StrokeStyleHandle));
        }
    }

    private static void ValidateStrokeWidth(float strokeWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
    }

    private static void ValidateRect(RectF rect, string parameterName)
    {
        if (rect.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Rectangle must have positive width and height.");
        }
    }

    private static void ValidateOpacity(float opacity)
    {
        if (opacity is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }
    }

    private void DrawArrow(PointF start, PointF end, BrushHandle brush, StrokeStyleHandle? strokeStyle, float strokeWidth, float headLength, float headAngleDegrees)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0f)
        {
            throw new ArgumentException("Arrow start and end points must be different.", nameof(end));
        }

        float unitX = dx / length;
        float unitY = dy / length;
        float angle = MathF.PI * headAngleDegrees / 180f;
        PointF left = RotateArrowHead(end, -unitX, -unitY, angle, headLength);
        PointF right = RotateArrowHead(end, -unitX, -unitY, -angle, headLength);

        sink.DrawLine(start, end, brush, strokeWidth, strokeStyle);
        sink.DrawLine(end, left, brush, strokeWidth, strokeStyle);
        sink.DrawLine(end, right, brush, strokeWidth, strokeStyle);
    }

    private static PointF RotateArrowHead(PointF end, float baseX, float baseY, float angle, float headLength)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        float x = (baseX * cos) - (baseY * sin);
        float y = (baseX * sin) + (baseY * cos);
        return new PointF(end.X + (x * headLength), end.Y + (y * headLength));
    }

    private static void ValidateArrowHead(float headLength, float headAngleDegrees)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(headLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(headAngleDegrees);
        if (headAngleDegrees >= 90f)
        {
            throw new ArgumentOutOfRangeException(nameof(headAngleDegrees), "Arrow head angle must be less than 90 degrees.");
        }
    }

    private static void ValidateCrosshair(float size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
    }
}

public sealed class FillOperations
{
    private readonly IDrawCommandSink sink;

    internal FillOperations(IDrawCommandSink sink)
    {
        this.sink = sink;
    }

    public void Rectangle(RectF rect, BrushHandle brush)
    {
        ValidateBrush(brush);
        sink.FillRectangle(rect, brush);
    }

    public void RoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
    {
        ValidateBrush(brush);
        sink.FillRoundedRectangle(rect, radiusX, radiusY, brush);
    }

    public void Circle(PointF center, float radius, BrushHandle brush)
    {
        ValidateBrush(brush);
        sink.FillCircle(center, radius, brush);
    }

    public void Ellipse(RectF bounds, BrushHandle brush)
    {
        ValidateBrush(brush);
        sink.FillEllipse(bounds, brush);
    }

    public void Triangle(PointF a, PointF b, PointF c, BrushHandle brush)
    {
        ValidateBrush(brush);
        sink.FillTriangle(a, b, c, brush);
    }

    public void Geometry(GeometryPath geometry, BrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateBrush(brush);
        sink.FillGeometry(geometry, brush);
    }

    private static void ValidateBrush(BrushHandle brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (brush.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(BrushHandle));
        }
    }
}

public sealed class MeasureOperations
{
    private readonly IDrawCommandSink sink;

    internal MeasureOperations(IDrawCommandSink sink)
    {
        this.sink = sink;
    }

    public SizeF Text(string text, FontHandle font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ObjectDisposedException.ThrowIf(font.IsDisposed, font);
        return sink.MeasureText(text, font);
    }

    public SizeF Text(TextLayoutHandle layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ObjectDisposedException.ThrowIf(layout.IsDisposed, layout);
        return sink.MeasureTextLayout(layout);
    }
}
