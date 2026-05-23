using ModernOverlay;

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Text Layout Sample",
    Bounds = new WindowBounds(140, 140, 560, 260),
    FrameRateLimit = FrameRateLimit.Fixed(30),
});

using SolidBrushHandle textBrush = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle outlineBrush = overlay.Resources.CreateSolidBrush(new ColorRgba(0.2f, 0.7f, 1f, 0.85f));
using FontHandle titleFont = overlay.Resources.CreateFont(new FontOptions("Segoe UI Semibold", 22));
using FontHandle bodyFont = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 15));
using TextLayoutHandle title = overlay.Resources.CreateTextLayout(
    "Reusable DirectWrite text layout",
    titleFont,
    new TextLayoutOptions { MaxWidth = 460, MaxHeight = 48, HorizontalAlignment = TextHorizontalAlignment.Center });
using TextLayoutHandle body = overlay.Resources.CreateTextLayout(
    "This sample exercises constrained layout, wrapping, measurement, and cached text layout drawing through the public resource manager.",
    bodyFont,
    new TextLayoutOptions { MaxWidth = 460, MaxHeight = 120, Wrapping = TextWrapping.WholeWord });

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.RoundedRectangle(new RectF(32, 28, 496, 190), 14, 14, outlineBrush, 2f);
    frame.Draw.Text(title, textBrush, new PointF(50, 48));
    frame.Draw.Text(body, textBrush, new PointF(50, 104));

    SizeF measured = frame.Measure.Text(body);
    frame.Draw.Rectangle(new RectF(50, 104, measured.Width, measured.Height), outlineBrush, 1f);
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await overlay.RunAsync(cts.Token);
