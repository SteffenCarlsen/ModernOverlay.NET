# DirectComposition Backend Spike

Date: 2026-05-22

## Decision

Keep the Direct2D HWND render-target backend as the parity backend for the current implementation. Do not make DirectComposition the default yet.

Do not build a DirectComposition/DXGI prototype in the MVP/alpha pass. The alpha milestone should finish and document the Direct2D HWND path first; DirectComposition gets its own backend milestone once the acceptance evidence below can be collected.

## Evidence From Current Implementation

- The internal backend abstraction already separates `OverlayWindow` from the concrete Direct2D HWND backend.
- Direct2D HWND rendering covers clear, shapes, dashed strokes, geometry paths, text, reusable text layouts, images, clips, transforms, resize, backend recreation, diagnostics, and benchmark dry runs.
- Transparency validation artifacts exist for DWM frame extension and layered-window alpha through `samples/TransparencyValidationOverlay`.
- BenchmarkDotNet dry runs currently cover Direct2D clear/present, primitive batch/present, resize, and overlay create/recreate/dispose lifecycle.

## DirectComposition Status

DirectComposition remains a future backend candidate. The repository does not currently include a DirectComposition/DXGI swap-chain backend, and `TransparencyMode.DirectComposition` currently falls back to DWM frame extension while emitting a `BackendFallback` diagnostic event.

Capture-backed output-duplication rendering is tracked separately in `docs/capture-backed-overlay-spike.md`. It may compete with DirectComposition for some visual-sync scenarios, but it is not a DirectComposition implementation and should not be treated as true transparency.

The spike did not promote DirectComposition because the required acceptance evidence is not yet available:

- transparent composition is unproven for this codebase;
- click-through/no-activate compatibility is unproven;
- resize and device-loss behavior are unimplemented;
- text/image/shape parity is unimplemented;
- benchmark comparison against Direct2D HWND is not available.

## Follow-Up Criteria

A later DirectComposition backend should not become default until it proves:

- transparent composition with the selected overlay window styles;
- click-through and no-activate behavior;
- resize and DPI behavior;
- device-loss recovery;
- text, image, shape, clip, transform, and geometry parity;
- equal or better benchmark results than the Direct2D HWND backend;
- acceptable implementation complexity for users and maintainers.

## Current Default

`TransparencyMode.Auto` remains mapped to DWM frame extension over the Direct2D HWND backend. This is a documented provisional default, not a final claim that DirectComposition is unnecessary.
