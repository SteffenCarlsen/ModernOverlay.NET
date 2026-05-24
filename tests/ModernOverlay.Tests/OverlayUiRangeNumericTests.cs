using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Globalization;
using System.Reflection;
using UiTextBox = ModernOverlay.UI.TextBox;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiRangeNumericTests
{
    private const int VirtualKeyEnd = 0x23;
    private const int VirtualKeyLeft = 0x25;
    private const int VirtualKeyPageDown = 0x22;
    private const int VirtualKeyRight = 0x27;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SliderClampsDragsCapturesTracksKeyboardDisablesAndRenders()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Slider slider = new()
        {
            Minimum = 10f,
            Maximum = 30f,
            Value = 100f,
            SmallChange = 2f,
            LargeChange = 5f,
            Width = 100f,
            Height = 22f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(slider, 10f);
        Canvas.SetTop(slider, 10f);
        ui.Root.Children.Add(slider);
        ui.Render(new DrawContext());

        Assert.AreEqual(30f, slider.Value);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 70, 20);
        Assert.IsTrue(slider.IsPointerCaptured);
        Assert.AreEqual(20f, slider.Value);

        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 130, 20);
        Assert.AreEqual(30f, slider.Value);

        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 10, 20);
        Assert.IsFalse(slider.IsPointerCaptured);
        Assert.AreEqual(10f, slider.Value);

        slider.Focus();
        DispatchKey(overlay, VirtualKeyRight);
        Assert.AreEqual(12f, slider.Value);
        DispatchKey(overlay, VirtualKeyPageDown);
        Assert.AreEqual(10f, slider.Value);
        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(30f, slider.Value);

        var sink = new RecordingDrawCommandSink();
        ui.Render(new DrawContext(sink));
        Assert.IsTrue(sink.FilledRoundedRectangles.Count > 0);
        Assert.IsTrue(sink.FilledCircles.Count > 0);
        PointF maxThumbCenter = sink.FilledCircles[^1];
        Assert.IsTrue(maxThumbCenter.X <= slider.Bounds.X + slider.Bounds.Width - 7f);

        slider.IsEnabled = false;
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 60, 20);
        DispatchKey(overlay, VirtualKeyLeft);
        Assert.AreEqual(30f, slider.Value);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task NumberBoxParsesInvariantTextRejectsInvalidInputAndUsesStepButtons()
    {
        CultureInfo? previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
        try
        {
            await using OverlayWindow overlay = await CreateOverlayAsync();
            using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
            NumberBox number = new()
            {
                Minimum = 0d,
                Maximum = 10d,
                Step = 2d,
                Value = 3d,
                Width = 160f,
                Height = 30f,
                HorizontalAlignment = UiHorizontalAlignment.Left,
                VerticalAlignment = UiVerticalAlignment.Top,
            };
            Canvas.SetLeft(number, 10f);
            Canvas.SetTop(number, 10f);
            int changes = 0;
            number.ValueChanged += (_, _) => changes++;
            ui.Root.Children.Add(number);
            ui.Render(new DrawContext());

            UiTextBox textBox = (UiTextBox)number.Children[0];
            textBox.Text = "4.5";
            Assert.AreEqual(4.5d, number.Value);
            Assert.AreEqual("4.5", textBox.Text);

            textBox.Text = "nope";
            Assert.AreEqual(4.5d, number.Value);
            Assert.AreEqual("4.5", textBox.Text);

            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
            textBox.Text = string.Empty;
            DispatchText(overlay, "-");
            Assert.AreEqual(4.5d, number.Value);
            Assert.AreEqual("-", textBox.Text);
            DispatchText(overlay, "6");
            Assert.AreEqual(0d, number.Value);
            Assert.AreEqual("0", textBox.Text);

            Click(overlay, 146, 20);
            Assert.AreEqual(2d, number.Value);

            Click(overlay, 20, 20);
            Assert.AreEqual(0d, number.Value);

            number.Value = 50d;
            Assert.AreEqual(10d, number.Value);
            Assert.AreEqual("10", textBox.Text);
            Assert.IsTrue(changes >= 4);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

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

    private static void DispatchText(OverlayWindow overlay, string text)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleTextInputEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleTextInputEvent");
        method.Invoke(overlay, [new Win32TextInputEvent(text, false)]);
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<RectF> FilledRoundedRectangles { get; } = [];

        public List<PointF> FilledCircles { get; } = [];

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
        {
            FilledRoundedRectangles.Add(rect);
            AddPrimitive();
        }

        public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => AddPrimitive();

        public void FillCircle(PointF center, float radius, BrushHandle brush)
        {
            FilledCircles.Add(center);
            AddPrimitive();
        }

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
