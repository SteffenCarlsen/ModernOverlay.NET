using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow dwmGlass = await CreateOverlayAsync(
    "DWM glass frame",
    new WindowBounds(120, 120, 420, 180),
    TransparencyMode.DwmGlassFrame);
await using OverlayWindow layered = await CreateOverlayAsync(
    "Layered alpha",
    new WindowBounds(120, 330, 420, 180),
    TransparencyMode.LayeredWindowAttributes);

using SolidBrushHandle white = dwmGlass.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle cyan = dwmGlass.Resources.CreateSolidBrush(new ColorRgba(0.1f, 0.7f, 1f, 0.9f));
using FontHandle font = dwmGlass.Resources.CreateFont(new FontOptions("Segoe UI", 18));
using SolidBrushHandle layeredWhite = layered.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle layeredAccent = layered.Resources.CreateSolidBrush(new ColorRgba(1f, 0.5f, 0.2f, 0.9f));
using FontHandle layeredFont = layered.Resources.CreateFont(new FontOptions("Segoe UI", 18));

dwmGlass.Render += frame => DrawValidationFrame(frame, "DWM glass", white, cyan, font);
layered.Render += frame => DrawValidationFrame(frame, "Layered attributes", layeredWhite, layeredAccent, layeredFont);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
await Task.WhenAll(
    dwmGlass.RunAsync(cts.Token).AsTask(),
    layered.RunAsync(cts.Token).AsTask());

static async ValueTask<OverlayWindow> CreateOverlayAsync(string title, WindowBounds bounds, TransparencyMode mode)
{
    return await OverlayWindow.CreateAsync(new OverlayWindowOptions
    {
        Title = $"ModernOverlay Transparency - {title}",
        Bounds = bounds,
        TransparencyMode = mode,
        InputMode = OverlayInputMode.ClickThrough,
        FrameRateLimit = FrameRateLimit.Fixed(30),
    });
}

static void DrawValidationFrame(DrawContext frame, string label, BrushHandle text, BrushHandle accent, FontHandle font)
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.RoundedRectangle(new RectF(18, 18, 384, 124), 18, 18, accent, 4f);
    frame.Draw.Crosshair(new PointF(350, 80), 24, text, 2f);
    frame.Draw.Text(label, font, text, new PointF(42, 56));
}
