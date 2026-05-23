# Capture-Backed Overlay Spike

Date: 2026-05-22

## Idea

Model a future experimental overlay mode that avoids depending on compositor transparency by copying the desktop output behind the overlay and drawing that copy as the overlay background.

This is not true transparency. It is an opaque overlay that reconstructs the background every frame:

1. Acquire the current desktop/output frame with `IDXGIOutputDuplication`.
2. Copy the acquired frame into a reusable Direct3D texture.
3. Crop or map the copied output to the overlay window/client bounds.
4. Draw that copied texture as the first layer of the overlay.
5. Draw ModernOverlay primitives, text, images, and integration commands on top.
6. Present the finished overlay frame.

The practical goal is to test whether a capture-backed overlay can provide tighter visual synchronization, fewer DWM alpha artifacts, or better fullscreen/borderless behavior than the current DWM/color-key fallback.

## Placement

Keep this out of the stable transparency story until proven.

Preferred shape:

1. Add an experimental package such as `ModernOverlay.Capture` or `ModernOverlay.Experimental.Capture`.
2. Keep the public API opt-in and clearly labeled as capture-backed, not transparent.
3. Avoid making it the default for `TransparencyMode.Auto`.
4. Avoid promising support for undocumented window-band behavior.

Possible API names:

1. `CaptureBackedOverlayOptions`
2. `CaptureBackedOverlayBackend`
3. `OverlayWindowOptions.BackgroundMode = OverlayBackgroundMode.CaptureBacked`
4. `CaptureSource.OutputDuplication(...)`

Avoid naming it only `TransparencyMode.CaptureBacked` unless the docs make the distinction very clear.

## Overlay Feature Backlog

These are the overlay-specific features worth preserving for future implementation. They are based on the public capture-backed overlay pattern observed in the referenced research repo, but the implementation must be clean-room and limited to legitimate overlay scenarios.

1. Capture-backed background source.
   - Purpose: make the overlay appear transparent without relying on compositor alpha by redrawing the output frame behind it.
   - Implementation idea: introduce an experimental capture source that owns `IDXGIOutputDuplication`, exposes the latest output texture, and can map a target/overlay rectangle to a source rectangle.
   - API sketch: `CaptureSource.OutputDuplication(OutputSelection selection)` and `OverlayBackgroundMode.CaptureBacked`.
   - Done when: a sample can render a captured desktop/output texture as the first overlay layer with normal ModernOverlay drawing commands above it.

2. Explicit capture exclusion.
   - Purpose: prevent the overlay from appearing in its own duplicated frame and creating recursive visual feedback.
   - Status: Implemented first pass for normal overlay windows.
   - Evidence: `OverlayWindowOptions.ExcludeFromCapture` applies `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` when a Win32 overlay is shown; `WindowEffects.TryExcludeFromCapture(...)` and `WindowEffects.TryClearDisplayAffinity(...)` expose best-effort helper paths; focused Windows integration tests verify native and high-level display-affinity behavior on the local OS.
   - Remaining work: capture-backed samples still need to prove there is no recursive overlay feedback and diagnostics should report whether capture exclusion succeeded, fell back, or is unsupported inside a future capture backend.

3. D3D11/DXGI texture render path.
   - Purpose: avoid unnecessary CPU copies and make the captured output usable as a GPU texture.
   - Implementation idea: add a D3D11-backed experimental backend or backend helper that can draw an `ID3D11Texture2D`/shader-resource view as a full-screen or cropped textured quad before normal overlay primitives.
   - API sketch: keep this internal at first; expose only capture-backed options and diagnostics.
   - Done when: texture blit, crop, scale, resize, and device recreation are covered by tests or local artifacts.

4. Output and target mapping.
   - Purpose: map target window/client coordinates onto the correct DXGI output texture.
   - Implementation idea: reuse `OverlayTarget` resolution for the target HWND/bounds, then select the DXGI output that contains the target or overlay. Convert between virtual desktop pixels, output-local pixels, overlay pixels, and DIPs.
   - API sketch: `OutputSelection.FromTarget`, `OutputSelection.Primary`, and `OutputSelection.FromMonitorHandle`.
   - Done when: single-monitor target mapping is correct and follow-up items exist for crossing monitors, mixed DPI, and negative-coordinate layouts.

