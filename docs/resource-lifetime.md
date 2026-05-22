# Resource Lifetime

Public resource handles are device-independent descriptors. Native Direct2D, DirectWrite, and WIC realizations are internal backend resources tied to a backend generation.

## Handles

Supported public handles:

- `SolidBrushHandle`
- `LinearGradientBrushHandle`
- `FontHandle`
- `ImageHandle`
- `GeometryPath`
- `StrokeStyleHandle`
- `TextLayoutHandle`

All handles are disposable and validate use-after-dispose.

## Creation

Create resources outside the render hot path:

```csharp
using SolidBrushHandle brush = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
```

Set `RejectResourceCreationDuringRender` to catch accidental resource allocation inside render callbacks.

## Native Realizations

The Direct2D backend realizes brushes, stroke styles, text layouts, images, and geometry paths lazily. Backend recreation disposes native caches and recreates native resources from public descriptors as needed.

## Leak Diagnostics

`OverlayResourceManager.CreateLeakReport()` returns live handles with kind, id, generation, allocation site, and native realization snapshots. It also emits an EventSource resource leak event when live resources remain.

