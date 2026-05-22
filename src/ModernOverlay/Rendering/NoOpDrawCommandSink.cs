namespace ModernOverlay.Rendering;

internal sealed class NoOpDrawCommandSink : IDrawCommandSink
{
    public int CommandCount { get; private set; }

    public int PrimitiveCount { get; private set; }

    public int TransientTextLayoutCount { get; private set; }

    public int NativeResourceCount => 0;

    internal NoOpDrawCommandSink()
    {
    }

    public void ResetFrame()
    {
        CommandCount = 0;
        PrimitiveCount = 0;
        TransientTextLayoutCount = 0;
    }

    public void Clear(ColorRgba color)
    {
        CommandCount++;
    }

    public void PushClip(RectF clip)
    {
    }

    public void PopClip()
    {
    }

    public void PushTransform(Matrix3x2F transform)
    {
    }

    public void PopTransform()
    {
    }

    public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillRectangle(RectF rect, BrushHandle brush)
    {
        Count();
    }

    public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
    {
        Count();
    }

    public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillCircle(PointF center, float radius, BrushHandle brush)
    {
        Count();
    }

    public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillEllipse(RectF bounds, BrushHandle brush)
    {
        Count();
    }

    public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
    {
        Count();
    }

    public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
    }

    public void FillGeometry(GeometryPath geometry, BrushHandle brush)
    {
        Count();
    }

    public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
    {
        Count();
    }

    public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
    {
        TransientTextLayoutCount++;
        Count();
    }

    public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
    {
        Count();
    }

    public SizeF MeasureText(string text, FontHandle font)
    {
        TransientTextLayoutCount++;
        return new(text.Length * font.Options.Size * 0.5f, font.Options.Size);
    }

    public SizeF MeasureTextLayout(TextLayoutHandle layout)
    {
        SizeF measured = MeasureText(layout.Text, layout.Font);
        return new SizeF(
            MathF.Min(measured.Width, layout.Options.MaxWidth),
            MathF.Min(measured.Height, layout.Options.MaxHeight));
    }

    private void Count()
    {
        CommandCount++;
        PrimitiveCount++;
    }
}
