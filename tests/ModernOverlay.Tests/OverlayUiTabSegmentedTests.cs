using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiTabControl = ModernOverlay.UI.TabControl;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiTabSegmentedTests
{
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyLeft = 0x25;
    private const int VirtualKeyRight = 0x27;
    private const int VirtualKeyEnd = 0x23;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabPointerSelectionSkipsDisabledTabsAndArrangesActiveContent()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out ProbeElement first, out ProbeElement second, out ProbeElement third);
        tabs.Items[1].IsEnabled = false;
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());

        Click(overlay, 70, 20);
        Assert.AreEqual(0, tabs.SelectedIndex);
        CollectionAssert.Contains(tabs.Children.ToArray(), first);
        CollectionAssert.DoesNotContain(tabs.Children.ToArray(), second);

        Click(overlay, 125, 20);
        ui.Render(new DrawContext());

        Assert.AreEqual(2, tabs.SelectedIndex);
        CollectionAssert.Contains(tabs.Children.ToArray(), third);
        Assert.AreEqual(10f, third.Bounds.X, 0.001f);
        Assert.AreEqual(40f, third.Bounds.Y, 0.001f);
        Assert.AreEqual(220f, third.Bounds.Width, 0.001f);
        Assert.AreEqual(90f, third.Bounds.Height, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabPointerBoundaryKeepsPreviousRenderedHeader()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out _, out _, out _);
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());

        float boundaryX = 10f + TabHeaderWidth("One");
        ClickUi(ui, new PointF(boundaryX, 20f));

        Assert.AreEqual(0, tabs.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabPointerBottomEdgeStillHitsHeader()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out _, out _, out _);
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());

        ClickUi(ui, new PointF(70f, 40f));

        Assert.AreEqual(1, tabs.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabKeyboardNavigationSkipsDisabledTabs()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out _, out _, out _);
        tabs.Items[1].IsEnabled = false;
        int changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());
        tabs.Focus();

        DispatchKey(overlay, VirtualKeyRight);
        Assert.AreEqual(2, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyLeft);
        Assert.AreEqual(0, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, tabs.SelectedIndex);
        Assert.AreEqual(4, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabHeadersRenderTextCenteredByDefault()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out _, out _, out _);
        ui.Root.Children.Add(tabs);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        PointF oneOrigin = sink.TextOrigins["One"][0];
        float oneHeaderWidth = TabHeaderWidth("One");
        float expectedX = tabs.Bounds.X + MathF.Max(0f, oneHeaderWidth - RecordingDrawCommandSink.MeasureTextWidth("One")) / 2f;
        Assert.AreEqual(expectedX, oneOrigin.X, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SegmentedControlPointerAndKeyboardSelection()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        SegmentedControl segmented = new()
        {
            Width = 180f,
        };
        Canvas.SetLeft(segmented, 10f);
        Canvas.SetTop(segmented, 10f);
        segmented.Items.Add("A");
        segmented.Items.Add("B");
        segmented.Items.Add("C");
        int changes = 0;
        segmented.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(segmented);
        ui.Render(new DrawContext());

        Click(overlay, 80, 20);
        Assert.AreEqual(1, segmented.SelectedIndex);

        segmented.Focus();
        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, segmented.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, segmented.SelectedIndex);

        DispatchKey(overlay, VirtualKeyLeft);
        Assert.AreEqual(2, segmented.SelectedIndex);
        Assert.AreEqual(4, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SegmentedControlPointerBoundaryKeepsPreviousRenderedSegment()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        SegmentedControl segmented = new()
        {
            Width = 180f,
        };
        Canvas.SetLeft(segmented, 10f);
        Canvas.SetTop(segmented, 10f);
        segmented.Items.Add("A");
        segmented.Items.Add("B");
        segmented.Items.Add("C");
        ui.Root.Children.Add(segmented);
        ui.Render(new DrawContext());

        ClickUi(ui, new PointF(70f, 20f));

        Assert.AreEqual(0, segmented.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SegmentedControlPointerOuterEdgesStillHitRenderedSegments()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        SegmentedControl segmented = new()
        {
            Width = 180f,
        };
        Canvas.SetLeft(segmented, 10f);
        Canvas.SetTop(segmented, 10f);
        segmented.Items.Add("A");
        segmented.Items.Add("B");
        segmented.Items.Add("C");
        ui.Root.Children.Add(segmented);
        ui.Render(new DrawContext());

        ClickUi(ui, new PointF(80f, 40f));
        Assert.AreEqual(1, segmented.SelectedIndex);

        ClickUi(ui, new PointF(190f, 25f));
        Assert.AreEqual(2, segmented.SelectedIndex);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiTabControl CreateTabs(out ProbeElement first, out ProbeElement second, out ProbeElement third)
    {
        UiTabControl tabs = new()
        {
            Width = 220f,
            Height = 120f,
            MinWidth = 0f,
            MinHeight = 0f,
        };
        Canvas.SetLeft(tabs, 10f);
        Canvas.SetTop(tabs, 10f);
        first = new ProbeElement();
        second = new ProbeElement();
        third = new ProbeElement();
        tabs.Add("One", first);
        tabs.Add("Two", second);
        tabs.Add("Three", third);
        return tabs;
    }

    private static float TabHeaderWidth(string header) => header.Length * UiTheme.Default.FontSize * 0.62f + 24f;

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void ClickUi(OverlayUiRoot ui, PointF position)
    {
        DispatchUiPointer(ui, OverlayPointerEventKind.Pressed, OverlayPointerButton.Left, position);
        DispatchUiPointer(ui, OverlayPointerEventKind.Released, OverlayPointerButton.Left, position);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private static void DispatchUiPointer(OverlayUiRoot ui, OverlayPointerEventKind kind, OverlayPointerButton button, PointF position)
    {
        MethodInfo method = typeof(OverlayUiRoot).GetMethod("DispatchPointer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayUiRoot), "DispatchPointer");
        method.Invoke(ui, [new OverlayPointerEventArgs(kind, button, position, (int)MathF.Round(position.X), (int)MathF.Round(position.Y)), kind]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }

    private sealed class ProbeElement : UiElement
    {
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public Dictionary<string, List<PointF>> TextOrigins { get; } = [];

        public int CommandCount { get; private set; }

        public int PrimitiveCount { get; private set; }

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

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
        {
            if (!TextOrigins.TryGetValue(text, out List<PointF>? origins))
            {
                origins = [];
                TextOrigins[text] = origins;
            }

            origins.Add(origin);
            AddPrimitive();
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font)
            => new(MeasureTextWidth(text), font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
            => new(MeasureTextWidth(layout.Text), layout.Font.Options.Size);

        public static float MeasureTextWidth(string text)
            => text.Length * UiTheme.Default.FontSize * 0.62f;

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }
}
