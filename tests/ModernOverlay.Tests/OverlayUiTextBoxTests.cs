using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiTextBox = ModernOverlay.UI.TextBox;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiTextBoxTests
{
    private const int VirtualKeyA = 0x41;
    private const int VirtualKeyBackspace = 0x08;
    private const int VirtualKeyDelete = 0x2E;
    private const int VirtualKeyEnd = 0x23;
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyLeft = 0x25;
    private const int VirtualKeyRight = 0x27;
    private const int VirtualKeyV = 0x56;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TextInputInsertsAtCaretReplacesSelectionFiltersControlCharactersAndHonorsMaxLength()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTextBox textBox = CreateTextBox("abc");
        textBox.MaxLength = 4;
        int changes = 0;
        textBox.TextChanged += (_, _) => changes++;
        ui.Root.Children.Add(textBox);
        textBox.Focus();

        textBox.CaretIndex = 1;
        DispatchText(overlay, "XYZ");

        Assert.AreEqual("aXbc", textBox.Text);
        Assert.AreEqual(2, textBox.CaretIndex);

        textBox.SelectionStart = 1;
        textBox.SelectionLength = 2;
        DispatchText(overlay, "z\r\n");

        Assert.AreEqual("azc", textBox.Text);
        Assert.AreEqual(2, textBox.CaretIndex);
        Assert.AreEqual(0, textBox.SelectionLength);
        Assert.IsTrue(changes >= 2);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardEditingMovesCaretExtendsSelectionAndDeletesText()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTextBox textBox = CreateTextBox("abcd");
        ui.Root.Children.Add(textBox);
        textBox.Focus();
        textBox.CaretIndex = 2;

        DispatchKey(overlay, VirtualKeyLeft, pressed: true);
        Assert.AreEqual(1, textBox.CaretIndex);

        DispatchKey(overlay, VirtualKeyRight, pressed: true, modifiers: Win32ModifierKeys.Shift);
        Assert.AreEqual(1, textBox.SelectionStart);
        Assert.AreEqual(1, textBox.SelectionLength);

        DispatchKey(overlay, VirtualKeyBackspace, pressed: true);
        Assert.AreEqual("acd", textBox.Text);
        Assert.AreEqual(1, textBox.CaretIndex);

        DispatchKey(overlay, VirtualKeyEnd, pressed: true);
        DispatchKey(overlay, VirtualKeyBackspace, pressed: true);
        Assert.AreEqual("ac", textBox.Text);

        DispatchKey(overlay, VirtualKeyHome, pressed: true);
        DispatchKey(overlay, VirtualKeyDelete, pressed: true);
        Assert.AreEqual("c", textBox.Text);

        DispatchKey(overlay, VirtualKeyA, pressed: true, modifiers: Win32ModifierKeys.Control);
        Assert.AreEqual(0, textBox.SelectionStart);
        Assert.AreEqual(1, textBox.SelectionLength);
        Assert.AreEqual(1, textBox.CaretIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ReadOnlyAndDisabledTextBoxesDoNotMutateText()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTextBox readOnly = CreateTextBox("locked");
        UiTextBox disabled = CreateTextBox("disabled");
        readOnly.IsReadOnly = true;
        ui.Root.Children.Add(readOnly);
        ui.Root.Children.Add(disabled);

        readOnly.Focus();
        DispatchText(overlay, "!");
        DispatchKey(overlay, VirtualKeyBackspace, pressed: true);
        Assert.AreEqual("locked", readOnly.Text);

        disabled.Focus();
        disabled.IsEnabled = false;
        Assert.IsNull(ui.FocusedElement);

        DispatchText(overlay, "!");
        Assert.AreEqual("disabled", disabled.Text);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SupportedTextScopeStoresDeliveredUnicodeAndKeepsClipboardPasteDeferred()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTextBox textBox = CreateTextBox(string.Empty);
        ui.Root.Children.Add(textBox);
        textBox.Focus();

        DispatchText(overlay, "é🚀");

        Assert.AreEqual("é🚀", textBox.Text);
        Assert.AreEqual(3, textBox.Text.Length);
        Assert.AreEqual(3, textBox.CaretIndex);

        DispatchKey(overlay, VirtualKeyV, pressed: true, modifiers: Win32ModifierKeys.Control);

        Assert.AreEqual("é🚀", textBox.Text);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RenderDrawsTextSelectionAndCaretForFocusedTextBox()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTextBox textBox = CreateTextBox("render");
        textBox.SelectionStart = 1;
        textBox.SelectionLength = 3;
        textBox.CaretIndex = 4;
        ui.Root.Children.Add(textBox);
        textBox.Focus();
        var sink = new RecordingDrawCommandSink();

        ui.Render(new DrawContext(sink));

        Assert.Contains("render", sink.TextRuns);
        Assert.IsTrue(sink.LineCount > 0);
        Assert.IsTrue(sink.FilledRectangles.Count > 0);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private static UiTextBox CreateTextBox(string text)
        => new()
        {
            Text = text,
            Width = 180f,
            Height = 30f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };

    private static void DispatchKey(
        OverlayWindow overlay,
        int virtualKey,
        bool pressed,
        Win32ModifierKeys modifiers = Win32ModifierKeys.None)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, pressed, false, 1, 0, false, false, !pressed, modifiers)]);
    }

    private static void DispatchText(OverlayWindow overlay, string text)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleTextInputEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleTextInputEvent");
        method.Invoke(overlay, [new Win32TextInputEvent(text, false)]);
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> TextRuns { get; } = [];

        public List<RectF> FilledRectangles { get; } = [];

        public int LineCount { get; private set; }

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
            LineCount++;
            AddPrimitive();
        }

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

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
