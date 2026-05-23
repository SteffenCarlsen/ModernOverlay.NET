# Source Projects

The source tree is split by package and responsibility. The alpha package shape is intentionally explicit so backend and integration boundaries stay visible while the API is still preview.

## Package Map

| Project | Package | Purpose |
|---|---|---|
| `ModernOverlay` | `ModernOverlay` | Main facade, overlay lifecycle, drawing/window API namespaces, target tracking, public options, and render backend registry. |
| `ModernOverlay.Direct2D` | `ModernOverlay.Direct2D` | Current Direct2D/DirectWrite/WIC renderer backend. The main package bundles this assembly for the preview common path. |
| `ModernOverlay.Win32` | `ModernOverlay.Win32` | Low-level Win32 windowing, z-order, style, query, display-affinity, and native diagnostics helpers. |
| `ModernOverlay.Diagnostics` | `ModernOverlay.Diagnostics` | EventSource logging bridge and diagnostics helpers. |
| `ModernOverlay.Integration` | `ModernOverlay.Integration` | Optional cooperative named-pipe command protocol for owned hosts. |
| `ModernOverlay.Integration.Experimental` | source-only | Contract-only experimental provider boundary. It is not packed for alpha. |

## Common Development Paths

1. Overlay lifecycle: start in `ModernOverlay/OverlayWindow.cs`.
2. Window options, target descriptors, bounds, DPI, and helper facades: start in `ModernOverlay/Windows`.
3. Drawing/resource API: start in `ModernOverlay/Drawing`.
4. Backend contracts: start in `ModernOverlay/Rendering`.
5. Direct2D renderer behavior: start in `ModernOverlay.Direct2D`.
6. Win32 window behavior: start in `ModernOverlay.Win32`.
7. Cooperative IPC: start in `ModernOverlay.Integration`.

Read more: [public API and package review](../docs/public-api-package-review.md), [integration boundary](../docs/integration-boundary.md), [device recreation](../docs/device-recreation.md).
