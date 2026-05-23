# ModernOverlay Docs

Use this folder as the project manual. The docs are intentionally split by topic so the preview API can evolve without turning every change into a giant rewrite.

## Start Here

1. [Quick start](quick-start.md): create a minimal overlay, track a window, and handle pointer input.
2. [Installation](installation.md): package/project references, target framework, and backend package shape.
3. [GameOverlay.NET mapping](gameoverlay-migration.md): old concepts mapped to the new ModernOverlay API.
4. [A/B development testing](ab-development-testing.md): quick ways to play with samples, package consumers, and comparison knobs.
5. [Modernization spec](modernization-spec.md): original architecture and parity target that shaped the repository.

## Core Usage

1. [Window modes](window-modes.md): click-through, interactive, z-order, capture exclusion, and transparency modes.
2. [Target tracking](target-tracking.md): HWND/process/title/class/foreground/provider targets and tracking cadence.
3. [DPI and multi-monitor](dpi-and-multi-monitor.md): physical pixels, DIPs, DPI changes, and monitor movement.
4. [Drawing primitives](drawing-primitives.md): shapes, text, images, helpers, clips, and transforms.
5. [Resource lifetime](resource-lifetime.md): handles, native realizations, leak diagnostics, and hot-path resource guidance.

## Runtime Behavior

1. [Device loss and recreation](device-recreation.md): manual recreation, backend-requested recreation, and resource behavior.
2. [Troubleshooting](troubleshooting.md): common overlay, target, DPI, transparency, and render-callback issues.
3. [Performance guide](performance-guide.md): resource, target-tracking, frame pacing, and benchmark guidance.
4. [Performance baseline](performance-baseline-20260522-local.md): recorded local BenchmarkDotNet evidence.

## Integration And Research

1. [Integration boundary](integration-boundary.md): cooperative IPC, named-pipe security, payload limits, and non-goals.
2. [Transparency validation](transparency-validation.md): current transparency implementation, caveats, and manual validation.
3. [DirectComposition spike](directcomposition-spike.md): why DirectComposition is a future backend milestone.
4. [Capture-backed overlay spike](capture-backed-overlay-spike.md): experimental output-duplication reconstruction idea.
5. [ModernOverlay 1.1 interactive UI analysis](modernoverlay-1.1-interactive-ui-analysis.md): retained UI, dynamic placement, and control-system planning.
6. [ModernOverlay 1.1 interactive UI architecture](modernoverlay-1.1-interactive-ui-architecture.md): accepted UI architecture direction, review alignment, and implementation order.

## Release State

1. [Root task list](../Tasks.md): active alpha cleanup and follow-up checklist.
2. [Feature completeness](feature-completeness.md): current MVP/alpha coverage and remaining risks.
3. [Next action points](next-action-points.md): numbered milestone roadmap.
4. [Public API and package review](public-api-package-review.md): preview API/package decisions.
5. [Implementation history](implementation-history.md): public summary of important implementation decisions and tradeoffs.
6. [Development notes](development-notes.md): repository maintenance and validation habits.
7. [Release validation checklist](release-validation-checklist.md): command gate and manual validation checklist.
8. [Release publishing](release-publishing.md): tag-driven GitHub release and NuGet publishing setup.
9. [Definition of done audit](definition-of-done-audit.md): MVP/alpha release-bar audit.
10. [ModernOverlay 1.1 interactive UI tasks](modernoverlay-1.1-interactive-ui-tasks.md): separate checklist for the 1.1 UI feature track.
