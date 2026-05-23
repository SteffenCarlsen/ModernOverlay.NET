# Next Action Points

Date: 2026-05-22

This roadmap turns the remaining implementation notes, release checks, and follow-up risks into milestone-sized work. The release bar is the current hobbyist MVP/alpha bar: a coherent package that builds, runs, demonstrates the intended API, and is useful to experiment with. Production-style completeness is not the first-release gate.

## 1. MVP/Alpha Release Candidate

Goal: close the preview package into something taggable without expanding the feature scope.

1. Reconcile stale implementation notes.
   - Milestone: Alpha release candidate.
   - Action: replace the old open-question list with the decisions already made: .NET 11 only, current package split, built-in EventSource diagnostics, explicit bounds APIs, current title matching, current sample set, documented transparency fallback, real drawing/windows namespaces, source-only experimental package, and hobbyist MVP release bar.
   - Done when: the notes no longer ask questions that have already been answered.

2. Finish package README and NuGet-facing caveats.
   - Milestone: Alpha release candidate.
   - Status: Implemented and pack-validated.
   - Action: keep the docs lightweight, but make the package page clear about `net11.0-windows`, preview API status, Direct2D bundled common path, source-only experimental integration, and the DWM/color-key transparency fallback.
   - Evidence: the root package README and shared package release notes now call out `net11.0-windows`, preview API/packaging status, the bundled Direct2D common path, source-only experimental integration, DWM/color-key transparency fallbacks, and the hobbyist MVP/alpha release bar.
   - Done when: package validation confirms the README and release notes are included in the emitted alpha packages. Current evidence: Release pack output includes README metadata and updated release notes in all five alpha packages.

3. Run the release command gate after final doc/API edits.
   - Milestone: Alpha release candidate.
   - Status: Passed locally after window namespace/API cleanup.
   - Action: run `tools\Invoke-ModernOverlayReleaseValidation.ps1`, then run `tools\Invoke-ModernOverlayReleaseValidation.ps1 -SkipTransparencySample -RunBenchmarkDry` if benchmark harness evidence changed.
   - Evidence: `$env:MSBuildEnableWorkloadResolver='false'; tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry` passed on 2026-05-23 with 109/109 full tests, 63/63 non-integration tests, pack/package-boundary validation, package README/release-note caveat validation, package-consumer smoke restore/build/run, transparency sample execution, and BenchmarkDotNet dry-run reports under `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-015548`.
   - Done when: the latest build, full test, non-integration test, pack, package-boundary, transparency-sample, and benchmark-dry evidence is recorded.

4. Record one final local release result.
   - Milestone: Alpha release candidate.
   - Status: Updated with latest local command-gate evidence.
   - Action: update or add a dated result file from `docs/release-validation-results-template.md`.
   - Evidence: `docs/release-validation-results-20260523-local.md` records the latest `DESKTOP-PIO5DGP` command gate after the window namespace/API cleanup, machine details, package metadata assertions, known caveats, and manual-check status.
   - Done when: command evidence, machine details, known caveats, and manual-check status are captured in one place.

## 2. First Alpha Release

Goal: publish or tag the first playable preview without pretending the project is production-ready.

1. Tag/package the preview as an MVP alpha.
   - Milestone: First alpha.
   - Status: Package artifact manifest refreshed after window namespace/API cleanup; tag/publish not performed.
   - Action: use the five intended alpha packages: `ModernOverlay`, `ModernOverlay.Direct2D`, `ModernOverlay.Win32`, `ModernOverlay.Diagnostics`, and `ModernOverlay.Integration`.
   - Evidence: `docs/release-artifacts-20260523-alpha.md` records the five local alpha package artifacts, SHA-256 hashes, package-boundary caveats, absent experimental package, and bundled Direct2D backend entries after the window namespace/API cleanup.
   - Done when: `ModernOverlay.Integration.Experimental` is not packed, `ModernOverlay` contains the bundled Direct2D backend DLL/XML, and the package set has a recorded artifact manifest before any tag or publish action.

2. Keep `ModernOverlay.Integration.Experimental` source-only.
   - Milestone: First alpha.
   - Status: Enforced by release gate.
   - Action: leave it out of package publication until a real authorized provider exists.
   - Evidence: `tools\Invoke-ModernOverlayReleaseValidation.ps1` fails if an experimental package is emitted; the latest local command gate passed with exactly the five intended alpha packages.
   - Done when: the release gate continues to fail if an experimental package is emitted.

