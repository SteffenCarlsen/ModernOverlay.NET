using ModernOverlay.Rendering;
using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiLayoutTests
{
    private static readonly string[] ExpectedMeasureArrangeOrder = ["measure", "arrange"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task LayoutEngineMeasuresArrangesAndClipsVisibleElements()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var visible = new RecordingElement(new SizeF(70f, 40f))
        {
            Width = 50f,
            Height = 30f,
            MinWidth = 40f,
            MaxWidth = 60f,
            MinHeight = 10f,
            MaxHeight = 35f,
            Margin = new Thickness(5f, 6f, 7f, 8f),
            Padding = new Thickness(2f),
            HorizontalAlignment = UiHorizontalAlignment.Center,
            VerticalAlignment = UiVerticalAlignment.Bottom,
            ReceivesInput = true,
        };
        var hidden = new RecordingElement(new SizeF(25f, 15f))
        {
            Visibility = UiVisibility.Hidden,
        };
        var collapsed = new RecordingElement(new SizeF(25f, 15f))
        {
            Visibility = UiVisibility.Collapsed,
        };
        Canvas.SetLeft(visible, 0f);
        Canvas.SetTop(visible, 0f);
        Canvas.SetLeft(hidden, 80f);
        Canvas.SetTop(hidden, 0f);
        Canvas.SetLeft(collapsed, 120f);
        Canvas.SetTop(collapsed, 0f);
        ui.Root.Children.Add(visible);
        ui.Root.Children.Add(hidden);
        ui.Root.Children.Add(collapsed);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        CollectionAssert.AreEqual(ExpectedMeasureArrangeOrder, visible.Events);
        Assert.AreEqual(new SizeF(60f, 35f), visible.MeasureAvailable);
        Assert.AreEqual(new SizeF(62f, 44f), visible.DesiredSize);
        Assert.AreEqual(new RectF(5f, 6f, 50f, 30f), visible.Bounds);
        Assert.AreEqual(new RectF(7f, 8f, 46f, 26f), visible.ContentBounds);
        Assert.AreEqual(new SizeF(25f, 15f), hidden.DesiredSize);
        Assert.AreEqual(new RectF(80f, 0f, 25f, 15f), hidden.Bounds);
        Assert.AreEqual(new SizeF(0f, 0f), collapsed.DesiredSize);
        Assert.AreEqual(new RectF(120f, 0f, 0f, 0f), collapsed.Bounds);
        Assert.IsTrue(sink.Clips.Contains(visible.Bounds));
        Assert.IsFalse(sink.FilledRects.Contains(hidden.Bounds));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CanvasSupportsAbsoluteRightBottomAndStretchAnchoring()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        RecordingElement absolute = CreateElement(30f, 20f);
        RecordingElement rightBottom = CreateElement(25f, 15f);
        RecordingElement stretched = new(new SizeF(10f, 10f));
        Canvas.SetLeft(absolute, 10f);
        Canvas.SetTop(absolute, 12f);
        Canvas.SetRight(rightBottom, 5f);
        Canvas.SetBottom(rightBottom, 7f);
        Canvas.SetLeft(stretched, 20f);
        Canvas.SetRight(stretched, 30f);
        Canvas.SetTop(stretched, 40f);
        Canvas.SetBottom(stretched, 50f);
        ui.Root.Children.Add(absolute);
        ui.Root.Children.Add(rightBottom);
        ui.Root.Children.Add(stretched);

        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(10f, 12f, 30f, 20f), absolute.Bounds);
        Assert.AreEqual(new RectF(170f, 98f, 25f, 15f), rightBottom.Bounds);
        Assert.AreEqual(new RectF(20f, 40f, 150f, 30f), stretched.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task StackPanelSupportsOrientationSpacingMarginsCollapsedChildrenAndCrossAxisAlignment()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        StackPanel vertical = new()
        {
            Width = 100f,
            Height = 100f,
            Spacing = 4f,
        };
        RecordingElement first = CreateElement(40f, 10f);
        RecordingElement collapsed = CreateElement(50f, 50f);
        RecordingElement rightAligned = CreateElement(30f, 12f);
        first.Margin = new Thickness(1f, 2f);
        collapsed.Visibility = UiVisibility.Collapsed;
        rightAligned.HorizontalAlignment = UiHorizontalAlignment.Right;
        vertical.Children.Add(first);
        vertical.Children.Add(collapsed);
        vertical.Children.Add(rightAligned);

        StackPanel horizontal = new()
        {
            Orientation = UiOrientation.Horizontal,
            Width = 100f,
            Height = 40f,
            Spacing = 3f,
        };
        RecordingElement left = CreateElement(20f, 10f);
        RecordingElement bottom = CreateElement(25f, 12f);
        bottom.VerticalAlignment = UiVerticalAlignment.Bottom;
        horizontal.Children.Add(left);
        horizontal.Children.Add(bottom);
        Canvas.SetLeft(horizontal, 0f);
        Canvas.SetTop(horizontal, 110f);
        ui.Root.Children.Add(vertical);
        ui.Root.Children.Add(horizontal);

        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(1f, 2f, 40f, 10f), first.Bounds);
        Assert.AreEqual(new RectF(0f, 0f, 0f, 0f), collapsed.Bounds);
        Assert.AreEqual(new RectF(70f, 18f, 30f, 12f), rightAligned.Bounds);
        Assert.AreEqual(new RectF(0f, 110f, 20f, 10f), left.Bounds);
        Assert.AreEqual(new RectF(23f, 138f, 25f, 12f), bottom.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DockPanelArrangesDockedChildrenAndFillLastChild()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        DockPanel dock = new()
        {
            Width = 100f,
            Height = 80f,
        };
        RecordingElement left = new(new SizeF(20f, 10f))
        {
            Width = 20f,
        };
        RecordingElement top = new(new SizeF(10f, 15f))
        {
            Height = 15f,
        };
        RecordingElement fill = new(new SizeF(10f, 10f));
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(left);
        dock.Children.Add(top);
        dock.Children.Add(fill);
        ui.Root.Children.Add(dock);

        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(0f, 0f, 20f, 80f), left.Bounds);
        Assert.AreEqual(new RectF(20f, 0f, 80f, 15f), top.Bounds);
        Assert.AreEqual(new RectF(20f, 15f, 80f, 65f), fill.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task GridSupportsPixelAutoStarAndSpans()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(240, 160);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Grid grid = new()
        {
            Width = 200f,
            Height = 100f,
        };
        grid.Columns.Add(new GridDefinition(GridLength.Pixel(40f)));
        grid.Columns.Add(new GridDefinition(GridLength.Auto));
        grid.Columns.Add(new GridDefinition(GridLength.Star(2f)));
        grid.Columns.Add(new GridDefinition(GridLength.Star()));
        grid.Rows.Add(new GridDefinition(GridLength.Pixel(20f)));
        grid.Rows.Add(new GridDefinition(GridLength.Auto));
        grid.Rows.Add(new GridDefinition(GridLength.Star()));
        RecordingElement auto = CreateElement(30f, 25f);
        RecordingElement spanned = new(new SizeF(10f, 10f));
        Grid.SetColumn(auto, 1);
        Grid.SetRow(auto, 1);
        Grid.SetColumn(spanned, 2);
        Grid.SetColumnSpan(spanned, 2);
        Grid.SetRow(spanned, 2);
        grid.Children.Add(auto);
        grid.Children.Add(spanned);
        ui.Root.Children.Add(grid);

        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(40f, 20f, 30f, 25f), auto.Bounds);
        Assert.AreEqual(new RectF(70f, 45f, 130f, 55f), spanned.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task WrapPanelWrapsByAvailableWidthAndSpacing()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        WrapPanel wrap = new()
        {
            Width = 70f,
            Height = 100f,
            Spacing = 5f,
        };
        RecordingElement first = CreateElement(30f, 10f);
        RecordingElement second = CreateElement(30f, 12f);
        RecordingElement third = CreateElement(20f, 8f);
        wrap.Children.Add(first);
        wrap.Children.Add(second);
        wrap.Children.Add(third);
        ui.Root.Children.Add(wrap);

        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(0f, 0f, 30f, 10f), first.Bounds);
        Assert.AreEqual(new RectF(35f, 0f, 30f, 12f), second.Bounds);
        Assert.AreEqual(new RectF(0f, 17f, 20f, 8f), third.Bounds);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync(int width, int height)
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, width, height),
        });

    private static RecordingElement CreateElement(float width, float height)
        => new(new SizeF(width, height))
        {
            Width = width,
            Height = height,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };

    private sealed class RecordingElement(SizeF naturalSize) : UiElement
    {
        public List<string> Events { get; } = [];

        public SizeF MeasureAvailable { get; private set; }

        protected override SizeF MeasureCore(SizeF availableSize)
        {
            Events.Add("measure");
            MeasureAvailable = availableSize;
            return naturalSize;
        }

        protected override void ArrangeCore(RectF finalRect)
        {
            Events.Add("arrange");
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.Draw.Fill.Rectangle(Bounds, context.Theme.Foreground);
        }
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<RectF> Clips { get; } = [];

        public List<RectF> FilledRects { get; } = [];

        public int CommandCount { get; private set; }

        public int PrimitiveCount { get; private set; }

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

        public void Clear(ColorRgba color) => CommandCount++;

        public void PushClip(RectF clip)
        {
            CommandCount++;
            Clips.Add(clip);
        }

        public void PopClip() => CommandCount++;

        public void PushTransform(Matrix3x2F transform) => CommandCount++;

        public void PopTransform() => CommandCount++;

        public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillRectangle(RectF rect, BrushHandle brush)
        {
            AddPrimitive();
            FilledRects.Add(rect);
        }

        public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
            => AddPrimitive();

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
            => AddPrimitive();

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font)
            => new(text.Length, font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
            => new(layout.Text.Length, layout.Font.Options.Size);

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }
}
