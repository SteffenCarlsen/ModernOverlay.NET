# Tasks

Status date: 2026-05-23

This task list maps the current repository state against the [modernization spec](docs/modernization-spec.md). The implementation has moved past the original Phase 0-9 parity backlog for an MVP/alpha: the solution, Vortice/Direct2D stack, windowing, drawing/resource APIs, samples, docs, diagnostics, package validation scripts, integration package, and benchmark harness are all present.

The remaining work is no longer "build the first overlay library" work. It is mostly alpha release hygiene, current validation repair, broader environment evidence, exact backend prototypes, and 1.0 API/package stabilization.

The version 1.1 interactive UI feature track is intentionally tracked separately in `docs/modernoverlay-1.1-interactive-ui-analysis.md` and `docs/modernoverlay-1.1-interactive-ui-tasks.md` so the alpha/1.0 stabilization list stays focused.

## Current Validation Snapshot

- `dotnet test ModernOverlay.sln --configuration Release --verbosity minimal` currently builds all projects and passes 109/109 tests locally.
- Existing docs record an earlier passing release gate in `docs/release-validation-results-20260523-local.md`. Re-run the full release gate after documentation cleanup before tagging or publishing the alpha.

## Alpha Release Candidate

- [ ] Re-run the release validation gate after documentation cleanup.
  - Preferred command: `$env:MSBuildEnableWorkloadResolver='false'; tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry`.
  - Confirm build, full tests, non-integration tests, pack, package-boundary checks, package consumer smoke, transparency sample, and benchmark dry-run all pass.
  - Record new evidence in a fresh dated release validation result or update the existing 2026-05-23 result if that is the chosen convention.

## First Alpha Release

- [ ] Configure NuGet.org trusted publishing and tag the first alpha once the release gate is green.
  - Add a trusted publishing policy for owner `TaFFe`, repository `SteffenCarlsen/ModernOverlay.NET`, workflow file `release.yml`, and environment `release`.
  - Add the GitHub Actions repository secret `NUGET_USER` with the NuGet profile name.
  - Push a unique SemVer tag such as `v1.0.0`; NuGet package versions are immutable.
  - Confirm the release workflow creates a GitHub release and publishes the five intended alpha packages.
  - Intended package set: `ModernOverlay.NET`, `ModernOverlay.NET.Direct2D`, `ModernOverlay.NET.Win32`, `ModernOverlay.NET.Diagnostics`, and `ModernOverlay.NET.Integration`.
  - Keep `ModernOverlay.Integration.Experimental` source-only for alpha.
  - Confirm `ModernOverlay` still bundles `ModernOverlay.Direct2D.dll` and XML for the common package path.

- [ ] Observe remote CI on `windows-latest`.
  - Confirm restore, solution shape validation, build, non-integration tests, optional integration tests, pack, benchmark dry-run, and artifact upload.
  - Record the remote CI run URL/result in release notes or validation docs.

## Post-Alpha Environment Validation

- [ ] Validate the command gate and transparency sample on Windows 11.
  - Record OS build, GPU, driver, DPI scale, and compositor observations.

- [ ] Validate mixed-DPI and negative-coordinate monitor layouts.
  - Cover overlay movement, target tracking, resize, DPI change handling, and rendering clarity across monitors.

- [ ] Validate fullscreen and borderless target scenarios.
  - Keep the result framed as Windows/compositor behavior, not a bypass guarantee.
  - Document any limitations in troubleshooting and release notes.

- [ ] Validate at least one additional GPU/driver combination.
  - Focus on transparency, resize/recreate behavior, and target tracking.

- [ ] Complete the manual visual release checklist on representative machines.
  - Use `docs/release-validation-checklist.md` and `docs/release-validation-results-template.md`.

## Transparency Backend Work

- [ ] Prototype exact `UpdateLayeredWindow` CPU-copy per-pixel alpha.
  - Compare visual correctness, resize behavior, CPU cost, latency, and resource lifetime against the current Direct2D HWND plus DWM/color-key fallback.

- [ ] Prototype a DirectComposition/DXGI backend.
  - Prove transparent composition, click-through/no-activate behavior, resize, device loss, draw parity, and benchmark comparison before considering it as default.

- [ ] Decide the stable transparency API contract.
  - Before 1.0, `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` should either map to exact implementations or be renamed/rescoped as fallback request modes.

- [ ] Continue capture-backed overlay research as experimental only.
  - Keep it separate from true compositor transparency.
  - Prove no recursive self-capture, correct output mapping, target movement across monitor/DPI boundaries, duplication loss recovery, and latency tradeoffs before exposing any public package.

## 1.0 API And Package Stabilization

- [ ] Decide the long-term backend package boundary.
  - Replace or explicitly accept the current preview file-bundling approach for `ModernOverlay.Direct2D` inside `ModernOverlay`.
  - Consider a metapackage or lower-level abstractions package if it removes the project-reference cycle cleanly.

- [ ] Review public `ModernOverlay.Win32` ownership.
  - Decide whether it remains a supported advanced-user package or becomes a lower-level/internal implementation detail.

- [ ] Finalize namespace ownership before API freeze.
  - Review remaining root-level overlay configuration types.
  - Ensure docs and samples use the final namespaces with no temporary aliases.

- [ ] Preserve explicit pixel/DIP bounds APIs.
  - Avoid reintroducing ambiguous `SetBounds(WindowBounds)`-style APIs unless the semantics are unmistakable.

- [ ] Decide the 1.0 input depth.
  - Current alpha supports pointer movement, button presses/releases, and wheel deltas.
  - Decide whether pointer capture, raw input, gestures, or selective hit testing belong before 1.0.

- [ ] Add stronger real device-loss evidence.
  - Automated tests cover deterministic recreate-target recovery.
  - Representative driver-reset or hardware device-loss validation is still needed before making stronger claims.

## Integration Package Follow-Up

- [ ] Define richer multi-client ownership and conflict rules if real scenarios need more than bounded concurrent ingestion.

- [ ] Design a larger-payload strategy beyond line-delimited JSON.
  - Candidate directions: side-channel files or shared memory for large/frequently reused image and geometry payloads.

- [ ] Keep experimental integration unpublished until a real authorized provider exists.
  - The current package should remain contract-only/source-only for alpha.

## Performance And Benchmarks

- [ ] Add fresh non-dry benchmark baselines when new backend classes or major drawing paths are introduced.
  - Especially for `UpdateLayeredWindow`, DirectComposition, capture-backed rendering, and any large IPC payload changes.

- [ ] Revisit the BenchmarkDotNet toolchain before 1.0.
  - Move away from in-process emit once .NET 11 and BenchmarkDotNet support make out-of-process runs reliable.

- [ ] Use the performance regression issue template when benchmark deltas become meaningful enough to track.
