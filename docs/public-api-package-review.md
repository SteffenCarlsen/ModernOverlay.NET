# Public API and Package Review

Date: 2026-05-22

This review records the current alpha package/API decision against the modernization spec.

## Verdict

The current public API is acceptable for a preview implementation, but it is not yet a stable 1.0 surface. The package split is intentionally explicit for alpha, while the main package now bundles the Direct2D backend for the common path:

- `ModernOverlay` is the facade and primary user API.
- `ModernOverlay` also carries the Direct2D backend assembly and Vortice package dependencies so simple package consumers can reference one package.
- `ModernOverlay.Direct2D` remains available as the explicit Direct2D HWND backend package for hosts that want a separate backend dependency boundary.
- `ModernOverlay.Win32` exposes low-level window helpers needed by tests, samples, and advanced users.
- `ModernOverlay.Diagnostics` exposes EventSource and logging integration. Core EventSource emission remains built in; the optional part is the logging adapter and developer-facing diagnostics helpers.
- `ModernOverlay.Integration` exposes cooperative IPC command transport for owned applications.
- `ModernOverlay.Integration.Experimental` stays in source for opt-in experimental provider contracts and failure isolation for authorized sample-host research, but it is not packable for the alpha publication.

## Common Path

The common app path no longer requires an explicit `Direct2DOverlayBackend.Register()` call. The `ModernOverlay` package includes the Direct2D backend assembly, and the facade auto-discovers it before the first overlay is created.

For the common package path:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.0.0" />
```

Advanced hosts may still reference the backend package explicitly:

```xml
<PackageReference Include="ModernOverlay.NET" Version="1.0.0" />
<PackageReference Include="ModernOverlay.NET.Direct2D" Version="1.0.0" />
```

This keeps the one-package common scenario working while preserving the backend package as a separately testable artifact.

## Why The Backend Package Also Remains Separate

`ModernOverlay.Direct2D` depends on `ModernOverlay` for the public resource and rendering contracts. Making `ModernOverlay` directly reference `ModernOverlay.Direct2D` would create a project cycle and collapse the backend boundary the spec asks us to keep for future DirectComposition work.

The preview package therefore bundles the already-built `ModernOverlay.Direct2D` assembly into the `ModernOverlay` NuGet package instead of adding a project reference. The clean 1.0 options are:

- Move backend contracts into a lower-level abstractions assembly, then let an all-in-one `ModernOverlay` package depend on the Direct2D backend without a cycle.
- Keep the current bundle-and-separate-package split if users prefer one-package install over a stricter backend boundary.
- Introduce a separate metapackage that depends on `ModernOverlay` and `ModernOverlay.Direct2D`.
- Return to an explicit backend package only if preserving backend ownership proves more important than the one-package common path.

The current preview chooses the bundle-and-separate-package option because it best matches the spec's common-scenario package requirement without refactoring the backend boundary.

The release command gate now enforces this alpha package boundary by cleaning stale package artifacts before pack, requiring exactly the five intended alpha packages, checking that `ModernOverlay` contains the bundled Direct2D backend DLL/XML, and failing if `ModernOverlay.Integration.Experimental` is packed.

## Surface Review

The facade API covers the required spec areas:

- overlay lifecycle: `OverlayWindow.CreateAsync`, `RunAsync`, `StopAsync`, show/hide, pause/resume, recreate, dispose;
- window configuration: input mode, z-order, frame limit, present mode, DPI mode, transparency mode, class options, target tracking, hidden/minimized/render exception policies;
- drawing: immediate-mode draw/fill/measure operations, clips, transforms, helpers, text, images, geometry, strokes, gradients;
- resources: descriptor-backed handles with deterministic disposal, leak snapshots, native realization snapshots;
- target tracking: HWND, process ID/name, title/class, foreground, custom provider, bounds modes, reacquire, per-target tracking cadence;
- diagnostics: frame stats, EventSource, logging adapter, native failure diagnostics.
- cooperative integration: expanded shape/geometry/image/text command protocol with solid and linear-gradient brushes, named-pipe client/server, owned-host command host, and sample host;
- experimental integration: provider/bridge/transport contracts, named-pipe transport adapter, provider failure isolation, and no internal native-resource exposure.

Known preview decisions that should be revisited before 1.0:

- The `ModernOverlay` package bundles `ModernOverlay.Direct2D` by file item rather than project reference to avoid a project cycle. That should be revisited before 1.0.
- `ModernOverlay.Win32` is public. That is useful for advanced users and samples, but it expands the support surface.
1. `ModernOverlay.Diagnostics` is referenced by the core package so EventSource diagnostics are available by default. Keep that for NuGet users; treat the logging adapter and developer helpers as optional.
2. `ModernOverlay.Integration` is preview and opt-in. `ModernOverlay.Integration.Experimental` is source-only for alpha until there is a real authorized experimental provider worth publishing.
3. `Draw.Box` remains a full rectangle helper, and `Draw.CornerBox` covers corner-only box markers without changing existing `Box` behavior.
4. `WindowBounds` is a physical-pixel type while drawing coordinates are DIPs. The public overlay API now uses explicit `SetBoundsPixels` and `SetBoundsDips`; do not carry an obsolete ambiguous `SetBounds(WindowBounds)` alias into v1.
5. `OverlayWindow.Options.Bounds` preserves the caller's requested fallback bounds. Effective target-resolved bounds belong in runtime diagnostics such as `FrameStats.WindowBounds`, `FrameStats.TargetBounds`, and target events.
6. `ModernOverlay.Drawing` is now the home for drawing primitives, draw context operations, resource handles, and resource manager types. `ModernOverlay.Windows` is now the home for window handles, bounds, DPI scale, target descriptors, and overlay window options. `OverlayWindow` remains in the root `ModernOverlay` facade namespace.
7. `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` are fallback request modes today, not exact backend implementations. The fallback is acceptable for MVP/alpha, but true CPU-copy layered alpha or DirectComposition/DXGI per-pixel alpha remains documented backend work.

## Release Stance

This API is ready for MVP/alpha validation and manual release testing. The release stance is intentionally practical: ship a preview package that is useful to experiment with, keep caveats visible, and harden from real usage rather than waiting for production-style completeness. It should keep preview package metadata until:

1. More transparency/DPI/fullscreen validation has been collected across Windows 10 and Windows 11.
2. Package ownership is confirmed.
3. The bundled-backend versus abstractions/metapackage decision is final.
4. The remaining backend transparency names are resolved as exact implementations or renamed before stable release.