3. Keep Windows 11, mixed-DPI, fullscreen, and extra GPU validation as follow-up work.
   - Milestone: First alpha.
   - Status: Documented in release notes and artifact manifest.
   - Action: document these as confidence-building checks, not alpha blockers.
   - Evidence: package README/release notes, `docs/release-validation-results-20260523-local.md`, and `docs/release-artifacts-20260523-alpha.md` state that broader Windows 11, mixed-DPI, fullscreen/borderless, and GPU/driver validation remain follow-up hardening work.
   - Done when: release notes clearly say what has been locally validated and what remains environment-specific.

## 3. Post-Alpha Validation

Goal: expand confidence from one local Windows 10 validation path into more real environments.

1. Validate on Windows 11.
   - Milestone: Post-alpha hardening.
   - Action: run the command gate and transparency sample on a Windows 11 machine.
   - Done when: a Windows 11 result file records machine details, pass/fail notes, and any compositor differences.

2. Validate mixed-DPI and negative-coordinate monitor layouts.
   - Milestone: Post-alpha hardening.
   - Action: move overlays and target windows across monitors with different scale factors and coordinates.
   - Done when: DPI changes, target tracking, resizing, and rendering behavior are recorded.

3. Validate fullscreen and borderless game scenarios.
   - Milestone: Post-alpha hardening.
   - Action: test transparent/click-through overlays against representative fullscreen and borderless targets without attempting bypass behavior.
   - Done when: limitations are documented as Windows/compositor behavior rather than hidden library promises.

4. Validate additional GPU/driver combinations.
   - Milestone: Post-alpha hardening.
   - Action: repeat transparency and resize/recreate checks on at least one additional GPU/driver setup.
   - Done when: results identify whether the current DWM/color-key path is broadly usable or machine-sensitive.

5. Observe remote CI.
   - Milestone: Post-alpha hardening.
   - Action: confirm the configured workflow restores, builds, tests, packs, runs benchmark dry validation, and uploads diagnostics on `windows-latest`.
   - Done when: remote CI evidence is recorded, not only local command output.

## 4. Integration Package Expansion

Goal: improve cooperative IPC for owned applications while keeping the no-injection boundary clean.

1. Add reusable remote resource handles.
   - Milestone: Integration alpha plus.
   - Status: Implemented first pass.
   - Evidence: `OverlayResourceDefinition`, command resource-reference ids, `ReleaseResourceIds`, and `CooperativeOverlayCommandHost` caching now define, reference, and release remote brush, font, image, and geometry resources without recreating them every update.
   - Done when: focused integration tests prove cached resources survive command clears, release deterministically, reject missing references, and do not leak after failed updates.

2. Add larger payload strategy.
   - Milestone: Integration alpha plus.
   - Status: Implemented first pass.
   - Evidence: alpha IPC keeps line-delimited JSON, exposes `OverlayCommandLimits`, enforces serialized-message, inline-image, geometry, command-count, resource-definition, resource-release, and resource-id limits, and documents side-channel files/shared memory as future work.
   - Done when: image/geometry payload size limits are documented and tested.

3. Add command diffing.
   - Milestone: Integration beta.
   - Status: Implemented first pass.
   - Evidence: `OverlayCommandPatch` supports append, insert-before, insert-after, replace, remove, and clear operations against explicit command ids. `CooperativeOverlayCommandHost` applies patches transactionally and rejects invalid patch batches without mutating the previous command/resource state.
   - Done when: ordering, deletion, replacement, and failure rollback semantics are explicit and tested.

4. Add authentication or local trust controls for IPC.
   - Milestone: Integration beta.
   - Status: Implemented first pass with token and Windows pipe ACL options.
   - Evidence: `NamedPipeOverlayCommandSecurity.RequireCommandToken(...)` lets a server require a shared local command token; `NamedPipeOverlayCommandClient` stamps the token; the server rejects missing/invalid tokens before invoking the handler; `NamedPipeOverlayCommandSecurity.CurrentUserOnly(...)` creates server pipes with a current-user Windows ACL; `NamedPipeOverlayCommandSecurity.WithPipeSecurity(...)` accepts custom `PipeSecurity`; `IpcOverlayDemo` and `SampleOwnedHost` use the token path.
   - Done when: the IPC sample uses the selected trust model and docs explain the boundary.

