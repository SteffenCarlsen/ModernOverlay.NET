# Transparency Validation

Date: 2026-05-22

## Current Implementation

- Overlay windows are created with `WS_EX_LAYERED` and `WS_EX_NOACTIVATE`.
- `OverlayInputMode.ClickThrough` adds `WS_EX_TRANSPARENT`.
- `TransparencyMode.Auto` currently applies `DwmExtendFrameIntoClientArea` with full-client negative margins.
- `TransparencyMode.DwmGlassFrame` uses the same DWM full-client extension explicitly.
- `TransparencyMode.LayeredWindowAttributes` is wired through `SetLayeredWindowAttributes` with full alpha.
- `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` intentionally throw `NotSupportedException` until their backends exist.

## Interpretation

The spec requires transparency validation before overcommitting to a renderer path. This pass adds the Win32 primitives needed for the spike but does not claim visual parity yet.

## Manual Validation Checklist

- Basic overlay appears borderless.
- Overlay does not activate when shown.
- Click-through mode passes mouse interaction to windows beneath.
- Interactive mode removes `WS_EX_TRANSPARENT`.
- `DwmGlassFrame` clears the client area as expected on Windows 10 and Windows 11.
- `LayeredWindowAttributes` preserves global alpha behavior but is not treated as proof of per-pixel Direct2D alpha.
- Resize preserves the transparent client area.
- DPI changes do not corrupt window bounds.

## Decision Status

Defaulting `Auto` to DWM glass is a provisional bootstrap decision. The Direct2D renderer should still validate clear-to-transparent behavior before the project treats DWM glass plus Direct2D HWND rendering as the final parity path.
