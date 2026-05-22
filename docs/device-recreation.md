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

## Current Limit

The implementation has a deterministic manual recreation path. A hardware device-removal simulation is not forced automatically because behavior varies by GPU, driver, and Direct2D backend. Treat manual recreation and resize stress as coverage for the managed/native lifecycle, then validate real device-loss behavior on representative hardware before release.