5. Consider multi-client fan-out.
   - Milestone: Integration beta or later.
   - Status: Implemented first pass.
   - Evidence: `NamedPipeOverlayCommandServer` now accepts multiple owned senders concurrently with a bounded `maxConcurrentConnections` setting, and tests prove two clients can be handled at the same time.
   - Done when: richer conflict resolution and command ownership rules are defined if real multi-sender scenarios need more than bounded concurrent command ingestion.

## 5. Transparency Backend Work

Goal: move from acceptable MVP fallback transparency to exact backend implementations.

1. Prototype exact `UpdateLayeredWindow` CPU-copy alpha.
   - Milestone: Transparency backend milestone.
   - Action: compare CPU-copy per-pixel alpha against the current Direct2D HWND plus DWM/color-key fallback.
   - Done when: performance, visual correctness, resize behavior, and resource lifetime tradeoffs are measured.

2. Prototype DirectComposition/DXGI.
   - Milestone: DirectComposition backend milestone.
   - Action: build a backend only after the Direct2D HWND path remains stable enough to compare against.
   - Done when: transparent composition, click-through/no-activate, resize, device loss, draw parity, benchmark comparison, and complexity are proven.

3. Prototype capture-backed overlay reconstruction.
   - Milestone: Experimental backend research.
   - Action: explore an opt-in backend/sample that uses `IDXGIOutputDuplication` to copy the desktop/output frame behind the overlay, draws that copy as the first layer, and draws normal overlay commands on top. Treat this as capture-backed reconstruction, not true transparency.
   - Backlog features: capture-backed background source, D3D11/DXGI texture render path, output/target mapping, duplication lifecycle recovery, immediate-present research mode, passive overlay plus interactive control window sample, optional ImGui-hosting sample, primitive batching benchmarks, and fullscreen placement research.
   - Implemented prerequisite: explicit capture exclusion has a first pass through `OverlayWindowOptions.ExcludeFromCapture`, `WindowEffects.TryExcludeFromCapture(...)`, and Win32 display-affinity helpers.
   - Design notes: keep it out of `TransparencyMode.Auto`; prefer an experimental package such as `ModernOverlay.Capture` or `ModernOverlay.Experimental.Capture`; do not depend on undocumented window-band behavior for the supported NuGet path; prove overlay self-capture avoidance with `SetWindowDisplayAffinity(..., WDA_EXCLUDEFROMCAPTURE)` or another documented mechanism; keep any implementation clean-room and exclude game-specific logic, memory reading, process injection, anti-cheat bypass, or stream-proofing claims.
   - Evidence to collect: recursive-capture artifact checks, single-monitor crop/mapping correctness, target move/resize behavior, click-through/no-activate behavior, duplication recreation after `DXGI_ERROR_ACCESS_LOST`, latency/performance comparison with Direct2D HWND and DirectComposition, and Windows 10/11 manual visual artifacts.
   - Reference: `docs/capture-backed-overlay-spike.md`.
   - Done when: the prototype shows whether capture-backed rendering is a useful experimental mode and whether unsupported window-band behavior is excluded, isolated, or abandoned.

4. Choose the stable transparency story.
   - Milestone: 1.0 backend decision.
   - Action: decide whether Direct2D HWND fallback remains supported, whether CPU-copy is a fallback, whether DirectComposition becomes default, and whether capture-backed reconstruction remains experimental.
   - Done when: `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` either perform exact implementations or are renamed/re-scoped before stable release, and capture-backed behavior is not confused with compositor transparency.

## 6. 1.0 API And Package Stabilization

Goal: remove preview tradeoffs before calling the API stable.

1. Decide the long-term backend package boundary.
   - Milestone: 1.0 API freeze.
   - Action: choose between the current bundled backend package, a metapackage, or a lower-level abstractions assembly that breaks the project-reference cycle cleanly.
   - Done when: package dependencies no longer rely on preview-only file bundling unless that is intentionally accepted.

2. Review public `ModernOverlay.Win32` ownership.
   - Milestone: 1.0 API freeze.
   - Action: decide whether it stays public for advanced users or becomes lower-level/internal to reduce support surface.
   - Done when: the package split reflects the desired support promise.

