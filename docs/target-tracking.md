# Target Tracking

Target tracking replaces the older sticky-window concept with composable target descriptors.

## Target Sources

Supported target descriptors:

- `WindowTarget.FromHwnd`
- `WindowTarget.ByProcessId`
- `WindowTarget.ByProcessName`
- `WindowTarget.ByTitle`
- `WindowTarget.ByWindowTitle`
- `WindowTarget.ByClassName`
- `WindowTarget.ForegroundWindow`
- `WindowTarget.FromProvider`

Discovery-backed targets reacquire by default. Provider-backed targets call the supplied provider during tracking passes.

## Bounds Modes

`TargetBoundsMode.Window` tracks the whole window rectangle.

`TargetBoundsMode.ClientArea` tracks the client area.

`WithCustomBounds` lets callers supply a resolver when the desired overlay region is smaller than the target window.

## Tracking Cadence

`OverlayWindowOptions.TargetTrackingInterval` defaults to 33 ms. Use `TimeSpan.Zero` for every-frame tracking, or a higher interval to reduce query overhead when exact tracking is not required.

## Loss, Reacquire, and Minimize

`TargetLost` fires when a previously resolved target disappears. `TargetReacquired` fires when it resolves again.

`TargetMinimizedPolicy.HideOverlay` hides the overlay while the target is minimized. `TargetMinimizedPolicy.PauseRendering` skips rendering without changing overlay visibility.

## Limits

Target following is cooperative and best-effort. ModernOverlay does not bypass elevated-process boundaries, secure desktops, protected content, anti-cheat systems, or exclusive fullscreen limitations.

