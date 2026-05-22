# Window Modes

ModernOverlay creates borderless top-level Win32 popup windows intended for desktop overlays.

## Input Modes

`OverlayInputMode.ClickThrough` is the default. It keeps `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` so mouse input passes through to windows below.

`OverlayInputMode.Interactive` removes the click-through style while keeping no-activate behavior. Pointer events are surfaced through `PointerMoved`, `PointerPressed`, and `PointerReleased`.

## Z-Order Modes

`OverlayZOrder.Normal` removes topmost behavior.

`OverlayZOrder.TopMost` applies topmost placement.

`OverlayZOrder.FollowTarget` attempts to place the overlay directly above the resolved target HWND during target tracking. This is best-effort because Windows shell state, elevated windows, secure desktops, topmost windows, virtual desktops, and fullscreen modes can override normal z-order expectations.

## Transparency Modes

`TransparencyMode.Auto` currently maps to DWM frame extension for the Direct2D HWND backend.

`TransparencyMode.DwmGlassFrame` explicitly uses DWM frame extension.

`TransparencyMode.LayeredWindowAttributes` applies layered-window alpha.

`TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` are reserved for later backend work and intentionally throw today.

Use `samples/TransparencyValidationOverlay` with `docs/transparency-validation.md` before claiming a release works across a specific Windows/GPU/DPI setup.

