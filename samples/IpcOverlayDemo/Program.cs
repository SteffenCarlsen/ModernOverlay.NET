using ModernOverlay;
using ModernOverlay.Integration;

const string pipeName = "ModernOverlay.IpcOverlayDemo";
const string commandToken = "modern-overlay-local-demo";

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay IPC Demo",
    Bounds = WindowBounds.FromPixels(120, 120, 760, 420),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    IsVisible = true,
});

using var host = new CooperativeOverlayCommandHost(overlay.Resources);
host.Handle(OverlayCommandMessage.Start(
    [
        OverlayDrawCommand.Clear(ColorRgba.Transparent),
        OverlayDrawCommand.TextRun("Waiting for SampleOwnedHost...", new PointF(24, 24), ColorRgba.White),
        OverlayDrawCommand.Rectangle(new RectF(20, 60, 340, 96), ColorRgba.FromBytes(80, 180, 255), 2),
    ]));

var server = new NamedPipeOverlayCommandServer(
    pipeName,
    (message, _) => ValueTask.FromResult(host.Handle(message)),
    NamedPipeOverlayCommandSecurity.RequireCommandToken(commandToken));

overlay.Render += host.Render;

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
Task serverTask = server.RunAsync(cts.Token);

try
{
    await overlay.RunAsync(cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
}

await serverTask;
