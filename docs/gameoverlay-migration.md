# GameOverlay.NET Concept Mapping

ModernOverlay preserves functional value, not API compatibility. The table below maps common GameOverlay.NET concepts to the current ModernOverlay surface.

| GameOverlay.NET concept | ModernOverlay equivalent |
|---|---|
| Transparent overlay window | `OverlayWindow.CreateAsync` with `TransparencyMode.Auto` or `DwmGlassFrame` |
| Click-through window | `OverlayInputMode.ClickThrough` |
| Interactive overlay | `OverlayInputMode.Interactive` plus `PointerMoved`/`PointerPressed`/`PointerReleased`/`PointerWheel` |
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
| Helper shapes | `Draw.Arrow`, `Draw.Crosshair`, `Draw.Box`, `Draw.CornerBox` |
| Window helper methods | `ModernOverlay.Windows.WindowQuery`, `WindowZOrder`, `WindowEffects`; lower-level `ModernOverlay.Win32.Win32*` helpers remain available |
| Native error inspection | `Win32NativeDiagnostics.LastFailure` |
| Frame diagnostics | `overlay.FrameStats`, `OverlayEventSource`, `OverlayEventSourceLogger` |

## Intentional Differences

- Public coordinates for drawing are DIPs; window bounds APIs explicitly distinguish pixels and DIPs.
- Resource handles are disposable descriptors. Native Direct2D/DirectWrite/WIC objects are internal backend realizations and are recreated after backend recreation.
- `DrawContext` instances supplied by `OverlayWindow.Render` are valid only during the render callback.
- Target tracking is composable through `WindowTarget` rather than a separate sticky-window type.
- Advanced or unsafe integration is not part of the core package. There is no injection, hook, protected-process bypass, anti-cheat bypass, or kernel integration.

## Current Compatibility Notes

- `Draw.Box` is a full rectangle helper. Use `Draw.CornerBox` for corner-only ESP-style boxes.
- DirectComposition and `UpdateLayeredWindow` are reserved for later backend work and currently fall back to the DWM-glass transparency path with diagnostics.
- Automatic animated-image timing is not implemented. Callers can draw an explicit WIC frame index.
- Direct2D backend registration is automatic when the `ModernOverlay.Direct2D` assembly is present. `Direct2DOverlayBackend.Register()` remains available for explicit startup flows.
- `ModernOverlay.Windows` now exposes spec-shaped helper facades backed by the Win32 implementation. Use `ModernOverlay.Win32` directly only when you need the lower-level native-flavored names.