3. Finish namespace ownership.
   - Milestone: 1.0 API freeze.
   - Status: Implemented first pass.
   - Action: review remaining root-level overlay configuration types and decide whether any should move into `ModernOverlay.Windows` or stay in the facade namespace.
   - Evidence: drawing primitives/resources live in `ModernOverlay.Drawing`; window handles, bounds, DPI scale, target descriptors, overlay window options, and window helper facades live in `ModernOverlay.Windows`; `OverlayWindow` remains in the root facade namespace; compatibility aliases such as `WindowTarget.ByWindowTitle`, `WindowTarget.Foreground`, and `RenderExceptionPolicy.IgnoreAndContinue` were removed before v1.
   - Done when: stable docs and samples use final namespaces with no obsolete v1 aliases. Current package-consumer evidence: the release gate restores `ModernOverlay` into a scratch consumer app, compiles the intended namespace imports, verifies removed aliases stay absent, and confirms the bundled Direct2D backend reaches the consumer output.

4. Keep explicit bounds APIs only.
   - Milestone: 1.0 API freeze.
   - Action: avoid reintroducing ambiguous `SetBounds(WindowBounds)` or similar aliases.
   - Done when: pixel and DIP boundaries remain explicit in code, docs, and samples.

5. Revisit render-loop monitor migration.
   - Milestone: 1.0 hardening.
   - Status: Implemented first pass.
   - Action: recompute `FrameRateLimit.DisplayDefault` if the overlay moves to a monitor with a different refresh rate.
   - Evidence: `OverlayWindow.RunAsync` now passes a live frame-interval resolver into the Win32 owner-thread frame loop. The owner-thread waitable timer rechecks the interval while running and resets its timer period when `DisplayDefault` resolves a new monitor refresh rate. `Win32OwnerThreadTests.FrameLoopRechecksDynamicIntervalWhileRunning` covers the dynamic resolver path.
   - Done when: frame pacing follows monitor migration or docs explain the fixed-at-run-start behavior.

6. Revisit pointer/input depth.
   - Milestone: 1.0 hardening.
   - Status: Implemented first scoped expansion.
   - Action: decide whether pointer capture, wheel, raw input, gestures, or selective hit testing belong in 1.0.
   - Evidence: `PointerWheel` and `OverlayPointerEventKind.Wheel` add vertical/horizontal wheel delta support to the existing pointer surface. Docs now state the alpha interaction scope: movement, button presses/releases, and wheel deltas are supported; pointer capture, raw input, gestures, and selective per-pixel hit testing remain future design work.
   - Done when: the interaction model is intentionally scoped instead of accidentally minimal.

7. Revisit real device-loss coverage.
   - Milestone: 1.0 hardening.
   - Status: Deterministic recreate-target recovery implemented; real driver-reset validation remains manual.
   - Action: supplement manual recreate/resize coverage with real device-loss or driver-reset evidence where practical.
   - Evidence: `EndFrameResult` can now report a backend recreate request. The Direct2D HWND backend translates `D2DERR_RECREATE_TARGET` into that result, and `OverlayWindow` raises `DeviceLost`, advances resource generation, recreates the backend, raises `DeviceRestored`, and continues rendering. Focused tests cover Direct2D recreate-target reporting and overlay recovery after a backend recreate request.
   - Done when: backend recreation claims match observed hardware behavior; current automated evidence covers deterministic recreate-target recovery, while real driver-reset behavior still requires representative hardware validation.

## 7. Performance Baseline

Goal: turn benchmark harness existence into useful performance evidence.

1. Capture baseline benchmark results.
   - Milestone: Post-alpha performance baseline.
   - Status: Baseline recorded for every current benchmark class.
   - Action: add new non-dry baselines when new benchmark classes are introduced for target tracking, DirectComposition, `UpdateLayeredWindow`, or capture-backed overlays.
   - Evidence: `docs/performance-baseline-20260522-local.md` records non-dry `DrawContextBenchmarks`, `Direct2DRenderBenchmarks`, `OverlayLifecycleBenchmarks`, and `TargetTrackingBenchmarks` runs on `DESKTOP-PIO5DGP`, including machine details, commands, artifact paths, and summary results.
   - Done when: artifact paths, environment details, and relevant benchmark summaries are recorded for every current benchmark class. Current evidence gap: none for existing benchmark classes; add new baselines when new backend benchmark classes are introduced.

2. Use the performance regression issue template.
   - Milestone: Post-alpha performance baseline.
   - Action: file regressions when benchmark changes are meaningful enough to track.
   - Done when: performance issues contain baseline/current artifacts and impact notes.

3. Revisit BenchmarkDotNet toolchain.
   - Milestone: 1.0 hardening.
   - Action: move away from in-process emit once .NET 11 and BenchmarkDotNet support make out-of-process runs reliable.
   - Done when: benchmark results are less affected by harness/runtime limitations.
