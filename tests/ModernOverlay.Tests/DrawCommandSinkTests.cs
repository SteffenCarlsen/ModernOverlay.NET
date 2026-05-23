using ModernOverlay.Rendering;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class DrawCommandSinkTests
{
    private static readonly string[] ExpectedForwardedCommands = ["Clear", "PushClip", "PushTransform", "DrawLine", "FillRectangle", "PopTransform", "PopClip", "DrawImage", "MeasureText", "DrawTextLayout", "MeasureTextLayout"];
    private static readonly string[] ExpectedUnwindCommands = ["PushClip", "PushTransform", "PopClip", "PopTransform"];

    [TestMethod]
    public void DrawContextForwardsCommandsToSink()
    {
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);
        var resources = new OverlayResourceManager();
        SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);

        context.Clear(ColorRgba.Black);
        using (context.Clip(new RectF(0, 0, 20, 20)))
        using (context.Transform(Matrix3x2F.CreateTranslation(4, 5)))
        {
            context.Draw.Line(new PointF(0, 0), new PointF(10, 10), brush, 2);
            context.Fill.Rectangle(new RectF(0, 0, 10, 10), brush);
        }

        using ImageHandle image = resources.CreateImage([1]);
        context.Draw.Image(image, frameIndex: 0, new RectF(0, 0, 1, 1));
        SizeF measured = context.Measure.Text("abc", resources.CreateFont(new FontOptions("Segoe UI", 12)));
        using TextLayoutHandle layout = resources.CreateTextLayout("abcd", resources.CreateFont(new FontOptions("Segoe UI", 10)));
        context.Draw.Text(layout, brush, new PointF(1, 2));
        SizeF measuredLayout = context.Measure.Text(layout);

        Assert.AreEqual(new SizeF(3, 12), measured);
        Assert.AreEqual(new SizeF(4, 10), measuredLayout);
        CollectionAssert.AreEqual(ExpectedForwardedCommands, sink.Commands);
    }

    [TestMethod]
    public void DrawImageRejectsNegativeFrameIndex()
    {
        var context = new DrawContext();
        var resources = new OverlayResourceManager();
        using ImageHandle image = resources.CreateImage([1]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => context.Draw.Image(image, frameIndex: -1, new RectF(0, 0, 1, 1)));
    }

    [TestMethod]
    public void FrameScopedDrawContextRejectsCommandsOutsideActiveFrame()
    {
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink, enforceFrameScope: true);

        InvalidOperationException beforeFrame = Assert.ThrowsExactly<InvalidOperationException>(() => context.Clear(ColorRgba.Black));
        StringAssert.Contains(beforeFrame.Message, "render callback");

        context.BeginFrame();
        context.Clear(ColorRgba.Black);
        context.CompleteFrame();

        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);
        InvalidOperationException afterFrame = Assert.ThrowsExactly<InvalidOperationException>(() => context.Draw.Line(
            new PointF(0, 0),
            new PointF(1, 1),
            brush));
        StringAssert.Contains(afterFrame.Message, "render callback");
    }

    [TestMethod]
    public void CompleteFrameUnwindsUnbalancedScopesBeforeThrowing()
    {
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);

        context.PushClip(new RectF(0, 0, 10, 10));
        context.PushTransform(Matrix3x2F.CreateTranslation(1, 2));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(context.CompleteFrame);

        StringAssert.Contains(exception.Message, "1 clip scope(s)");
        StringAssert.Contains(exception.Message, "1 transform scope(s)");
        CollectionAssert.AreEqual(ExpectedUnwindCommands, sink.Commands);
    }

    [TestMethod]
    public void DrawHelpersForwardToPrimitiveCommands()
    {
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);
        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);

        context.Draw.Box(new RectF(0, 0, 20, 10), brush);
        context.Draw.Crosshair(new PointF(10, 10), 5, brush);
        context.Draw.Arrow(new PointF(0, 0), new PointF(10, 0), brush);

        string[] expected =
        [
            "DrawRectangle",
            "DrawLine",
            "DrawLine",
            "DrawLine",
            "DrawLine",
            "DrawLine",
        ];
        CollectionAssert.AreEqual(expected, sink.Commands);
    }

    [TestMethod]
    public void CornerBoxDrawsEightCornerSegments()
    {
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);
        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);

        context.Draw.CornerBox(new RectF(10, 20, 40, 20), brush, cornerLength: 8, strokeWidth: 2);

        Assert.AreEqual(8, sink.Lines.Count);
        Assert.AreEqual(new PointF(10, 20), sink.Lines[0].Start);
        Assert.AreEqual(new PointF(18, 20), sink.Lines[0].End);
        Assert.AreEqual(new PointF(10, 20), sink.Lines[1].Start);
        Assert.AreEqual(new PointF(10, 28), sink.Lines[1].End);
        Assert.AreEqual(new PointF(50, 40), sink.Lines[6].Start);
        Assert.AreEqual(new PointF(42, 40), sink.Lines[6].End);
        Assert.AreEqual(new PointF(50, 40), sink.Lines[7].Start);
        Assert.AreEqual(new PointF(50, 32), sink.Lines[7].End);
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> Commands { get; } = [];

        public List<(PointF Start, PointF End)> Lines { get; } = [];

        public int CommandCount => Commands.Count;

        public int PrimitiveCount => Commands.Count(command => command.StartsWith("Draw", StringComparison.Ordinal) || command.StartsWith("Fill", StringComparison.Ordinal));

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

        public void Clear(ColorRgba color) => Commands.Add(nameof(Clear));

        public void PushClip(RectF clip) => Commands.Add(nameof(PushClip));

        public void PopClip() => Commands.Add(nameof(PopClip));

        public void PushTransform(Matrix3x2F transform) => Commands.Add(nameof(PushTransform));

        public void PopTransform() => Commands.Add(nameof(PopTransform));

        public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        {
            Lines.Add((start, end));
            Commands.Add(nameof(DrawLine));
        }

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawRectangle));

        public void FillRectangle(RectF rect, BrushHandle brush)
            => Commands.Add(nameof(FillRectangle));

        public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawRoundedRectangle));

        public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
            => Commands.Add(nameof(FillRoundedRectangle));

        public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawCircle));

        public void FillCircle(PointF center, float radius, BrushHandle brush)
            => Commands.Add(nameof(FillCircle));

        public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawEllipse));

        public void FillEllipse(RectF bounds, BrushHandle brush)
            => Commands.Add(nameof(FillEllipse));

        public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawTriangle));

        public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
            => Commands.Add(nameof(FillTriangle));

        public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawGeometry));

        public void FillGeometry(GeometryPath geometry, BrushHandle brush)
            => Commands.Add(nameof(FillGeometry));

        public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
            => Commands.Add(nameof(DrawImage));

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
            => Commands.Add(nameof(DrawText));

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => Commands.Add(nameof(DrawTextLayout));

        public SizeF MeasureText(string text, FontHandle font)
        {
            Commands.Add(nameof(MeasureText));
            return new SizeF(text.Length, font.Options.Size);
        }

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
        {
            Commands.Add(nameof(MeasureTextLayout));
            return new SizeF(layout.Text.Length, layout.Font.Options.Size);
        }
    }
}
