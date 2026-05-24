using ModernOverlay.Rendering;
using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiColorPickerTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ValueSetterUpdatesPreviewAndHexReadout()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ColorPicker picker = CreateColorPicker();
        int changes = 0;
        picker.ColorChanged += (_, _) => changes++;
        ui.Root.Children.Add(picker);
        ui.Render(new DrawContext());

        ColorRgba selected = ColorRgba.FromBytes(32, 64, 128, 192);
        picker.Value = selected;
        ui.Render(new DrawContext());

        Assert.AreEqual(1, changes);

        var sink = new RecordingDrawCommandSink();
        ui.Render(new DrawContext(sink));

        Assert.IsTrue(sink.FilledRoundedRectangles.Count > 0);
        Assert.AreEqual(selected, sink.FilledRoundedRectangles[0].Brush.Color);
        Assert.Contains("#204080 (192)", sink.TextRuns);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerSelectionUpdatesColorValueAndRaisesCallback()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ColorPicker picker = CreateColorPicker();
        ui.Root.Children.Add(picker);
        ui.Render(new DrawContext());
        int changes = 0;
        picker.ColorChanged += (_, _) => changes++;

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 60, 60);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 60, 60);
        Assert.AreNotEqual(ColorRgba.White, picker.Value);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 67, 138);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 67, 138);

        Assert.AreEqual(0.5f, picker.Value.A, 0.02f);
        Assert.IsTrue(changes >= 2);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 280, 220),
        });

    private static ColorPicker CreateColorPicker()
    {
        ColorPicker picker = new()
        {
            Width = 180f,
            Height = 174f,
        };
        Canvas.SetLeft(picker, 10f);
        Canvas.SetTop(picker, 10f);
        return picker;
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<(RectF Rect, SolidBrushHandle Brush)> FilledRoundedRectangles { get; } = [];

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
        {
            if (brush is SolidBrushHandle solid)
            {
                FilledRoundedRectangles.Add((rect, solid));
            }

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
            => AddPrimitive();

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
        {
            TextRuns.Add(text);
            AddPrimitive();
        }

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => AddPrimitive();

        public SizeF MeasureText(string text, FontHandle font) => new(text.Length * 7f, 14f);

        public SizeF MeasureTextLayout(TextLayoutHandle layout) => new(0f, 0f);

        private void AddPrimitive()
        {
            CommandCount++;
            PrimitiveCount++;
        }
    }
}
