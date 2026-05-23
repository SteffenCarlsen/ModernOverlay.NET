# Implementation History

This document summarizes the important implementation decisions made while bringing the modernization spec to the current MVP/alpha shape. It is a public orientation aid, not a changelog and not the active backlog.

Use these documents for live status instead:

- [Feature completeness](feature-completeness.md)
- [Definition of done audit](definition-of-done-audit.md)
- [Next action points](next-action-points.md)
- [Root task list](../Tasks.md)

## Major Decisions

- The repository targets `net11.0-windows` on `main`, with the .NET 10 fallback policy captured in [ADR 0001](adr/0001-dotnet-11-preview-and-net10-contingency.md).
- The public facade is `ModernOverlay`; drawing/resource types live under `ModernOverlay.Drawing`, and window/options/target/helper types live under `ModernOverlay.Windows`.
- `ModernOverlay` currently bundles the already-built `ModernOverlay.Direct2D` assembly into the preview NuGet package to provide a one-package common path without creating a project-reference cycle.
- `ModernOverlay.Direct2D` remains available as an explicit backend package while the 1.0 package boundary is still under review.
- `ModernOverlay.Integration` is an opt-in cooperative IPC package for owned applications. `ModernOverlay.Integration.Experimental` remains source-only until a real authorized experimental provider exists.
- Direct2D HWND rendering is the current parity backend. DirectComposition/DXGI and exact `UpdateLayeredWindow` CPU-copy alpha are tracked as later backend milestones.
- The transparency story is intentionally caveated for alpha: DWM/color-key behavior is usable locally, while exact per-pixel alpha and DirectComposition remain future work.
- EventSource diagnostics are wired through the core overlay path, and `ModernOverlay.Diagnostics` provides a logging adapter.

## Implemented Alpha Surface

- Win32 owner-thread lifecycle, message pump, waitable frame scheduling, show/hide/move/resize/dispose, no-activate/click-through/interactive styles, topmost and follow-target z-order.
- Direct2D/DirectWrite/WIC backed rendering for clear, shapes, text, images, brushes, stroke styles, geometry, clips, transforms, and helper markers.
- Resource handles with descriptor-backed recreation, generation tracking, deterministic disposal, and leak-report snapshots.
- Frame diagnostics including timing, FPS, command counts, native resource counts, target information, DPI scale, backend generation, skipped/dropped counts, and native failure data.
- Target tracking for HWND, process id/name, title, class name, foreground window, and custom providers, with loss/reacquire events and configurable polling.
- Samples for the spec-named capabilities plus practical aliases under `samples/`.
- BenchmarkDotNet harnesses for draw dispatch, Direct2D render paths, overlay lifecycle, and target tracking.
- Release validation tooling for build, tests, package boundaries, package-consumer smoke tests, transparency sample execution, and benchmark dry-run validation.

## Known Preview Tradeoffs

- The package split and backend bundling are preview decisions and should be revisited before 1.0.
- `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` currently emit fallback diagnostics instead of selecting exact backend implementations.
- Transparent black color-keying makes the Direct2D HWND backend usable for clear-to-transparent overlays, but it is not a final per-pixel alpha story.
- BenchmarkDotNet currently uses the in-process emit toolchain because .NET 11 preview support is still settling.
- Some validation remains local-machine evidence. Windows 11, mixed-DPI, fullscreen/borderless, and additional GPU/driver runs are still follow-up hardening work.

