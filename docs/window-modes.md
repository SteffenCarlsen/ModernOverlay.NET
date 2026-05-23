# Window Modes

ModernOverlay creates borderless top-level Win32 popup windows intended for desktop overlays.

## Input Modes

`OverlayInputMode.ClickThrough` is the default. It keeps `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` so mouse input passes through to windows below.

`OverlayInputMode.Interactive` removes the click-through style while keeping no-activate behavior. Pointer events are surfaced through `PointerMoved`, `PointerPressed`, `PointerReleased`, and `PointerWheel`.

The alpha interaction model intentionally covers mouse movement, button presses/releases, and vertical/horizontal wheel deltas. Pointer capture, raw input, gestures, and selective per-pixel hit testing remain future 1.0 design work until a real scenario needs them.

```csharp
overlay.InputMode = OverlayInputMode.Interactive;
overlay.PointerPressed += (_, args) => Console.WriteLine(args.Position);
overlay.PointerWheel += (_, args) => Console.WriteLine(args.WheelDelta);
```

## Z-Order Modes

`OverlayZOrder.Normal` removes topmost behavior.

`OverlayZOrder.TopMost` applies topmost placement.

`OverlayZOrder.FollowTarget` attempts to place the overlay directly above the resolved target HWND during target tracking. This is best-effort because Windows shell state, elevated windows, secure desktops, topmost windows, virtual desktops, and fullscreen modes can override normal z-order expectations.

```csharp
overlay.ZOrder = OverlayZOrder.TopMost;
overlay.InputMode = OverlayInputMode.ClickThrough;
```

## Capture Exclusion

`OverlayWindowOptions.ExcludeFromCapture` applies `SetWindowDisplayAffinity(..., WDA_EXCLUDEFROMCAPTURE)` when the Win32 overlay is shown. It is off by default and is intended for future capture-backed overlay work where the overlay must not appear in its own desktop-duplication frame.

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    IsVisible = true,
    ExcludeFromCapture = true,
});
```

When enabled, showing the overlay fails if Windows rejects the display-affinity request. Use `ModernOverlay.Windows.WindowEffects.TryExcludeFromCapture(...)` for best-effort helper usage outside `OverlayWindowOptions`.

This is not stream-proofing, bypass behavior, or a privacy guarantee. It is a supported Win32 display-affinity request whose behavior depends on Windows version, compositor/session state, and the capture technology being used.

## Transparency Modes

`TransparencyMode.Auto` currently maps to DWM frame extension plus transparent black color-keying for the Direct2D HWND backend.

`TransparencyMode.DwmGlassFrame` explicitly uses DWM frame extension.

`TransparencyMode.LayeredWindowAttributes` applies layered-window alpha plus transparent black color-keying.

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Transparency = TransparencyMode.LayeredWindowAttributes,
});
```

`TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` are reserved for later backend work. Requesting either mode falls back to DWM frame extension and emits a `BackendFallback` diagnostic event so callers get a usable overlay plus an observable warning.

This fallback is a preview compromise. True CPU-copy layered-window alpha and DirectComposition/DXGI per-pixel alpha remain explicit backend work before the transparency API can be treated as stable.

Use `samples/TransparencyValidationOverlay` with `docs/transparency-validation.md` before claiming a release works across a specific Windows/GPU/DPI setup.
