# Release Validation Results

Date: 2026-05-23
Validator: Codex local command gate
Machine: `DESKTOP-PIO5DGP`
OS: Microsoft Windows 10 IoT Enterprise LTSC `10.0.19044`, 64-bit
GPU/driver: NVIDIA GeForce RTX 3080 `32.0.15.9174`; AMD Radeon(TM) Graphics `32.0.11024.2`
SDK: `11.0.100-preview.4.26230.115`
PowerShell: `7.6.0`

## Command Gate

- Command: `$env:MSBuildEnableWorkloadResolver='false'; tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry`
- Result: Passed
- Root binlogs retained: 5
- Full test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-23_01_54_55_net11.0.trx`, 109/109 passed
- Non-integration test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-23_01_55_09_net11.0.trx`, 63/63 passed
- Pack result: passed and emitted exactly the five alpha packages: `ModernOverlay`, `ModernOverlay.Direct2D`, `ModernOverlay.Win32`, `ModernOverlay.Diagnostics`, and `ModernOverlay.Integration`
- Package metadata inspection: the release gate asserted all five alpha packages include `README.md` and release notes with the required `net11.0-windows` and DWM/color-key fallback caveats
- Package consumer smoke: restored `ModernOverlay` from the local release packages into a scratch `net11.0-windows` console app, compiled the intended `ModernOverlay`, `ModernOverlay.Drawing`, and `ModernOverlay.Windows` imports, verified removed v1 aliases stay absent, ran the app, and confirmed `ModernOverlay.Direct2D.dll` reaches the consumer output
- Experimental package boundary: `ModernOverlay.Integration.Experimental` was not packed
- Main package boundary: `ModernOverlay` includes the bundled Direct2D backend DLL/XML
- Transparency sample: passed as part of the command gate
- Benchmark dry run: passed; the log contains no BenchmarkDotNet issue markers
- Benchmark dry-run log: `benchmarks\ModernOverlay.Benchmarks\benchmark-dryrun-latest.log`
- Benchmark dry-run reports: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-015548`
- Artifact manifest: `docs/release-artifacts-20260523-alpha.md`

## Package Set

The package set produced by this gate is recorded in `docs/release-artifacts-20260523-alpha.md` with package sizes and SHA-256 hashes.

## Changes Covered Since Previous Alpha Manifest

1. Capture exclusion support through `OverlayWindowOptions.ExcludeFromCapture`.
2. Display-affinity helpers in the Win32 layer and `ModernOverlay.Windows.WindowEffects`.
3. Capture-backed overlay documentation updates that treat capture exclusion as a prerequisite, not a finished capture backend.
4. Current-user and custom Windows pipe ACL support for cooperative named-pipe IPC.
5. Window option and target descriptor API cleanup: `OverlayWindowOptions`, target descriptors, window policies, DPI/present/transparency options, and `FrameRateLimit` now live in `ModernOverlay.Windows`; `WindowTarget.ByWindowTitle`, `WindowTarget.Foreground`, and `RenderExceptionPolicy.IgnoreAndContinue` were removed before v1.
6. Package-consumer smoke coverage for the intended package install/import shape and bundled Direct2D backend output.

## Manual Visual Checks

Manual visual release checks are not fully completed by this command-gate result. The transparency sample executed successfully, but this result does not by itself prove user-observed visual correctness, mixed-DPI movement, fullscreen behavior, Windows 11 behavior, or additional GPU/driver coverage.

Use `docs/release-validation-results-template.md` for target-machine validation of windowing, rendering, target tracking, DPI/monitor movement, fullscreen behavior, and diagnostics.

## Decision

1. Approved for local MVP/alpha validation: yes.
2. Approved as production-ready: not applicable; this project is not using a production-readiness release bar.
3. Remaining confidence gaps: manual visual validation on more target environments; exact `UpdateLayeredWindow`/DirectComposition backend decisions remain documented fallback/future-backend work; capture-backed output-duplication rendering remains experimental research.
4. Follow-up issues: create target-environment validation entries for Windows 11, mixed-DPI monitors, fullscreen/borderless targets, and additional GPU/driver combinations.
