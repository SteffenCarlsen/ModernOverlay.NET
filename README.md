# ModernOverlay

ModernOverlay is a Windows-only overlay library for modern .NET. It replaces the useful parts of the GameOverlay.NET programming model with a new Vortice + Direct2D/DirectWrite/WIC + Win32 implementation.

This package is not a drop-in GameOverlay.NET replacement. The API intentionally uses new names, explicit lifetimes, safer target tracking, and first-class diagnostics.

## Preview Status

This repository currently targets `net11.0-windows` on a .NET 11 preview SDK. APIs, package layout, backend registration, and packaging metadata may change before .NET 11 GA or before a documented `net10.0-windows` fallback is chosen.

## Quick Start

The current Direct2D backend is explicit. Reference `ModernOverlay.Direct2D` and register it before creating overlays:

```csharp
using ModernOverlay;
using ModernOverlay.Direct2D;

Direct2DOverlayBackend.Register();

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
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
};

await overlay.RunAsync();
```

Drawing coordinates are DIPs. `WindowBounds` represents physical pixels unless constructed through the DIP helpers.

## Documentation

- Quick start: `docs/quick-start.md`
- Installation: `docs/installation.md`
- GameOverlay.NET mapping: `docs/gameoverlay-migration.md`
- Window modes: `docs/window-modes.md`
- Transparency validation: `docs/transparency-validation.md`
- Target tracking: `docs/target-tracking.md`
- DPI and multi-monitor: `docs/dpi-and-multi-monitor.md`
- Drawing primitives: `docs/drawing-primitives.md`
- Resource lifetime: `docs/resource-lifetime.md`
- Device loss and recreation: `docs/device-recreation.md`
- Integration boundary: `docs/integration-boundary.md`
- Troubleshooting: `docs/troubleshooting.md`
- Performance guide: `docs/performance-guide.md`
- DirectComposition decision note: `docs/directcomposition-spike.md`
- Feature completeness: `docs/feature-completeness.md`
- Release validation checklist: `docs/release-validation-checklist.md`

## Safety Boundary

ModernOverlay is standalone and cooperative by design. It does not implement stealth behavior, anti-cheat bypass, protected-process bypass, capture-protection bypass, or kernel-level integration.
