using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Input Mode Sample",
    Bounds = new WindowBounds(220, 220, 520, 200),
    InputMode = OverlayInputMode.ClickThrough,
    FrameRateLimit = FrameRateLimit.Fixed(30),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(new ColorRgba(1f, 0.55f, 0.15f, 0.9f));
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
PointF lastPointer = new(float.NaN, float.NaN);
int pointerPresses = 0;

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
Task toggleTask = ToggleInputModeAsync(overlay, cts.Token);

overlay.PointerPressed += (_, args) =>
{
    lastPointer = args.Position;
    pointerPresses++;
};

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.RoundedRectangle(new RectF(24, 24, 472, 132), 12, 12, accent, 3f);
    frame.Draw.Text($"Current input mode: {overlay.InputMode}", font, white, new PointF(48, 58));
    string pointerText = float.IsNaN(lastPointer.X)
        ? "Pointer presses: none"
        : $"Pointer presses: {pointerPresses} at {lastPointer.X:0}, {lastPointer.Y:0}";
    frame.Draw.Text(pointerText, font, white, new PointF(48, 92));
};

try
{
    await overlay.RunAsync(cts.Token);
}
finally
{
    await toggleTask.ConfigureAwait(false);
}

static async Task ToggleInputModeAsync(OverlayWindow overlay, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        overlay.InputMode = overlay.InputMode == OverlayInputMode.ClickThrough
            ? OverlayInputMode.Interactive
            : OverlayInputMode.ClickThrough;

        try
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
