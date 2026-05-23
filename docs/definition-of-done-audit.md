# Definition of Done Audit

Date: 2026-05-22

This audit maps the modernization spec's definition of done to current repository evidence. The release bar for this project is an enjoyable single-developer MVP/alpha: something that builds, packages, demonstrates the intended API, and works well enough to play with. It is not a production-readiness certification, and it does not require exhaustive test coverage or proving every Windows/GPU/fullscreen edge case before the first release.

| Requirement | Status | Evidence |
|---|---|---|
| Project targets `net11.0-windows`, or documented `net10.0-windows` fallback branch exists | Achieved | `global.json`, project files, and `docs/adr/0001-dotnet-11-preview-and-net10-contingency.md` |
| Project builds and tests on Windows | Achieved locally; CI configured | `.github/workflows/ci.yml`; `docs/release-validation-results-20260523-local.md`; latest full tests 109/109 and non-integration tests 63/63 passed locally |
| SharpDX is entirely absent | Achieved with guard | `Directory.Build.targets` rejects `SharpDX*` package references; `tests/ModernOverlay.Tests/SharpDxGuardTests.cs` scans package/source references |
| Vortice is the DirectX/Win32 binding layer | Achieved | `Directory.Packages.props`; `src/ModernOverlay.Direct2D`; `tests/ModernOverlay.Tests/VorticeDependencyProbeTests.cs` |
| Every GameOverlay.NET capability in the parity matrix has a modern equivalent | Achieved with documented preview caveats | `docs/gameoverlay-migration.md`; `docs/feature-completeness.md`; coverage for transparency, click-through, sticky targets, FPS limiting, drawing, text, images, geometry, helper APIs, diagnostics |
| Public API is documented as intentionally breaking and not drop-in compatible | Achieved | `README.md`, `docs/quick-start.md`, `docs/gameoverlay-migration.md`, `docs/public-api-package-review.md` |
| Transparent, click-through, no-activate overlays work | Achieved locally | `tests/ModernOverlay.Tests/TransparencyVisualTests.cs`; `tests/ModernOverlay.Tests/Win32StyleTests.cs`; `docs/release-validation-results-20260523-local.md`; `samples/TransparencyValidationOverlay` |
| Sticky window behavior works | Achieved locally | `tests/ModernOverlay.Tests/TargetTrackingTests.cs`; `samples/StickyTargetOverlay`; `samples/StickyWindowOverlay`; `docs/target-tracking.md` |
| Resource/device recreation works | Achieved locally | `tests/ModernOverlay.Tests/Direct2DRenderBackendTests.cs`; `tests/ModernOverlay.Tests/OverlayResourceManagerTests.cs`; `docs/device-recreation.md` |
| At least six samples demonstrate core usage | Achieved | 15 sample projects under `samples/`, including spec-named aliases |
| Benchmarks exist and can be run | Achieved | `benchmarks/ModernOverlay.Benchmarks`; `.github/workflows/ci.yml`; `tools/Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry`; BDN dry reports under `benchmarks/ModernOverlay.Benchmarks/BenchmarkDotNet.Artifacts/20260523-015548`; non-dry baseline in `docs/performance-baseline-20260522-local.md` |
| Diagnostics exist for frame timing, resources, target tracking, and native failures | Achieved | `src/ModernOverlay.Diagnostics`; `FrameStats`; `Win32NativeDiagnostics`; `samples/DiagnosticsOverlay`; `tests/ModernOverlay.Tests/OverlayEventSourceTests.cs` |
| Optional integration features are isolated, opt-in, and documented for owned/authorized applications only | Achieved | `src/ModernOverlay.Integration`; source-only `src/ModernOverlay.Integration.Experimental`; `docs/integration-boundary.md`; `tests/ModernOverlay.Tests/IntegrationCommandTests.cs`; `tests/ModernOverlay.Tests/ExperimentalIntegrationTests.cs` |
| Stealth, anti-cheat bypass, protected-process bypass, and kernel-level integration are not implemented | Achieved by absence and docs | `docs/integration-boundary.md`; no injection/hook/kernel projects or samples; experimental package exposes contracts only |

## Confidence Gaps

These items are useful next validation targets, not blockers for the hobbyist alpha/MVP release bar:

- Remote CI has not been observed in this local session. The workflow is configured for restore, solution-shape validation, build, non-integration tests, optional Windows integration tests, pack, benchmark dry run, and diagnostics upload.
- Windows 11 transparency validation is still missing. The project should support Windows 10 and Windows 11, but Windows 11 validation can happen after the first release.
- Mixed-DPI, fullscreen/borderless-game, and additional GPU/driver manual validation are not complete.
- `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` are documented fallback request modes. This matches the spec's current Direct2D HWND parity-backend decision, but it is not an exact CPU-copy or DirectComposition backend implementation.
- `ModernOverlay.Drawing` now owns drawing/resource primitives, and `ModernOverlay.Windows` owns window handle/bounds/DPI primitives plus helper facades. Root-level overlay configuration types remain in the facade namespace for alpha and should be reviewed before 1.0.

## Current Verdict

The implementation satisfies the MVP/alpha release bar: the library has a coherent package shape, repeatable command validation, local Windows evidence, samples, diagnostics, and documented caveats. Broader Windows 11, mixed-DPI, fullscreen, driver, and long-run validation should improve confidence over time, but those checks are follow-up hardening work rather than first-release blockers.
