using ModernOverlay;

await using OverlayWindow dwmGlass = await CreateOverlayAsync(
    "DWM glass frame",
    new WindowBounds(120, 120, 420, 180),
    TransparencyMode.DwmGlassFrame);
await using OverlayWindow layered = await CreateOverlayAsync(
    "Layered alpha",
    new WindowBounds(120, 330, 420, 180),
    TransparencyMode.LayeredWindowAttributes);
await using OverlayWindow updateFallback = await CreateOverlayAsync(
    "UpdateLayeredWindow fallback",
    new WindowBounds(580, 120, 420, 180),
    TransparencyMode.UpdateLayeredWindow);
await using OverlayWindow compositionFallback = await CreateOverlayAsync(
    "DirectComposition fallback",
    new WindowBounds(580, 330, 420, 180),
    TransparencyMode.DirectComposition);

var resources = new List<IDisposable>();
AttachFrame(dwmGlass, "DWM glass", new ColorRgba(0.1f, 0.7f, 1f, 0.9f), resources);
AttachFrame(layered, "Layered attributes", new ColorRgba(1f, 0.5f, 0.2f, 0.9f), resources);
AttachFrame(updateFallback, "Update fallback", new ColorRgba(0.7f, 0.9f, 0.2f, 0.9f), resources);
AttachFrame(compositionFallback, "Composition fallback", new ColorRgba(0.8f, 0.4f, 1f, 0.9f), resources);

try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
    await Task.WhenAll(
        dwmGlass.RunAsync(cts.Token).AsTask(),
        layered.RunAsync(cts.Token).AsTask(),
        updateFallback.RunAsync(cts.Token).AsTask(),
        compositionFallback.RunAsync(cts.Token).AsTask());
}
finally
{
    foreach (IDisposable resource in resources)
    {
        resource.Dispose();
    }
}

static void AttachFrame(OverlayWindow overlay, string label, ColorRgba accentColor, ICollection<IDisposable> resources)
{
    SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
    SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(accentColor);
    FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
    resources.Add(white);
    resources.Add(accent);
    resources.Add(font);
    overlay.Render += frame => DrawValidationFrame(frame, label, white, accent, font);
}

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
