# Transparency Validation

Date: 2026-05-22

## Current Implementation

- Overlay windows are created with `WS_EX_LAYERED` and `WS_EX_NOACTIVATE`.
- `OverlayInputMode.ClickThrough` adds `WS_EX_TRANSPARENT`.
- `TransparencyMode.Auto` currently applies `DwmExtendFrameIntoClientArea` with full-client negative margins.
- `TransparencyMode.DwmGlassFrame` uses the same DWM full-client extension explicitly.
- `OverlayWindowOptions.EnableBlurBehind` calls `DwmEnableBlurBehindWindow` after applying the selected transparency mode.
- Direct2D HWND transparent clears are made usable with `SetLayeredWindowAttributes` color-key transparency for black clear pixels. This is a Direct2D HWND compatibility fallback, not true per-pixel alpha.
- `TransparencyMode.LayeredWindowAttributes` is wired through `SetLayeredWindowAttributes` with full alpha plus the transparent black color key.
- `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` currently fall back to DWM full-client extension, transparent black color-keying, and `BackendFallback` diagnostics until their dedicated backends exist.
- `samples/TransparencyValidationOverlay` creates four validation overlays for `DwmGlassFrame`, `LayeredWindowAttributes`, `UpdateLayeredWindow` fallback, and `DirectComposition` fallback.
- `TransparencyVisualTests.TransparentClearShowsUnderlyingDesktopContent` creates a native GDI background window, renders a transparent clear over it, and samples the desktop pixel to guard the local DWM/color-key pass-through behavior.
- `docs/directcomposition-spike.md` records the current DirectComposition decision and the evidence still required before making it default.
- `docs/capture-backed-overlay-spike.md` records a separate experimental idea: reconstruct the apparent background from `IDXGIOutputDuplication`, draw it as the first layer, then draw overlay content on top. That mode is not true transparency and is not part of the current alpha implementation.

## Interpretation

The spec requires transparency validation before overcommitting to a renderer path. This implementation has the Win32 primitives and runnable validation sample needed for manual QA, but it does not claim full visual parity across Windows versions, GPUs, fullscreen modes, DPI transitions, and multi-monitor setups until those checks are run.

The current DWM/color-key path is acceptable for the MVP/alpha milestone because it gives a usable transparent overlay and observable fallback diagnostics. It is still known work, not a finished transparency story: true per-pixel `UpdateLayeredWindow` CPU-copy rendering or a DirectComposition/DXGI backend should be implemented and compared before the transparency backend is considered stable.

A capture-backed overlay may also be worth prototyping, but it should be evaluated as an experimental reconstruction mode rather than a transparency mode. Its validation needs are different: no recursive self-capture, correct output cropping, target movement across monitor/DPI boundaries, duplication loss recovery, and latency comparison against compositor-backed paths.

## Manual Validation Checklist

- Run `samples/TransparencyValidationOverlay` on the target Windows version and GPU/driver combination.
- Basic overlay appears borderless.
- Overlay does not activate when shown.
- Click-through mode passes mouse interaction to windows beneath.
- Interactive mode removes `WS_EX_TRANSPARENT`.
- `DwmGlassFrame` clears the client area as expected on Windows 10 and Windows 11.
- `LayeredWindowAttributes` preserves global alpha behavior but is not treated as proof of per-pixel Direct2D alpha.
- `UpdateLayeredWindow` and `DirectComposition` requests emit `BackendFallback` and visually match the DWM-glass path.
- Black pixels are transparent under the current color-key fallback; avoid drawing intentional pure-black overlay content until a true per-pixel alpha backend exists.
- Resize preserves the transparent client area.
- DPI changes do not corrupt window bounds.

## Local Validation Evidence

Validated locally on 2026-05-22:

- OS: Microsoft Windows 10 Home, version `10.0.19045`, 64-bit.
- GPUs reported by WMI: NVIDIA GeForce RTX 3050 Laptop GPU driver `32.0.15.7283`; AMD Radeon(TM) Graphics driver `30.0.14018.15002`.
- Virtual screen during capture: `0,0,1920,1200`.
- Command: `dotnet run --project samples\TransparencyValidationOverlay\TransparencyValidationOverlay.csproj --configuration Release --no-build`.
- Result: sample exited with code `0`; the desktop capture shows all four validation overlays visible, borderless, and clear-through over desktop/browser content.
- Local artifact: `artifacts/transparency-validation-20260522/desktop-capture-four-modes.png` (ignored by git).
- Automated test: `dotnet test ModernOverlay.sln --configuration Release --no-build --logger trx -bl:{{}}` passed `TransparencyVisualTests.TransparentClearShowsUnderlyingDesktopContent` as part of the 75-test Windows validation run.

This validates the current machine and compositor path only. Windows 11, exclusive fullscreen, mixed-DPI monitor movement, and additional GPU/driver combinations still require release validation.

## Decision Status

Defaulting `Auto` to DWM glass plus transparent black color-keying is a provisional bootstrap decision. `UpdateLayeredWindow` and `DirectComposition` also fall back to that path today rather than failing overlay creation. Direct2D HWND rendering remains the current parity backend; DirectComposition remains a future candidate until the criteria in `docs/directcomposition-spike.md` are satisfied.

Capture-backed output-duplication rendering remains a research idea tracked in `docs/capture-backed-overlay-spike.md`. It should not be described as compositor transparency or promoted into the stable API until the repository has prototype evidence and a support-boundary decision for any nonstandard window-band behavior.
