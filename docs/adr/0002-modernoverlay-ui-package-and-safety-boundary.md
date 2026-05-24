# ADR 0002: ModernOverlay.UI Package and Safety Boundary

## Status

Accepted for the 1.1 interactive UI implementation.

## Context

ModernOverlay 1.1 adds a retained interactive UI layer with controls, layout, popups, text input, dynamic placement, theming, and selective click-through input regions. The existing core overlay package already owns windows, drawing primitives, target tracking, resources, and backend integration.

The UI layer will evolve faster than the core drawing/window surface. It also needs documentation that keeps the project boundary clear: ModernOverlay supports standalone overlays and cooperative/authorized integration patterns, but it does not implement game-specific panels, process-memory behavior, input synthesis, injection, hooks, stealth, anti-cheat bypass, protected-process bypass, or kernel integration.

The release target remains an MVP/alpha-quality library feature that is useful to experiment with locally. It is not a production-certified UI framework and does not promise exhaustive accessibility, international text input, virtualization, or benchmark coverage in the first 1.1 pass.

## Decision

Ship the retained UI layer as a separate `ModernOverlay.UI` project and NuGet package.

`ModernOverlay.UI` depends on the core `ModernOverlay` package for overlay windows, drawing, input events, target bounds, DPI conversion, and resource handles. It also references `ModernOverlay.Diagnostics` so UI diagnostics use the same EventSource path as the rest of the repository. The core package must not depend on `ModernOverlay.UI`.

Selective click-through integration is expressed through the core input-region resolver contract and `OverlayInputMode.SelectiveClickThrough`. Applications opt into selective input explicitly; attaching a UI root does not silently change overlay input behavior.

The core UI package includes an `IUiLayoutStore` abstraction for placement persistence, but it does not ship a built-in JSON or file-backed store in 1.1. Samples may use in-memory stores. Durable file-backed adapters can be revisited outside the core package after the interface is stable.

## Safety Boundary

The 1.1 UI package is generic overlay UI infrastructure only. It must stay independent of game-specific behavior and must not add any of the following:

- memory reading or writing for another process;
- input synthesis or automation against another application;
- DLL injection, hooks, or protected-process bypass;
- anti-cheat, DRM, capture-protection, or security-boundary bypasses;
- kernel-mode components;
- app-specific feature panels derived from a particular target program.

Samples and docs should remain neutral and demonstrate generic UI concepts.

## Release Scope

The 1.1 target is a complete retained UI MVP suitable for local experimentation:

- explicit `ui.Render(frame)` ownership;
- layout panels, floating windows, placement, and interface-only persistence;
- core controls, popups, menus, text input, keyboard focus, pointer capture, and selective click-through;
- built-in theme resources with customization and readability checks;
- honest documentation for deferred areas such as IME composition, clipboard editing, UI Automation providers, `ScrollViewer`, virtualization, and benchmark validation.

Benchmarks and broad validation remain end-of-feature gates, not blockers for each implementation slice.

## Consequences

The separate package keeps the core overlay API smaller and lets UI APIs iterate during 1.1 without implying the same stability level as the drawing/window primitives.

The one-way dependency direction keeps package layering understandable: core capabilities are reusable without the retained UI layer, while UI can compose those core capabilities.

The explicit safety boundary allows UI work to derive generic interaction patterns from prior experiments without carrying over program-specific or unsupported behavior.

The interface-only layout persistence decision avoids adding a premature JSON/file dependency and preserves caller control over where placement state is stored.