5. Duplication lifecycle and recovery.
   - Purpose: keep the overlay alive across output changes and DXGI duplication loss.
   - Implementation idea: treat `DXGI_ERROR_ACCESS_LOST`, `DXGI_ERROR_INVALID_CALL`, device removed/reset, monitor mode changes, and display sleep as backend recreate triggers.
   - API sketch: reuse existing `DeviceLost`/`DeviceRestored` events plus capture-specific diagnostics such as `CaptureSourceLost`, `CaptureSourceRestored`, and `CaptureFrameSkipped`.
   - Done when: a forced/recreated duplication path can resume rendering without leaking frames or leaving the overlay black.

6. Immediate-present research mode.
   - Purpose: test whether a DXGI swap chain with immediate present and optional tearing reduces perceived overlay latency.
   - Implementation idea: measure a D3D11 flip-model swap chain against the current Direct2D HWND path and future DirectComposition backend. Keep the option experimental and off by default.
   - API sketch: `PresentMode.Immediate` and `PresentMode.ImmediateAllowTearing`, or a backend-specific experimental option.
   - Done when: benchmark and visual evidence show whether it helps without causing tearing, jitter, or excessive GPU usage.

7. Passive overlay plus interactive control surface.
   - Purpose: separate a click-through render overlay from an optional interactive configuration/control window.
   - Implementation idea: provide a sample that creates a passive overlay and a normal owned control window, rather than making the overlay itself responsible for every interaction.
   - API sketch: sample-level pattern first; possible future helper such as `OverlayControlWindow`.
   - Done when: users can inspect and change overlay settings without changing the passive overlay's click-through behavior.

8. Optional ImGui hosting sample.
   - Purpose: prove the backend can host third-party immediate-mode UI draw data when a user wants that style of controls.
   - Implementation idea: add only a sample or adapter after a D3D11 backend exists. Do not make ImGui part of the core API or required dependencies.
   - API sketch: `ModernOverlay.Samples.ImGuiHost` or an adapter package if there is enough demand.
   - Done when: ImGui can draw over the same frame as ModernOverlay primitives without owning the whole architecture.

9. Primitive batching benchmark.
   - Purpose: compare ModernOverlay's command forwarding and Direct2D realization against a simple batched D3D11 primitive path.
   - Implementation idea: add benchmarks for line/rectangle/arrow/circle batches, including a high-command-count HUD-like frame.
   - API sketch: benchmark-only at first.
   - Done when: performance data says whether a D3D11 primitive backend is worth building, or whether Direct2D batching is sufficient.

10. Fullscreen placement research.
   - Purpose: understand where supported topmost/no-activate/click-through overlays fail with fullscreen or borderless targets.
   - Implementation idea: validate normal HWND topmost behavior first, then compare DirectComposition, capture-backed rendering, and any supported Windows shell APIs. Keep undocumented window-band behavior out of the public package unless a supported route is found.
   - API sketch: none until there is a supported implementation.
   - Done when: docs explain which fullscreen scenarios are supported, degraded, or outside the library boundary.

## Non-Goals

Do not import or reproduce unrelated behavior from the research repo.

1. No game-specific modules.
2. No memory reading.
3. No process handle acquisition or handle hijacking.
4. No process injection for the supported NuGet path.
5. No anti-cheat bypass or stream-proofing claims.
6. No undocumented window-band dependency in stable APIs.

## Pipeline Sketch

The prototype should prove this pipeline before public API design:

1. Create or reuse a D3D11 device compatible with DXGI output duplication.
2. Select the DXGI output that contains the target window or overlay bounds.
3. Create an `IDXGIOutputDuplication` instance for that output.
4. On each frame, call `AcquireNextFrame` with a bounded timeout.
5. If a new frame exists, query the acquired `IDXGIResource` for `ID3D11Texture2D`.
6. Copy the relevant output texture region into a reusable shader-resource texture.
7. Release the duplication frame exactly once.
8. Begin the overlay render pass.
9. Draw the captured texture region first.
10. Draw normal overlay commands on top.
11. Present the overlay frame.
12. Recreate duplication resources on `DXGI_ERROR_ACCESS_LOST`, output changes, monitor movement, mode changes, or device recreation.

## Synchronization Model

This mode can synchronize overlay drawing to the most recent duplicated output frame. That can be useful when DWM alpha composition produces visible lag or artifacts.

Do not describe this as completely bypassing Windows presentation. Output duplication observes a composed output frame, and the overlay window still needs to present its final content. The value to measure is practical visual latency and artifact reduction, not a blanket compositor-bypass claim.

