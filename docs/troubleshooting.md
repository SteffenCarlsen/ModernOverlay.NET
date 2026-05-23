# Troubleshooting

## Overlay Does Not Appear

- Confirm the app references `ModernOverlay.Direct2D` so the backend assembly is present for auto-discovery before `OverlayWindow.CreateAsync`.
- Check that bounds are non-empty and on a visible monitor. Negative coordinates are valid for multi-monitor layouts, but easy to misread during testing.
- Use `overlay.FrameStats` or `DiagnosticsOverlay` to confirm frames are rendering.
- Inspect `Win32NativeDiagnostics.LastFailure` for the latest surfaced Win32/HRESULT failure.

## Click-through Does Not Work

- `OverlayInputMode.ClickThrough` sets `WS_EX_TRANSPARENT`; `Interactive` removes it.
- Some target windows, elevated processes, secure desktops, UAC prompts, exclusive fullscreen modes, capture protection, or protected content can prevent normal overlay behavior. ModernOverlay reports and documents those limits rather than trying to bypass them.
- After changing `overlay.InputMode`, keep callbacks short and avoid blocking the owner thread.

## Target Tracking Fails

- Prefer owned HWNDs or authorized/cooperative targets when validating behavior.
- `WindowTarget.ByTitle` uses ordinal-insensitive contains matching by default. Pass `MatchMode.Exact` when an exact title match is required.
- `FollowTarget` z-order is best-effort. Windows can reorder overlays due to activation, other topmost windows, virtual desktops, secure desktops, and shell behavior.
- `TargetTrackingInterval` trades overhead for latency. The default is 33 ms; use `TimeSpan.Zero` only when every-frame target polling is needed.

## FPS Looks Capped Or Inconsistent

- `FrameRateLimit.Unlimited` disables ModernOverlay's scheduled frame delay, but it does not override backend presentation or compositor pacing.
- If an unlimited overlay still sits near the monitor refresh rate, set `OverlayWindowOptions.PresentMode = PresentMode.Immediate` and compare `FrameStats.PresentDuration` in `DiagnosticsOverlay`.
- `PresentMode.BackendDefault` is intentionally conservative. With the current Direct2D HWND backend, it can be paced by the normal HWND/compositor presentation path.
- `FrameStats.CurrentFramesPerSecond` is calculated from completed-frame cadence, including presentation. Compare it with `AverageFramesPerSecond`, `LastFrameDuration`, `RenderDuration`, and `PresentDuration` when diagnosing timing.

## DPI Or Multi-monitor Behavior Looks Wrong

- Drawing uses DIPs. `WindowBounds` is physical pixels unless `FromDips` or `SetBoundsDips` is used.
- Per Monitor V2 DPI is requested by default. `WM_DPICHANGED` applies Windows' suggested physical bounds and resizes the backend.
- Manual multi-monitor visual validation is still required before release across mixed-DPI and negative-coordinate layouts.

## Transparency Looks Wrong

- The current default maps `TransparencyMode.Auto` to DWM glass frame extension.
- Direct2D HWND transparency currently uses a transparent black color key. Pure-black overlay content will also become transparent until a true per-pixel alpha backend exists.
- `TransparencyMode.LayeredWindowAttributes` is wired for global alpha behavior plus the same transparent black color key, not proof of per-pixel Direct2D alpha.
- `UpdateLayeredWindow` and `DirectComposition` currently fall back to DWM glass/color-key transparency and emit `BackendFallback` diagnostics until their dedicated backends exist.
- See `docs/transparency-validation.md` for the manual validation checklist.

## Render Callback Exceptions

Set `OverlayWindowOptions.ExceptionPolicy`:

- `Continue`: log and try the next frame.
- `PauseOverlay`: pause after surfacing the exception.
- `FailFast`: terminate the process after logging.

Use `RejectResourceCreationDuringRender` to catch accidental hot-path resource allocation through the overlay-owned resource manager.
