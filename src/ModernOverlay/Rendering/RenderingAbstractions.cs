namespace ModernOverlay.Rendering;

internal enum RenderBackendKind
{
    Null,
    Direct2DHwnd,
    DirectComposition,
}

internal readonly record struct RenderBackendGeneration(long Value)
{
    public static RenderBackendGeneration Initial => new(1);

    public RenderBackendGeneration Next() => new(Value + 1);
}

internal readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

internal readonly record struct FrameInfo(
    long FrameNumber,
    DateTimeOffset TimestampUtc,
    TimeSpan ElapsedTime,
    TimeSpan DeltaTime,
    TimeSpan TargetFrameInterval,
    TimeSpan ActualFrameInterval,
    int RenderThreadId,
    DpiScale DpiScale,
    WindowBounds WindowBounds,
    WindowBounds? TargetBounds);

internal readonly record struct BeginFrameResult(bool CanRender, string? SkipReason)
{
    public static BeginFrameResult Ready => new(true, null);

    public static BeginFrameResult Skipped(string reason) => new(false, reason);
}

internal readonly record struct EndFrameResult(bool Presented);

internal sealed record RenderBackendInitializeContext(
    WindowHandle Hwnd,
    PixelSize PixelSize,
    DpiScale Dpi,
    RenderQualityOptions Quality,
    PresentMode PresentMode);

internal interface IRenderBackend : IDisposable
{
    RenderBackendKind Kind { get; }

    RenderBackendGeneration Generation { get; }

    IDrawCommandSink CommandSink { get; }

    IBackendResourceFactory Resources { get; }

    void Initialize(RenderBackendInitializeContext context);

    void Resize(PixelSize size, DpiScale dpi);

    void Recreate(RenderBackendInitializeContext context);

    BeginFrameResult BeginFrame(in FrameInfo frameInfo);

    EndFrameResult EndFrame();

    void Clear(ColorRgba color);

    void SetQuality(RenderQualityOptions quality);

    void SetPresentMode(PresentMode presentMode);
}

internal interface IDrawCommandSink
{
    int CommandCount { get; }

    int PrimitiveCount { get; }

    int TransientTextLayoutCount { get; }

    int NativeResourceCount { get; }

    void Clear(ColorRgba color);

    void PushClip(RectF clip);

    void PopClip();

    void PushTransform(Matrix3x2F transform);

    void PopTransform();

    void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillRectangle(RectF rect, BrushHandle brush);

    void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush);

    void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillCircle(PointF center, float radius, BrushHandle brush);

    void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillEllipse(RectF bounds, BrushHandle brush);

    void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush);

    void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle);

    void FillGeometry(GeometryPath geometry, BrushHandle brush);

    void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode);

    void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin);

    void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin);

    SizeF MeasureText(string text, FontHandle font);

    SizeF MeasureTextLayout(TextLayoutHandle layout);
}

internal interface IBackendResourceFactory
{
    string BackendName { get; }
}
