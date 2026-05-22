using ModernOverlay;
using ModernOverlay.Direct2D;
using ModernOverlay.Win32;

Direct2DOverlayBackend.Register();

using Win32OverlayWindow target = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
    ClassName: $"ModernOverlayStickyTarget_{Guid.NewGuid():N}",
    Title: "ModernOverlay Sticky Target Host",
    X: 160,
    Y: 140,
    Width: 480,
    Height: 260,
    ClickThrough: false,
    TopMost: false,
    ToolWindow: false));
target.Show();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Sticky Target Sample",
    IsVisible = true,
    Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)),
    TargetTrackingInterval = TimeSpan.FromMilliseconds(16),
    ZOrder = OverlayZOrder.FollowTarget,
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(new ColorRgba(0.1f, 0.65f, 1f, 0.85f));
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 16));
using StrokeStyleHandle dash = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.Dash });

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Rectangle(new RectF(18, 18, 444, 224), accent, dash, 2f);
    frame.Draw.Crosshair(new PointF(240, 130), 28, white, 2f);
    frame.Draw.Text("Following owned target HWND", font, white, new PointF(32, 32));
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
Task targetMotion = MoveTargetAsync(target, cts.Token);

try
{
    await overlay.RunAsync(cts.Token);
}
finally
{
    await targetMotion.ConfigureAwait(false);
}

static async Task MoveTargetAsync(Win32OverlayWindow target, CancellationToken cancellationToken)
{
    int step = 0;
    while (!cancellationToken.IsCancellationRequested)
    {
        int x = 160 + step % 6 * 20;
        int y = 140 + step % 4 * 14;
        target.SetBounds(x, y, 480, 260);
        step++;

        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