## Recursive Capture Avoidance

The prototype must prove that the overlay does not capture itself.

Candidate mechanisms:

1. Use `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` where supported.
2. Hide or move the overlay during capture only if it can be done without flicker, which is unlikely to be acceptable.
3. Crop from a captured output region that excludes the overlay when possible.
4. Document any Windows-version limits of display affinity.

Acceptance evidence must include a test or manual artifact showing no recursive overlay feedback.

## Window Band And Support Boundary

Some implementations combine capture-backed rendering with nonstandard window-band behavior. Treat this as a research note only.

The public library should not depend on undocumented or privileged window-band APIs for the normal NuGet path. If a prototype explores this, keep it isolated behind an experimental build flag or private sample, and document:

1. The exact API used.
2. Required privileges such as UIAccess if any.
3. Windows versions tested.
4. Failure behavior when the band cannot be applied.
5. Security, compatibility, and anti-cheat implications.

The supported package should prefer normal topmost/no-activate/click-through HWND behavior unless a fully supported API path is identified.

## Targeting And Cropping

The capture backend has to map between multiple coordinate spaces:

1. DXGI output coordinates.
2. Virtual desktop pixels.
3. Target window client pixels.
4. Overlay window pixels.
5. ModernOverlay DIPs.

The first prototype should support a single-output scenario. Multi-monitor support should come after the coordinate mapping is measured and documented.

Required follow-up cases:

1. Target window entirely on one monitor.
2. Target window crossing monitors.
3. Negative-coordinate monitor layout.
4. Mixed-DPI monitors.
5. Target resize and move while duplication is active.
6. Foreground/window-target reacquisition.

## Performance Questions

Measure this against the existing Direct2D HWND fallback and future DirectComposition work:

1. End-to-end overlay frame time.
2. Time spent in `AcquireNextFrame`.
3. Texture copy cost.
4. Cropping/scaling shader cost.
5. Present duration.
6. CPU usage at 60, 120, 144, and 240 Hz where available.
7. GPU copy/3D utilization.
8. Memory bandwidth and texture allocation churn.
9. Behavior when no new frame is available.
10. Behavior when the target is static versus constantly changing.

The prototype should avoid `timeBeginPeriod(1)` unless measurement proves it is needed. If used, scope it tightly and document the system-wide timer-resolution impact.

## Failure Modes

The backend must handle:

1. `DXGI_ERROR_WAIT_TIMEOUT` without busy-spinning.
2. `DXGI_ERROR_ACCESS_LOST` by recreating duplication.
3. `DXGI_ERROR_INVALID_CALL` by releasing/recreating the duplication path.
4. Device removed/reset.
5. Monitor mode changes.
6. Lock screen, display sleep, RDP, or secure desktop transitions.
7. Protected content appearing black or unavailable.
8. Output duplication unavailable on the current Windows/session configuration.
9. Overlay display-affinity unsupported or ignored.

Failures should surface through diagnostics rather than silently falling back to misleading "transparent" behavior.

## Security And Policy Boundary

This feature must stay focused on legitimate overlays for owned or authorized applications. Do not design API examples around game assistance, bypass, injection, or anti-cheat evasion.

Acceptable samples:

1. Owned demo window with moving graphics.
2. Desktop annotation overlay.
3. Stream/debug HUD over a project-owned rendering sample.

Avoid samples that target third-party competitive games.

## Acceptance Criteria

Do not promote this beyond experimental until the repository has:

1. A working prototype package or sample.
2. No recursive capture artifact on supported Windows versions.
3. Single-monitor crop/mapping correctness.
4. Basic target-window move/resize correctness.
5. Click-through and no-activate behavior.
6. Device-loss and duplication-recreation handling.
7. Benchmark comparison against Direct2D HWND fallback.
8. Manual visual artifacts for at least Windows 10 and Windows 11.
9. Clear docs that this is capture-backed reconstruction, not true transparency.
10. A decision on whether unsupported window-band behavior is excluded, isolated, or abandoned.

## Current Decision

Capture-backed overlays are a useful experimental idea, not part of the first alpha or stable v1 transparency contract. Revisit after the current Direct2D HWND path, `UpdateLayeredWindow` CPU-copy prototype, and DirectComposition/DXGI prototype have enough evidence for a fair comparison.
