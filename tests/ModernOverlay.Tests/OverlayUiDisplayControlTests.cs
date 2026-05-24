using ModernOverlay.Rendering;
using ModernOverlay.UI;
using UiImage = ModernOverlay.UI.Image;
using UiProgressBar = ModernOverlay.UI.ProgressBar;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiDisplayControlTests
{
    private static readonly string[] ExpectedWrappedTextRuns = ["abcd", "efg…"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TextBlockMeasuresWrapsTrimsAndRendersTextLines()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        TextBlock text = new()
        {
            Text = "abcdefghij",
            Width = 24f,
            Height = 40f,
            TextWrapping = UiTextWrapping.Wrap,
            TextTrimming = UiTextTrimming.CharacterEllipsis,
            MaxLines = 2,
            Font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 10f)),
        };
        ui.Root.Children.Add(text);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.IsTrue(text.DesiredSize.Width <= 24f);
        Assert.IsTrue(text.DesiredSize.Height > 0f);
        CollectionAssert.AreEqual(ExpectedWrappedTextRuns, sink.TextRuns);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task EmptyTextBlockMeasuresButDoesNotRenderText()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        TextBlock text = new()
        {
            Text = string.Empty,
            Width = 120f,
            Height = 24f,
        };
        ui.Root.Children.Add(text);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.IsTrue(text.DesiredSize.Height > 0f);
        Assert.AreEqual(0, sink.TextRuns.Count);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ImageUsesSourceRectStretchAlignmentOpacityAndInterpolation()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ImageHandle image = overlay.Resources.CreateImage([1, 2, 3, 4]);
        UiImage control = new()
        {
            Source = image,
            SourceRect = new RectF(0f, 0f, 40f, 20f),
            Width = 80f,
            Height = 60f,
            Stretch = UiImageStretch.Uniform,
            ImageHorizontalAlignment = UiHorizontalAlignment.Center,
            ImageVerticalAlignment = UiVerticalAlignment.Center,
            ImageOpacity = 0.5f,
            FrameIndex = 2,
            InterpolationMode = ImageInterpolationMode.NearestNeighbor,
        };
        ui.Root.Children.Add(control);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.AreEqual(1, sink.Images.Count);
        ImageCall call = sink.Images[0];
        Assert.AreSame(image, call.Image);
        Assert.AreEqual(2, call.FrameIndex);
        Assert.AreEqual(new RectF(0f, 10f, 80f, 40f), call.Destination);
        Assert.AreEqual(new RectF(0f, 0f, 40f, 20f), call.Source);
        Assert.AreEqual(0.5f, call.Opacity);
        Assert.AreEqual(ImageInterpolationMode.NearestNeighbor, call.InterpolationMode);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ProgressBarClampsValueAndRendersFillRatio()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiProgressBar progress = new()
        {
            Minimum = 10f,
            Maximum = 30f,
            Value = 50f,
            Width = 100f,
            Height = 10f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        ui.Root.Children.Add(progress);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.AreEqual(30f, progress.Value);
        Assert.IsTrue(sink.FilledRoundedRectangles.Count >= 2);
        Assert.AreEqual(100f, sink.FilledRoundedRectangles[0].Width);
        Assert.AreEqual(100f, sink.FilledRoundedRectangles[1].Width);

        progress.Value = 20f;
        sink.ClearRecordedCommands();
        ui.Render(new DrawContext(sink));

        Assert.AreEqual(100f, sink.FilledRoundedRectangles[0].Width);
        Assert.AreEqual(50f, sink.FilledRoundedRectangles[1].Width);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private sealed record ImageCall(
        ImageHandle Image,
        int FrameIndex,
        RectF Destination,
        RectF? Source,
        float Opacity,
        ImageInterpolationMode InterpolationMode);

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> TextRuns { get; } = [];

        public List<ImageCall> Images { get; } = [];

        public List<RectF> FilledRoundedRectangles { get; } = [];

        public int CommandCount { get; private set; }

        public int PrimitiveCount { get; private set; }

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

        public void ClearRecordedCommands()
        {
            TextRuns.Clear();
            Images.Clear();
            FilledRoundedRectangles.Clear();
            CommandCount = 0;
            PrimitiveCount = 0;
        }

        public void Clear(ColorRgba color) => CommandCount++;

        public void PushClip(RectF clip) => CommandCount++;

        public void PopClip() => CommandCount++;

        public void PushTransform(Matrix3x2F transform) => CommandCount++;

        public void PopTransform() => CommandCount++;

        public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillRectangle(RectF rect, BrushHandle brush)
            => AddPrimitive();

        public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
        {
            FilledRoundedRectangles.Add(rect);
            AddPrimitive();
        }

        public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillCircle(PointF center, float radius, BrushHandle brush)
            => AddPrimitive();

        public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillEllipse(RectF bounds, BrushHandle brush)
            => AddPrimitive();

        public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
            => AddPrimitive();

        public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillGeometry(GeometryPath geometry, BrushHandle brush)
            => AddPrimitive();

        public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
        {
            Images.Add(new ImageCall(image, frameIndex, destination, source, opacity, interpolationMode));
            AddPrimitive();
        }

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
        {
            TextRuns.Add(text);
            AddPrimitive();
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font)
            => new(text.Length * MathF.Max(1f, font.Options.Size * 0.56f), font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
            => new(layout.Text.Length * MathF.Max(1f, layout.Font.Options.Size * 0.56f), layout.Font.Options.Size);

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }
}
