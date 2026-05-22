using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Basic Sample",
    Bounds = new WindowBounds(100, 100, 640, 360),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Text("ModernOverlay", font, white, new PointF(24, 24));
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await overlay.RunAsync(cts.Token);
