using ModernOverlay;
using ModernOverlay.Direct2D;
using ModernOverlay.Win32;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Diagnostics Sample",
    Bounds = new WindowBounds(180, 180, 760, 400),
    FrameRateLimit = FrameRateLimit.Fixed(30),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using SolidBrushHandle accent = overlay.Resources.CreateSolidBrush(new ColorRgba(0.25f, 0.9f, 0.55f, 0.9f));
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Cascadia Mono", 15));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    Win32NativeFailureInfo? nativeFailure = Win32NativeDiagnostics.LastFailure;
    string nativeFailureText = nativeFailure is null
        ? "none"
        : nativeFailure.Win32Error is int win32Error
            ? $"{nativeFailure.Operation} Win32 {win32Error}"
            : $"{nativeFailure.Operation} HRESULT 0x{nativeFailure.HResult.GetValueOrDefault():X8}";

    frame.Draw.Rectangle(new RectF(20, 20, 720, 340), accent, 2f);
    frame.Draw.Text($"FPS: {overlay.FrameStats.CurrentFramesPerSecond:0.0} avg {overlay.FrameStats.AverageFramesPerSecond:0.0}", font, white, new PointF(36, 42));
    frame.Draw.Text($"Frame ms: {overlay.FrameStats.LastFrameDuration.TotalMilliseconds:0.00} avg {overlay.FrameStats.MovingAverageFrameDuration.TotalMilliseconds:0.00}", font, white, new PointF(36, 72));
    frame.Draw.Text($"Thread: {overlay.FrameStats.RenderThreadId}", font, white, new PointF(36, 102));
    frame.Draw.Text($"Commands: {overlay.FrameStats.CommandCount} primitives {overlay.FrameStats.PrimitiveCount}", font, white, new PointF(36, 132));
    frame.Draw.Text($"Transient text layouts: {overlay.FrameStats.TransientTextLayoutCount}", font, white, new PointF(36, 162));
    frame.Draw.Text($"Native resources: {overlay.FrameStats.NativeResourceCount}", font, white, new PointF(36, 192));
    frame.Draw.Text($"Backend: {overlay.BackendName} gen {overlay.FrameStats.BackendGeneration}", font, white, new PointF(36, 222));
    frame.Draw.Text($"HWND: 0x{overlay.Hwnd.Value:X}", font, white, new PointF(36, 252));
    frame.Draw.Text($"Target HWND: {(overlay.FrameStats.TargetHwnd.IsNull ? "none" : $"0x{overlay.FrameStats.TargetHwnd.Value:X}")}", font, white, new PointF(36, 282));
    frame.Draw.Text($"DPI scale: {overlay.FrameStats.DpiScale.X:0.##} x {overlay.FrameStats.DpiScale.Y:0.##}", font, white, new PointF(36, 312));
    frame.Draw.Text($"Last native failure: {nativeFailureText}", font, white, new PointF(36, 342));
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await overlay.RunAsync(cts.Token);
