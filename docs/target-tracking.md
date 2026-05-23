# Target Tracking

Target tracking replaces the older sticky-window concept with composable target descriptors.

## Target Sources

Supported target descriptors:

- `WindowTarget.FromHwnd`
- `WindowTarget.ByProcessId`
- `WindowTarget.ByProcessName`
- `WindowTarget.ByTitle`
- `WindowTarget.ByClassName`
- `WindowTarget.ForegroundWindow`
- `WindowTarget.FromProvider`

Discovery-backed targets reacquire by default. Provider-backed targets call the supplied provider during tracking passes.

```csharp
OverlayTarget target = WindowTarget.ByTitle("Untitled - Notepad", MatchMode.Exact)
    .WithBoundsMode(TargetBoundsMode.ClientArea);
```

## Bounds Modes

`TargetBoundsMode.Window` tracks the whole window rectangle.

`TargetBoundsMode.ClientArea` tracks the client area.

`WithCustomBounds` lets callers supply a resolver when the desired overlay region is smaller than the target window.

```csharp
OverlayTarget target = WindowTarget.FromHwnd(hwnd)
    .WithCustomBounds(_ => WindowBounds.FromPixels(100, 100, 400, 240));
```

## Tracking Cadence

`OverlayWindowOptions.TargetTrackingInterval` defaults to 33 ms. Use `TimeSpan.Zero` for every-frame tracking, or a higher interval to reduce query overhead when exact tracking is not required.

Target descriptors can also carry their own cadence with `WithTrackingInterval`. A target-level interval overrides the window-level option for that overlay:

```csharp
var target = WindowTarget.ByTitle("Notepad")
    .WithBoundsMode(TargetBoundsMode.ClientArea)
    .WithTrackingInterval(TimeSpan.FromMilliseconds(33));
```

`OverlayWindow.Options` preserves the caller's requested fallback bounds and configured window-level tracking interval. Runtime diagnostics such as `FrameStats.WindowBounds`, `FrameStats.TargetBounds`, and target events expose the effective resolved bounds after target tracking.

## Loss, Reacquire, and Minimize

`TargetLost` fires when a previously resolved target disappears. `TargetReacquired` fires when it resolves again.

```csharp
overlay.TargetLost += (_, args) => Console.WriteLine($"lost {args.TargetHwnd}");
overlay.TargetReacquired += (_, args) => Console.WriteLine($"reacquired {args.TargetHwnd}");
```

`TargetMinimizedPolicy.HideOverlay` hides the overlay while the target is minimized. `TargetMinimizedPolicy.PauseRendering` skips rendering without changing overlay visibility.

## Limits

Target following is cooperative and best-effort. ModernOverlay does not bypass elevated-process boundaries, secure desktops, protected content, anti-cheat systems, or exclusive fullscreen limitations.
