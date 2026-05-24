using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiComboBox = ModernOverlay.UI.ComboBox;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiComboBoxTests
{
    private const int VirtualKeyEscape = 0x1B;
    private const int VirtualKeyEnd = 0x23;
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyUp = 0x26;
    private const int VirtualKeyDown = 0x28;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerTogglesDropDownAndSelectsItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        int changes = 0;
        comboBox.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(comboBox.IsDropDownOpen);

        Click(overlay, 20, 71);

        Assert.IsFalse(comboBox.IsDropDownOpen);
        Assert.AreEqual(1, comboBox.SelectedIndex);
        Assert.AreEqual("Auto", comboBox.SelectedItem);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OpenDropDownExtendsRenderBoundsBeyondHeader()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        ui.Render(new DrawContext());

        Assert.IsTrue(comboBox.IsDropDownOpen);
        Assert.IsTrue(comboBox.Bounds.Height > comboBox.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task HoveredDropDownItemRendersWithEmphasizedText()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 20, 71);
        var sink = new RecordingDrawCommandSink();
        ui.Render(new DrawContext(sink));

        Assert.AreEqual(2, sink.TextRuns.Count(text => text == "Auto"));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerInUnrenderedDropDownPartialRowDoesNotSelectHiddenItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid", "Hidden");
        comboBox.MaxDropDownHeight = 92f;
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(comboBox.IsDropDownOpen);

        Click(overlay, 20, 120);

        Assert.IsFalse(comboBox.IsDropDownOpen);
        Assert.AreEqual(-1, comboBox.SelectedIndex);
        Assert.IsNull(comboBox.SelectedItem);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OutsidePointerAndEscapeCloseDropDown()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());
        comboBox.IsDropDownOpen = true;

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 220, 130);
        Assert.IsFalse(comboBox.IsDropDownOpen);

        comboBox.IsDropDownOpen = true;

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsFalse(comboBox.IsDropDownOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardNavigationSkipsDisabledItems()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Disabled", "Hybrid");
        comboBox.IsItemEnabledSelector = item => !Equals(item, "Disabled");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());
        comboBox.Focus();

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(0, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(2, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyUp);
        Assert.AreEqual(0, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, comboBox.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OpenDropDownUsesPopupZOrderForSelection()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox lower = CreateComboBox("Lower A", "Lower B");
        UiComboBox upper = CreateComboBox("Upper A", "Upper B");
        lower.ZIndex = (int)UiLayer.Popup;
        upper.ZIndex = (int)UiLayer.Popup + 1;
        lower.IsDropDownOpen = true;
        upper.IsDropDownOpen = true;
        ui.Root.Children.Add(lower);
        ui.Root.Children.Add(upper);
        ui.Render(new DrawContext());

        Click(overlay, 20, 71);

        Assert.AreEqual(-1, lower.SelectedIndex);
        Assert.AreEqual(1, upper.SelectedIndex);
        Assert.IsTrue(lower.IsDropDownOpen);
        Assert.IsFalse(upper.IsDropDownOpen);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiComboBox CreateComboBox(params object?[] items)
    {
        UiComboBox comboBox = new()
        {
            Width = 140f,
        };
        Canvas.SetLeft(comboBox, 10f);
        Canvas.SetTop(comboBox, 10f);
        foreach (object? item in items)
        {
            comboBox.Items.Add(item);
        }

        return comboBox;
    }

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
        => DispatchPointer(overlay, new Win32PointerEvent(kind, button, x, y));

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEvent pointer)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [pointer]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> TextRuns { get; } = [];

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
            TextRuns.Add(text);
            AddPrimitive();
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font) => new(text.Length * 7f, 14f);

        public SizeF MeasureTextLayout(TextLayoutHandle layout) => new(layout.Text.Length * 7f, 14f);

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }
}
