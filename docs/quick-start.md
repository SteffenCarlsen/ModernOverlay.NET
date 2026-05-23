# Quick Start

ModernOverlay is a Windows-only, Vortice-backed overlay library. It currently targets `net11.0-windows`, which is a preview runtime in this repository, so consumers should expect preview SDK/tooling churn until the target reaches GA. `main` does not carry a checked-in `net10.0-windows` fallback.

This is not a drop-in GameOverlay.NET package. The API keeps the useful model of transparent immediate-mode overlays, but uses new names, explicit lifetimes, and safer target-tracking/diagnostic surfaces.

## Minimal Overlay

Applications can reference the preview `ModernOverlay` package directly. It includes the current Direct2D backend assembly for the common path, and the facade auto-discovers the backend before creating the first overlay:

```csharp
using ModernOverlay;
using ModernOverlay.Drawing;
using ModernOverlay.Windows;

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Demo",
    Bounds = WindowBounds.FromPixels(100, 100, 640, 360),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Text("Hello overlay", font, white, new PointF(24, 24));
    frame.Draw.Rectangle(new RectF(20, 60, 240, 120), white, 2f);
};

await overlay.RunAsync();
```

Drawing coordinates are DIPs. `WindowBounds` is physical pixels unless constructed with `WindowBounds.FromDips`.

## Sticky Target Overlay

Use `WindowTarget` to follow a known window, process, title, class, foreground window, or custom provider. `FollowTarget` uses best-effort Win32 z-order placement and cannot guarantee ordering across UAC prompts, secure desktops, virtual desktops, or other topmost windows.

```csharp
var target = WindowTarget.ByTitle("Notepad")
    .WithBoundsMode(TargetBoundsMode.ClientArea)
    .WithTrackingInterval(TimeSpan.FromMilliseconds(33));

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Target = target,
    ZOrder = OverlayZOrder.FollowTarget,
    InputMode = OverlayInputMode.ClickThrough,
});
```

For deterministic local validation, see `samples/StickyWindowOverlay` or its capability alias `samples/StickyTargetOverlay`, which follows an owned test HWND instead of a third-party process.

## Interaction

Click-through overlays set `WS_EX_TRANSPARENT` so mouse input passes to windows below. Interactive overlays remove that style and expose pointer events:

```csharp
overlay.InputMode = OverlayInputMode.Interactive;
overlay.PointerPressed += (_, args) =>
{
    PointF positionInDips = args.Position;
};

overlay.PointerWheel += (_, args) =>
{
    int wheelDelta = args.WheelDelta;
};
```

Global hotkeys are available through `overlay.Hotkeys`. Hotkey and pointer callbacks run on the overlay owner thread, so keep them short.
