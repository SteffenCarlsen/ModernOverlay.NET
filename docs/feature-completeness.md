# Feature Completeness

This matrix tracks non-benchmark spec coverage for the MVP/alpha library. The bar is a coherent, buildable, usable package with samples and documented caveats; broader environment coverage is hardening work, not a first-release requirement.

See `docs/next-action-points.md` for the numbered milestone/action roadmap that follows from this matrix.

| Area | Status | Evidence |
|---|---|---|
| Repository bootstrap | Implemented | Solution, central package management, analyzers, Windows CI, `.gitignore`, dependency report |
| SharpDX removal guard | Implemented | MSBuild package guard and source/package tests |
| .NET 11 / .NET 10 fallback decision | Implemented | `docs/adr/0001-dotnet-11-preview-and-net10-contingency.md` |
| HWND lifecycle | Implemented | `Win32OverlayWindow`, owner thread, lifecycle tests |
| Input modes | Implemented | Click-through/interactive styles, pointer move/button/wheel events, input sample |
| Transparency modes | Implemented with documented fallbacks and local validation | DWM glass and layered alpha exist; `UpdateLayeredWindow` and DirectComposition request paths fall back to DWM glass/color-key transparency with diagnostics; automated local transparent-clear pixel test passes |
| DPI handling | Implemented with manual QA remaining | Per Monitor V2, `WM_DPICHANGED`, conversion helpers, DPI docs |
| Direct2D backend | Implemented | Direct2D HWND backend, render target creation, resize/recreate, primitive tests |
| Resource system | Implemented | Descriptor handles, native realization tracking, leak report, recreation behavior |
| Drawing parity | Implemented | Shapes, text, images, geometry, strokes, clips, transforms, `Draw.Box`, `Draw.CornerBox`, `Draw.Crosshair`, and `Draw.Arrow` helpers |
| Render loop/lifecycle | Implemented | Owner-thread run loop, waitable timer, frame stats, pause/resume, exception policy, backend-requested recreation |
| Target tracking | Implemented | HWND/PID/process/title/class/foreground/provider targets, sticky sample, loss/reacquire |
| Window helpers | Implemented | Spec-shaped `ModernOverlay.Windows.WindowQuery`, `WindowZOrder`, and `WindowEffects` facades over the lower-level Win32 helpers, with failure diagnostics preserved |
| Diagnostics | Implemented | EventSource, logging adapter, diagnostics sample, native failure tracking |
| Samples | Implemented by capability with spec-named aliases | Basic, StickyWindow/StickyTarget, Interactive/InputMode, ImageAndText/Image, Geometry/Shapes, diagnostics, text layout, hotkey, transparency, IPC overlay demo, sample owned host |
| Docs | Implemented draft | Quick start, installation, migration, troubleshooting, window modes, target tracking, DPI, drawing, resources, recreation, integration boundary, API/package review, release validation checklist/results template, definition-of-done audit |
| Package common path | Implemented and gate-validated for preview | `ModernOverlay` package bundles the Direct2D backend assembly and Vortice dependencies while keeping `ModernOverlay.Direct2D` available as an explicit backend package; release validation asserts the exact package set and bundled backend entries |
| Namespaces | Implemented for preview | `ModernOverlay.Drawing` contains drawing primitives, draw context operations, resource handles, and resource manager types; `ModernOverlay.Windows` contains window handles, bounds, DPI scale, target descriptors, overlay window options, and helper facades |
| Optional integration package | Implemented for cooperative IPC | `ModernOverlay.Integration` command protocol, expanded shape/geometry/image/text command set with inline and reusable remote resources, named-pipe client/server, command host, loopback tests, `IpcOverlayDemo`, and `SampleOwnedHost` |
| Experimental integration package | Implemented as source-only isolated contracts | `ModernOverlay.Integration.Experimental` exposes provider/bridge/transport contracts, named-pipe transport adapter, provider isolation wrapper, and tests without exposing internal native resources; it is not packable for alpha |

## Remaining Feature Risks

- Local transparency validation passed on one Windows 10 laptop setup. Representative Windows 11, fullscreen, mixed-DPI, and additional GPU/driver validation should be added over time, but those checks are not mandatory for the first MVP/alpha release.
- Exact `UpdateLayeredWindow` CPU-copy and DirectComposition/DXGI backends are not implemented; requested modes currently fall back to DWM glass with diagnostics.
- Capture-backed output-duplication rendering is documented as an experimental research idea in `docs/capture-backed-overlay-spike.md`. It would reconstruct the apparent background by drawing a captured output frame behind overlay commands, so it must not be presented as true compositor transparency or a current alpha feature.
- Capture-backed prerequisite work has started with `OverlayWindowOptions.ExcludeFromCapture` and `WindowEffects.TryExcludeFromCapture(...)`, which apply the supported Win32 display-affinity request needed to avoid recursive self-capture in future output-duplication prototypes.
- The preview package bundles `ModernOverlay.Direct2D` by file item to avoid a project cycle. The long-term package boundary still needs a 1.0 decision.
- Public API and package split are reviewed for alpha in `docs/public-api-package-review.md`; a 1.0 naming/package ownership review is still required before calling the surface stable.
- Device recreation has deterministic manual and backend-requested coverage, including Direct2D recreate-target reporting. Real driver-reset behavior still needs representative hardware validation before making strong device-loss claims.
- `ModernOverlay.Drawing` now owns drawing/resource primitives, and `ModernOverlay.Windows` owns window handle/bounds/DPI primitives, target descriptors, overlay window options, and helper facades. `OverlayWindow` remains in the root facade namespace.
- `ModernOverlay.Integration` covers common shape, helper, inline geometry path, clear, image, text, solid-brush, linear-gradient-brush commands, reusable remote brush/font/image/geometry resources, explicit JSON payload limits, transactional command patches, an opt-in local command token for named-pipe senders, current-user/custom Windows pipe ACLs, and bounded concurrent named-pipe clients. Side-channel file payloads, shared-memory payloads, and richer multi-client ownership/conflict policies remain future integration work.
- `ModernOverlay.Integration.Experimental` is contract-only and source-only for alpha. It validates the spec's opt-in package boundary and failure-isolation shape, but it should not be published until there is a real deeper integration provider.
- `tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry` passed locally and is recorded in `docs/release-validation-results-20260523-local.md`. The gate covered build, full tests, non-integration tests, pack/package-boundary assertions, transparency sample execution, and BenchmarkDotNet dry-run issue-marker inspection. That proves the automated command/benchmark gates, not the full manual visual matrix.
- `docs/definition-of-done-audit.md` maps the spec definition of done to current evidence and treats broader platform/manual validation as confidence-building follow-up work rather than production-release gates.
- `.github/workflows/ci.yml` now validates solution shape, build, non-integration tests, optional Windows integration tests, pack, and BenchmarkDotNet dry-run execution on `windows-latest`, with binlogs/TRX/benchmark artifacts uploaded for diagnosis.
