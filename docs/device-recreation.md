# Device Loss and Recreation

ModernOverlay exposes a deterministic recreation path for resize, manual recreation, and backend-generation validation.

## Manual Recreation

```csharp
await overlay.RecreateAsync();
```

Manual recreation:

- raises `DeviceLost`;
- emits an EventSource device-lost event;
- advances the public resource generation;
- recreates the backend against the existing HWND;
- raises `DeviceRestored`;
- emits an EventSource device-restored event.

## Resource Behavior

Public handles remain descriptors. Native realizations are disposed with the old backend generation and recreated lazily when the resource is used again.

```csharp
overlay.DeviceLost += (_, _) => Console.WriteLine("overlay backend lost");
overlay.DeviceRestored += (_, _) => Console.WriteLine("overlay backend restored");
```

## Automatic Backend Recreation

The render backend can also request recreation after a frame. The Direct2D HWND backend translates `D2DERR_RECREATE_TARGET` from `EndDraw` into a recreate request instead of surfacing it as a user render exception. `OverlayWindow` responds by raising `DeviceLost`, advancing resource generation, recreating the backend against the existing HWND, and raising `DeviceRestored`. The failed frame is not counted as presented; subsequent frames can continue with recreated native resources.

## Current Limit

The implementation has deterministic manual and backend-requested recreation paths. A real hardware device-removal or driver-reset event is still not forced automatically because behavior varies by GPU, driver, and Direct2D backend. Treat manual recreation, resize stress, and simulated recreate-target coverage as managed/native lifecycle evidence, then validate real device-loss behavior on representative hardware before calling device-loss handling complete.
