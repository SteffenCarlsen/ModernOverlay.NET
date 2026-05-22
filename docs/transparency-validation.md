# Transparency Validation

Date: 2026-05-22

## Current Implementation

- Overlay windows are created with `WS_EX_LAYERED` and `WS_EX_NOACTIVATE`.
- `OverlayInputMode.ClickThrough` adds `WS_EX_TRANSPARENT`.
- `TransparencyMode.Auto` currently applies `DwmExtendFrameIntoClientArea` with full-client negative margins.
- `TransparencyMode.DwmGlassFrame` uses the same DWM full-client extension explicitly.
- `OverlayWindowOptions.EnableBlurBehind` calls `DwmEnableBlurBehindWindow` after applying the selected transparency mode.
- `TransparencyMode.LayeredWindowAttributes` is wired through `SetLayeredWindowAttributes` with full alpha.
- `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` intentionally throw `NotSupportedException` until their backends exist.
- `samples/TransparencyValidationOverlay` creates two side-by-side validation overlays for `DwmGlassFrame` and `LayeredWindowAttributes`.
- `docs/directcomposition-spike.md` records the current DirectComposition decision and the evidence still required before making it default.

## Interpretation

The spec requires transparency validation before overcommitting to a renderer path. This implementation has the Win32 primitives and runnable validation sample needed for manual QA, but it does not claim full visual parity across Windows versions, GPUs, fullscreen modes, DPI transitions, and multi-monitor setups until those checks are run.

## Manual Validation Checklist

- Run `samples/TransparencyValidationOverlay` on the target Windows version and GPU/driver combination.
- Basic overlay appears borderless.
- Overlay does not activate when shown.
- Click-through mode passes mouse interaction to windows beneath.
- Interactive mode removes `WS_EX_TRANSPARENT`.
- `DwmGlassFrame` clears the client area as expected on Windows 10 and Windows 11.
- `LayeredWindowAttributes` preserves global alpha behavior but is not treated as proof of per-pixel Direct2D alpha.
- Resize preserves the transparent client area.
- DPI changes do not corrupt window bounds.

## Decision Status

Defaulting `Auto` to DWM glass is a provisional bootstrap decision. Direct2D HWND rendering remains the current parity backend; DirectComposition remains a future candidate until the criteria in `docs/directcomposition-spike.md` are satisfied.
