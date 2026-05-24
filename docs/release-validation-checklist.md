# Release Validation Checklist

This checklist captures practical evidence for a single-developer MVP/alpha release. The goal is to prove the package builds, runs, and is useful enough to play with, not to certify production readiness or exhaustive platform coverage.

The command gate is the recommended minimum before tagging an alpha. The manual sections are confidence-building checks that can be recorded as they are tried on more machines, Windows versions, DPI layouts, fullscreen modes, and GPU/driver combinations.

## Command Gate

Run the repeatable command gate first:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1
```

The gate verifies:

- `ModernOverlay.sln` does not contain virtual solution folders or nested-project entries.
- Release build passes with a binlog.
- Full test suite passes with TRX output and a binlog.
- Non-integration test suite passes with TRX output and a binlog.
- Pack succeeds with a binlog.
- Pack output contains exactly the intended alpha packages: `ModernOverlay.NET`, `ModernOverlay.NET.Direct2D`, `ModernOverlay.NET.Win32`, `ModernOverlay.NET.Diagnostics`, `ModernOverlay.NET.Integration`, and `ModernOverlay.UI`.
- The `ModernOverlay` package contains the bundled Direct2D backend DLL/XML for the common path.
- Every emitted alpha package contains `README.md` and release notes with the required `net11.0-windows` and DWM/color-key fallback caveats.
- `ModernOverlay.Integration.Experimental` remains source-only and is not packed.
- `TransparencyValidationOverlay` runs unless skipped.
- Root binlogs are pruned back to the configured retention count.

For benchmark harness validation, run:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry
```

The dry benchmark run validates compilation/execution and checks the BenchmarkDotNet log for issue markers. Dry results are not performance evidence.

Use `docs/release-validation-results-template.md` to record the machine-specific manual checks below.

## Windowing

- Transparent overlay appears.
- Clear-to-transparent works.
- Click-through passes input to the target below.
- Interactive mode receives pointer input.
- Showing the overlay does not steal focus.
- Topmost mode behaves as expected.
- Follow-target z-order works best-effort and limitations are documented.

## Rendering

- Text renders crisply.
- Images render.
- Dashed shapes render.
- Geometry paths render.
- Clip and transform scopes behave correctly.
- Resize does not crash or leak native resources.
- Manual recreation does not crash and resources draw again afterward.

## Targeting

- Whole-window target tracking works.
- Client-area target tracking works.
- Target minimize/restore behavior works.
- Target lost/reacquired events fire.
- Restricted, elevated, protected, or fullscreen targets fail with documented limitations rather than bypass behavior.

## DPI and Monitors

- Per-monitor DPI changes resize the backend correctly.
- Mixed-DPI monitors behave acceptably.
- Negative-coordinate monitor layouts behave acceptably.

## Diagnostics

- Frame timing updates.
- Resource counts update.
- Target HWND/bounds diagnostics update.
- Native failure diagnostics show useful context.
- EventSource and logging adapter emit expected events.
