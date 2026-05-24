using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;
using UiCheckBox = ModernOverlay.UI.CheckBox;
using UiRadioButton = ModernOverlay.UI.RadioButton;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiButtonControlTests
{
    private const int VirtualKeySpace = 0x20;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ButtonActivatesByPointerKeyboardAndCommandButSuppressesCancelCases()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ui.DragThreshold = 4f;
        object? commandParameter = null;
        int clicks = 0;
        UiButton button = CreateButton<UiButton>("Apply", 10f, 10f);
        button.CommandParameter = "profile";
        button.Command = new UiCommand(parameter => commandParameter = parameter);
        button.Click += (_, _) => clicks++;
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        button.Focus();
        DispatchKey(overlay, VirtualKeySpace);
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 40, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 160, 160);

        Assert.AreEqual(2, clicks);
        Assert.AreEqual("profile", commandParameter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ToggleButtonTogglesByPointerKeyboardCommandAndSkipsDisabledInput()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ToggleButton toggle = CreateButton<ToggleButton>("Toggle", 10f, 10f);
        int checkedChanges = 0;
        int commandCalls = 0;
        toggle.CheckedChanged += (_, _) => checkedChanges++;
        toggle.Command = new UiCommand(_ => commandCalls++);
        ui.Root.Children.Add(toggle);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(toggle.IsChecked);

        toggle.Focus();
        DispatchKey(overlay, VirtualKeySpace);
        Assert.IsFalse(toggle.IsChecked);

        toggle.IsEnabled = false;
        Click(overlay, 20, 20);
        DispatchKey(overlay, VirtualKeySpace);

        Assert.IsFalse(toggle.IsChecked);
        Assert.AreEqual(2, checkedChanges);
        Assert.AreEqual(2, commandCalls);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CheckBoxLaysOutRendersGlyphAndActivatesFromKeyboard()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiCheckBox checkBox = CreateButton<UiCheckBox>("Check", 10f, 10f);
        ui.Root.Children.Add(checkBox);
        ui.Render(new DrawContext());

        checkBox.Focus();
        DispatchKey(overlay, VirtualKeySpace);
        Assert.IsTrue(checkBox.IsChecked);

        var sink = new RecordingDrawCommandSink();
        ui.Render(new DrawContext(sink));

        Assert.IsTrue(checkBox.Bounds.Width >= checkBox.MinWidth);
        Assert.IsTrue(sink.DrawRectangles.Count > 0);
        Assert.IsTrue(sink.FilledRectangles.Count > 0);
        Assert.Contains("Check", sink.TextRuns);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ButtonTextAlignmentControlsTextOrigin()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton left = CreateButton<UiButton>("Align", 10f, 10f);
        UiButton right = CreateButton<UiButton>("Align", 10f, 50f);
        left.TextHorizontalAlignment = UiHorizontalAlignment.Left;
        right.TextHorizontalAlignment = UiHorizontalAlignment.Right;
        ui.Root.Children.Add(left);
        ui.Root.Children.Add(right);
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        PointF leftOrigin = sink.TextOrigins["Align"][0];
        PointF rightOrigin = sink.TextOrigins["Align"][1];
        Assert.IsTrue(leftOrigin.X < rightOrigin.X);
        Assert.AreEqual(left.ContentBounds.X, leftOrigin.X, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RadioButtonClearsPeersAndHonorsDisabledDynamicGroupChanges()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiRadioButton first = CreateButton<UiRadioButton>("One", 10f, 10f);
        UiRadioButton second = CreateButton<UiRadioButton>("Two", 10f, 42f);
        UiRadioButton third = CreateButton<UiRadioButton>("Three", 10f, 74f);
        first.GroupName = "group";
        second.GroupName = "group";
        third.GroupName = "group";
        third.IsEnabled = false;
        ui.Root.Children.Add(first);
        ui.Root.Children.Add(second);
        ui.Root.Children.Add(third);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(first.IsChecked);

        second.Focus();
        DispatchKey(overlay, VirtualKeySpace);
        Assert.IsFalse(first.IsChecked);
        Assert.IsTrue(second.IsChecked);

        Click(overlay, 20, 84);
        Assert.IsFalse(third.IsChecked);
        Assert.IsTrue(second.IsChecked);

        ui.Root.Children.Remove(second);
        third.IsEnabled = true;
        Click(overlay, 20, 84);

        Assert.IsTrue(third.IsChecked);
        Assert.IsFalse(first.IsChecked);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private static T CreateButton<T>(string text, float x, float y)
        where T : UiButton, new()
    {
        T button = new()
        {
            Text = text,
            Width = 100f,
            Height = 28f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(button, x);
        Canvas.SetTop(button, y);
        return button;
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

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<RectF> DrawRectangles { get; } = [];

        public List<RectF> FilledRectangles { get; } = [];

        public List<string> TextRuns { get; } = [];

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
        {
            DrawRectangles.Add(rect);
            AddPrimitive();
        }

        public void FillRectangle(RectF rect, BrushHandle brush)
        {
            FilledRectangles.Add(rect);
            AddPrimitive();
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
        {
            TextRuns.Add(text);
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
