# GameOverlay.NET Concept Mapping

ModernOverlay preserves functional value, not API compatibility. The table below maps common GameOverlay.NET concepts to the current ModernOverlay surface.

| GameOverlay.NET concept | ModernOverlay equivalent |
|---|---|
| Transparent overlay window | `OverlayWindow.CreateAsync` with `TransparencyMode.Auto` or `DwmGlassFrame` |
| Click-through window | `OverlayInputMode.ClickThrough` |
| Interactive overlay | `OverlayInputMode.Interactive` plus `PointerMoved`/`PointerPressed`/`PointerReleased` |
| Sticky window | `OverlayWindowOptions.Target` with `WindowTarget.*` factories |
| Topmost overlay | `OverlayZOrder.TopMost` |
| Follow target z-order | `OverlayZOrder.FollowTarget` |
| FPS limiter | `FrameRateLimit.Fixed`, `DisplayDefault`, or `Unlimited` |
| Direct2D drawing surface | `overlay.Render += frame => ...` |
| Solid brushes | `OverlayResourceManager.CreateSolidBrush` |
| Linear gradients | `CreateLinearGradientBrush` |
| Fonts/text formats | `CreateFont` and `CreateTextLayout` |
| Text draw/measure | `frame.Draw.Text`, `frame.Measure.Text` |
| Images | `CreateImage` plus `frame.Draw.Image` |
| Geometry paths | `CreateGeometry` plus `Draw.Geometry`/`Fill.Geometry` |
| Dashed lines/shapes | `StrokeStyleHandle` overloads |
| Helper shapes | `Draw.Arrow`, `Draw.Crosshair`, `Draw.Box` |
| Window helper methods | `ModernOverlay.Win32.Win32WindowQuery`, `Win32WindowZOrder`, `Win32WindowEffects` |
| Native error inspection | `Win32NativeDiagnostics.LastFailure` |
| Frame diagnostics | `overlay.FrameStats`, `OverlayEventSource`, `OverlayEventSourceLogger` |

## Intentional Differences

- Public coordinates for drawing are DIPs; window bounds APIs explicitly distinguish pixels and DIPs.
- Resource handles are disposable descriptors. Native Direct2D/DirectWrite/WIC objects are internal backend realizations and are recreated after backend recreation.
- `DrawContext` instances supplied by `OverlayWindow.Render` are valid only during the render callback.
- Target tracking is composable through `WindowTarget` rather than a separate sticky-window type.
- Advanced or unsafe integration is not part of the core package. There is no injection, hook, protected-process bypass, anti-cheat bypass, or kernel integration.

## Current Compatibility Notes

- `Draw.Box` is a rectangle alias. Corner-only ESP-style boxes should be added as an explicit option or separate helper if required.
- DirectComposition and `UpdateLayeredWindow` are reserved for later backend work.
- Automatic animated-image timing is not implemented. Callers can draw an explicit WIC frame index.
- Direct2D backend registration is explicit for now: call `Direct2DOverlayBackend.Register()`.

