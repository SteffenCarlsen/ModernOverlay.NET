using ModernOverlay;

byte[] samplePng =
[
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
    0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D, 0x00, 0x00,
    0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
];

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Image Sample",
    Bounds = new WindowBounds(220, 220, 420, 260),
    FrameRateLimit = FrameRateLimit.Fixed(30),
});

using ImageHandle image = overlay.Resources.CreateImage(samplePng);
using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(new ColorRgba(0.2f, 0.75f, 1f, 0.9f));
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 16));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Rectangle(new RectF(24, 24, 372, 192), accent, 2f);
    frame.Draw.Image(image, new RectF(52, 56, 96, 96), opacity: 1f, interpolationMode: ImageInterpolationMode.NearestNeighbor);
    frame.Draw.Image(image, new RectF(172, 56, 96, 96), opacity: 0.55f);
    frame.Draw.Text("PNG from memory, scaled through WIC + Direct2D", font, white, new PointF(52, 172));
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await overlay.RunAsync(cts.Token);
