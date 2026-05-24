using ModernOverlay.Rendering;
using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiRenderOrderTests
{
    private static readonly string[] ExpectedLayerOrder = ["content-a", "content-b", "floating", "popup", "adorner"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RenderSortsByZIndexThenInsertionOrder()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ui.Root.Children.Add(CreateElement("popup", zIndex: (int)UiLayer.Popup));
        ui.Root.Children.Add(CreateElement("content-a", zIndex: (int)UiLayer.Content));
        ui.Root.Children.Add(CreateElement("adorner", zIndex: (int)UiLayer.Adorner));
        ui.Root.Children.Add(CreateElement("content-b", zIndex: (int)UiLayer.Content));
        ui.Root.Children.Add(CreateElement("floating", zIndex: (int)UiLayer.Floating));
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        CollectionAssert.AreEqual(ExpectedLayerOrder, sink.TextRuns);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RenderPushesElementClipsAroundChildDrawCalls()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        RecordingElement first = CreateElement("first", 10f, 20f, 30f, 40f);
        RecordingElement second = CreateElement("second", 60f, 70f, 20f, 25f);
        ui.Root.Children.Add(first);
        ui.Root.Children.Add(second);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.Contains(new RectF(0f, 0f, 320f, 220f), sink.Clips);
        Assert.Contains(first.Bounds, sink.Clips);
        Assert.Contains(second.Bounds, sink.Clips);
        CollectionAssert.Contains(sink.Commands, "DrawText:first");
        CollectionAssert.Contains(sink.Commands, "PopClip");
        int drawIndex = sink.Commands.IndexOf("DrawText:first");
        Assert.IsTrue(drawIndex > 0);
        Assert.AreEqual("PushClip", sink.Commands[drawIndex - 1]);
        Assert.AreEqual("PopClip", sink.Commands[drawIndex + 1]);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private static RecordingElement CreateElement(string name, int zIndex)
        => CreateElement(name, 0f, 0f, 20f, 20f, zIndex);

    private static RecordingElement CreateElement(string name, float x, float y, float width, float height, int zIndex = 0)
    {
        RecordingElement element = new(name)
        {
            Width = width,
            Height = height,
            ZIndex = zIndex,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }

    private sealed class RecordingElement(string name) : UiElement
    {
        protected override void RenderCore(UiRenderContext context)
            => context.Draw.Draw.Text(name, context.Theme.Font, context.Theme.Foreground, new PointF(Bounds.X, Bounds.Y));
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> Commands { get; } = [];

        public List<string> TextRuns { get; } = [];

        public List<RectF> Clips { get; } = [];

        public int CommandCount { get; private set; }

        public int PrimitiveCount { get; private set; }

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

        public void Clear(ColorRgba color) => AddCommand("Clear");

        public void PushClip(RectF clip)
        {
            Clips.Add(clip);
            AddCommand("PushClip");
        }

        public void PopClip() => AddCommand("PopClip");

        public void PushTransform(Matrix3x2F transform) => AddCommand("PushTransform");

        public void PopTransform() => AddCommand("PopTransform");

        public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawLine));

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawRectangle));

        public void FillRectangle(RectF rect, BrushHandle brush)
            => AddPrimitive(nameof(FillRectangle));

        public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawRoundedRectangle));

        public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
            => AddPrimitive(nameof(FillRoundedRectangle));

        public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawCircle));

        public void FillCircle(PointF center, float radius, BrushHandle brush)
            => AddPrimitive(nameof(FillCircle));

        public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawEllipse));

        public void FillEllipse(RectF bounds, BrushHandle brush)
            => AddPrimitive(nameof(FillEllipse));

        public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawTriangle));

        public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
            => AddPrimitive(nameof(FillTriangle));

        public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive(nameof(DrawGeometry));

        public void FillGeometry(GeometryPath geometry, BrushHandle brush)
            => AddPrimitive(nameof(FillGeometry));

        public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
            => AddPrimitive(nameof(DrawImage));

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
        {
            TextRuns.Add(text);
            AddPrimitive($"DrawText:{text}");
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive(nameof(DrawTextLayout));

        public SizeF MeasureText(string text, FontHandle font)
            => new(text.Length, font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
            => new(layout.Text.Length, layout.Font.Options.Size);

        private void AddPrimitive(string command)
        {
            PrimitiveCount++;
            AddCommand(command);
        }

        private void AddCommand(string command)
        {
            CommandCount++;
            Commands.Add(command);
        }
    }
}
