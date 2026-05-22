using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Hotkey Sample",
    Bounds = new WindowBounds(240, 240, 560, 220),
    InputMode = OverlayInputMode.ClickThrough,
    FrameRateLimit = FrameRateLimit.Fixed(30),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(new ColorRgba(0.35f, 0.6f, 1f, 0.9f));
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
using FontHandle smallFont = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 14));

int hotkeyPresses = 0;
string status = "Press Ctrl+Alt+O";
using IDisposable hotkey = overlay.Hotkeys.Register("ToggleInputMode", KeyGesture.CtrlAltO, () =>
{
    hotkeyPresses++;
    overlay.InputMode = overlay.InputMode == OverlayInputMode.ClickThrough
        ? OverlayInputMode.Interactive
        : OverlayInputMode.ClickThrough;
    status = $"Hotkey presses: {hotkeyPresses}";
});

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.RoundedRectangle(new RectF(24, 24, 512, 152), 12, 12, accent, 3f);
    frame.Draw.Text(status, font, white, new PointF(48, 58));
    frame.Draw.Text($"Input mode: {overlay.InputMode}", font, white, new PointF(48, 92));
    frame.Draw.Text("Ctrl+Alt+O toggles the overlay input mode.", smallFont, white, new PointF(48, 132));
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await overlay.RunAsync(cts.Token);
