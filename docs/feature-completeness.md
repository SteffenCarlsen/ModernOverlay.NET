# Feature Completeness

This matrix tracks non-benchmark spec coverage. Benchmarks remain a later performance-review gate.

| Area | Status | Evidence |
|---|---|---|
| Repository bootstrap | Implemented | Solution, central package management, analyzers, CI, `.gitignore`, dependency report |
| SharpDX removal guard | Implemented | MSBuild package guard and source/package tests |
| .NET 11 / .NET 10 fallback decision | Implemented | `docs/adr/0001-dotnet-11-preview-and-net10-contingency.md` |
| HWND lifecycle | Implemented | `Win32OverlayWindow`, owner thread, lifecycle tests |
| Input modes | Implemented | Click-through/interactive styles, pointer events, input sample |
| Transparency modes | Partial | DWM glass and layered alpha exist; `UpdateLayeredWindow` and DirectComposition remain unsupported |
| DPI handling | Implemented with manual QA remaining | Per Monitor V2, `WM_DPICHANGED`, conversion helpers, DPI docs |
| Direct2D backend | Implemented | Direct2D HWND backend, render target creation, resize/recreate, primitive tests |
| Resource system | Implemented | Descriptor handles, native realization tracking, leak report, recreation behavior |
| Drawing parity | Implemented with known `Draw.Box` interpretation | Shapes, text, images, geometry, strokes, clips, transforms, helpers |
| Render loop/lifecycle | Implemented | Owner-thread run loop, waitable timer, frame stats, pause/resume, exception policy |
| Target tracking | Implemented | HWND/PID/process/title/class/foreground/provider targets, sticky sample, loss/reacquire |
| Window helpers | Implemented | Query, z-order, effects, failure diagnostics |
| Diagnostics | Implemented | EventSource, logging adapter, diagnostics sample, native failure tracking |
| Samples | Implemented by capability | Basic, sticky, shapes, text layout, diagnostics, input, hotkey, image, transparency |
| Docs | Implemented draft | Quick start, installation, migration, troubleshooting, window modes, target tracking, DPI, drawing, resources, recreation, integration boundary |
| Optional integration package | Not implemented | Deferred by spec until post-parity cooperative integration work |

## Remaining Feature Risks

- Manual transparency validation is still required across representative Windows/GPU/DPI/fullscreen setups.
- `UpdateLayeredWindow` and DirectComposition are documented but not implemented.
- Package split still requires explicit Direct2D backend registration.
- Public API review is still needed before calling the surface stable.

