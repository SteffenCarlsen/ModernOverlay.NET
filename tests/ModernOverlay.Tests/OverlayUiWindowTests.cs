using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiWindowTests
{
    private const int VirtualKeyEscape = 0x1B;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DragResizeAndReleaseSaveManualPlacement()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var store = new MemoryLayoutStore();
        UiWindow window = CreateWindow(120f, 90f);
        window.LayoutKey = "settings";
        window.LayoutStore = store;
        ui.Root.Children.Add(window);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.Left, 50, 60);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 50, 60);
        ui.Render(new DrawContext());

        AssertPlacement(window, 40f, 50f);
        Assert.IsTrue(store.TryLoad("settings", out UiPlacement dragged));
        Assert.AreEqual(40f, dragged.Bounds.X, 0.001f);
        Assert.AreEqual(50f, dragged.Bounds.Y, 0.001f);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 152, 132);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.Left, 182, 152);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 182, 152);

        Assert.AreEqual(150f, window.Width, 0.001f);
        Assert.AreEqual(110f, window.Height, 0.001f);
        Assert.IsTrue(store.TryLoad("settings", out UiPlacement resized));
        Assert.AreEqual(150f, resized.Bounds.Width, 0.001f);
        Assert.AreEqual(110f, resized.Bounds.Height, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ResizeAnchoredWindowKeepsManualSizeAcrossLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var store = new MemoryLayoutStore();
        UiWindow window = CreateWindow(120f, 90f);
        window.LayoutKey = "settings";
        window.LayoutStore = store;
        window.Placement = UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(10f));
        ui.Root.Children.Add(window);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 118, 88);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.Left, 148, 108);
        ui.Render(new DrawContext());
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 148, 108);
        ui.Render(new DrawContext());

        Assert.AreEqual(UiPlacementKind.Manual, window.Placement?.Kind);
        Assert.AreEqual(150f, window.Width, 0.001f);
        Assert.AreEqual(110f, window.Height, 0.001f);
        Assert.AreEqual(150f, window.Bounds.Width, 0.001f);
        Assert.AreEqual(110f, window.Bounds.Height, 0.001f);
        Assert.IsTrue(store.TryLoad("settings", out UiPlacement resized));
        Assert.AreEqual(150f, resized.Bounds.Width, 0.001f);
        Assert.AreEqual(110f, resized.Bounds.Height, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task AnchoredWindowWithStarLayoutContentKeepsExplicitSize()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(120f, 90f);
        window.Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(0f, 10f, 10f, 0f));
        window.Content = CreateStarGridContent();
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        Assert.AreEqual(120f, window.Bounds.Width, 0.001f);
        Assert.AreEqual(90f, window.Bounds.Height, 0.001f);
        AssertPlacement(window, 130f, 10f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerActivationBringsWindowToFrontAndFocusesIt()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow lower = CreateWindow(120f, 90f);
        UiWindow upper = CreateWindow(120f, 90f);
        Canvas.SetLeft(upper, 150f);
        lower.ZIndex = (int)UiLayer.Floating;
        upper.ZIndex = (int)UiLayer.Floating + 3;
        ui.Root.Children.Add(lower);
        ui.Root.Children.Add(upper);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        Assert.AreSame(lower, ui.FocusedElement);
        Assert.IsTrue(lower.ZIndex > upper.ZIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerActivationKeepsWindowBelowPopupLayer()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(120f, 90f);
        window.ZIndex = (int)UiLayer.Popup + 20;
        ModernOverlay.UI.ToolTip toolTip = new()
        {
            Owner = window,
            Text = "Tooltip",
            IsOpen = true,
        };
        ui.Root.Children.Add(window);
        ui.Root.Children.Add(toolTip);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        Assert.IsTrue(window.ZIndex < (int)UiLayer.Popup);
        Assert.IsTrue(toolTip.ZIndex > window.ZIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CloseChromeAndEscapeRequestCloseAndRemoveWindow()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow clicked = CreateWindow(120f, 90f);
        int clickedCloseCount = 0;
        clicked.CloseRequested += (_, _) => clickedCloseCount++;
        ui.Root.Children.Add(clicked);
        ui.Render(new DrawContext());

        Click(overlay, 113, 25);

        Assert.AreEqual(1, clickedCloseCount);
        CollectionAssert.DoesNotContain(ui.Root.Children.ToArray(), clicked);

        UiWindow keyboard = CreateWindow(120f, 90f);
        int keyboardCloseCount = 0;
        keyboard.CloseRequested += (_, _) => keyboardCloseCount++;
        ui.Root.Children.Add(keyboard);
        ui.Render(new DrawContext());
        keyboard.Focus();

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.AreEqual(1, keyboardCloseCount);
        CollectionAssert.DoesNotContain(ui.Root.Children.ToArray(), keyboard);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MinimizeBehaviorsCollapseHideAndDockUntilRestored()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow collapse = CreateWindow(120f, 90f);
        int minimizedChanges = 0;
        collapse.MinimizedChanged += (_, _) => minimizedChanges++;
        ui.Root.Children.Add(collapse);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);
        ui.Render(new DrawContext());

        Assert.IsTrue(collapse.IsMinimized);
        Assert.AreEqual(30f, collapse.Bounds.Height, 0.001f);
        Assert.AreEqual(1, minimizedChanges);

        collapse.Restore();
        ui.Root.Children.Remove(collapse);

        UiWindow hidden = CreateWindow(120f, 90f);
        hidden.MinimizeBehavior = MinimizeBehavior.HideUntilRestored;
        ui.Root.Children.Add(hidden);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);

        Assert.IsTrue(hidden.IsMinimized);
        Assert.AreEqual(UiVisibility.Hidden, hidden.Visibility);

        hidden.Restore();
        Assert.AreEqual(UiVisibility.Visible, hidden.Visibility);
        ui.Root.Children.Remove(hidden);

        UiWindow docked = CreateWindow(120f, 90f);
        docked.MinimizeBehavior = MinimizeBehavior.Dock;
        ui.Root.Children.Add(docked);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);

        Assert.IsTrue(docked.IsMinimized);
        Assert.AreEqual(200f, docked.Width, 0.001f);
        Assert.AreEqual(30f, docked.Height, 0.001f);
        AssertPlacement(docked, 8f, 142f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ChromeButtonGlyphsAreCenteredInButtonBounds()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(120f, 90f);
        ui.Root.Children.Add(window);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        LineRun minimize = sink.LineRuns.Single(line => line.Start.X == 85f && line.End.X == 93f);
        Assert.AreEqual(25f, minimize.Start.Y, 0.001f);
        Assert.AreEqual(25f, minimize.End.Y, 0.001f);

        LineRun closeA = sink.LineRuns.Single(line => line.Start.X == 109.5f && line.Start.Y == 21.5f);
        LineRun closeB = sink.LineRuns.Single(line => line.Start.X == 116.5f && line.Start.Y == 21.5f);
        Assert.AreEqual(116.5f, closeA.End.X, 0.001f);
        Assert.AreEqual(28.5f, closeA.End.Y, 0.001f);
        Assert.AreEqual(109.5f, closeB.End.X, 0.001f);
        Assert.AreEqual(28.5f, closeB.End.Y, 0.001f);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiWindow CreateWindow(float width, float height)
    {
        UiWindow window = new()
        {
            Title = "Tools",
            Width = width,
            Height = height,
            MinWidth = 80f,
            MinHeight = 60f,
            Padding = Thickness.Zero,
        };
        Canvas.SetLeft(window, 10f);
        Canvas.SetTop(window, 10f);
        return window;
    }

    private static Grid CreateStarGridContent()
    {
        Grid grid = new() { Height = 48f };
        grid.Columns.Add(new GridDefinition(GridLength.Pixel(40f)));
        grid.Columns.Add(new GridDefinition(GridLength.Star()));
        grid.Rows.Add(new GridDefinition(GridLength.Pixel(20f)));
        grid.Rows.Add(new GridDefinition(GridLength.Star()));
        grid.Children.Add(new TextBlock { Text = "Fixed" });
        TextBlock star = new() { Text = "Star" };
        Grid.SetColumn(star, 1);
        grid.Children.Add(star);
        TextBlock span = new() { Text = "Span" };
        Grid.SetRow(span, 1);
        Grid.SetColumnSpan(span, 2);
        grid.Children.Add(span);
        return grid;
    }

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }

    private static void AssertPlacement(UiWindow window, float expectedLeft, float expectedTop)
    {
        Assert.AreEqual(expectedLeft, Canvas.GetLeft(window), 0.001f);
        Assert.AreEqual(expectedTop, Canvas.GetTop(window), 0.001f);
    }

    private sealed class MemoryLayoutStore : IUiLayoutStore
    {
        private readonly Dictionary<string, UiPlacement> placements = [];

        public bool TryLoad(string key, out UiPlacement placement)
            => placements.TryGetValue(key, out placement);

        public void Save(string key, UiPlacement placement)
            => placements[key] = placement;
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<TextRun> TextRuns { get; } = [];

        public List<LineRun> LineRuns { get; } = [];

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
        {
            LineRuns.Add(new LineRun(start, end));
            AddPrimitive();
        }

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
            TextRuns.Add(new TextRun(text, origin));
            AddPrimitive();
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font)
            => new(MeasureTextWidth(text), font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout)
            => new(MeasureTextWidth(layout.Text), layout.Font.Options.Size);

        public static float MeasureTextWidth(string text)
            => text switch
            {
                "-" => 6f,
                "x" => 8f,
                _ => text.Length * 7f,
            };

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }

    private sealed record TextRun(string Text, PointF Origin);

    private sealed record LineRun(PointF Start, PointF End);
}
