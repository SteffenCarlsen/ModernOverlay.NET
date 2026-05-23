# A/B Development Testing

Use this page when you want to play with ModernOverlay locally and compare small API or rendering changes without turning it into a release exercise.

## Current Good Baseline

1. Latest local command-gate evidence: `docs/release-validation-results-20260523-local.md`.
2. Latest package manifest: `docs/release-artifacts-20260523-alpha.md`.
3. Latest non-dry benchmark baseline: `docs/performance-baseline-20260522-local.md`.
4. The release gate now includes a package-consumer smoke app that restores the emitted `ModernOverlay` package, compiles the intended `ModernOverlay`, `ModernOverlay.Drawing`, and `ModernOverlay.Windows` imports, checks that removed v1 aliases stay absent, and verifies the bundled Direct2D backend reaches the consumer output.

## Fastest Way To Play

Run one sample and edit it directly:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet run --project samples\BasicOverlay\BasicOverlay.csproj --configuration Release
```

Or use the sample launcher:

```powershell
tools\Start-ModernOverlaySample.ps1 Basic
tools\Start-ModernOverlaySample.ps1 Shapes
tools\Start-ModernOverlaySample.ps1 Sticky
tools\Start-ModernOverlaySample.ps1 -List
```

If you want to keep the canonical samples clean, create a scratch playground:

```powershell
tools\New-ModernOverlayPlayground.ps1 -From Basic -Name Basic-A
tools\New-ModernOverlayPlayground.ps1 -From Shapes -Name Shapes-B
```

Playgrounds are copied into `artifacts\playgrounds`, get their own editable `Program.cs`, and rewrite project references for that scratch location. This also makes the spec-named alias samples usable as independent starting points.

Good A/B sample choices:

1. `samples\BasicOverlay`: resource creation, render callback, text, and a simple shape.
2. `samples\ShapesOverlay`: helper drawing such as boxes, corner boxes, crosshairs, arrows, and geometry-style primitives.
3. `samples\StickyTargetOverlay`: target tracking against an owned helper window.
4. `samples\InputModeOverlay`: click-through versus interactive pointer handling.
5. `samples\TransparencyValidationOverlay`: current transparency modes and fallback behavior.
6. `samples\IpcOverlayDemo` plus `samples\SampleOwnedHost`: cooperative named-pipe command ingestion.

## Local Package Consumer

If you want to test as a package consumer instead of a project-reference consumer:

1. Use the packages already emitted under `src\*\bin\Release`.
2. Create a scratch app targeting `net11.0-windows`.
3. Add the local package folder as a NuGet source.
4. Reference only:

```xml
<PackageReference Include="ModernOverlay" Version="0.1.0-preview" />
```

Minimal package-consumer imports:

```csharp
using ModernOverlay;
using ModernOverlay.Drawing;
using ModernOverlay.Windows;
```

This is the intended common path. `ModernOverlay` should bring the Direct2D backend assembly along for the current preview package.

## Useful A/B Knobs

1. `OverlayInputMode.ClickThrough` versus `OverlayInputMode.Interactive`.
2. `OverlayZOrder.TopMost` versus `OverlayZOrder.FollowTarget`.
3. `FrameRateLimit.Fixed(...)`, `FrameRateLimit.DisplayDefault`, and `FrameRateLimit.Unlimited`.
4. `PresentMode.BackendDefault` versus `PresentMode.Immediate`.
5. `TargetBoundsMode.Window`, `TargetBoundsMode.ClientArea`, and `WithCustomBounds(...)`.
6. `TransparencyMode.Auto`, `DwmGlassFrame`, `LayeredWindowAttributes`, `UpdateLayeredWindow`, and `DirectComposition`.
7. `OverlayWindowOptions.ExcludeFromCapture`.
8. Reused resources versus creating resources inside the render callback.
9. `Draw.Rectangle`, `Draw.Box`, `Draw.CornerBox`, `Draw.Crosshair`, and geometry paths for visual-marker experiments.

## Practical Notes

1. Drawing coordinates are DIPs; `WindowBounds` is physical pixels unless you use the DIP conversion helpers.
2. Exact title tracking is `WindowTarget.ByTitle(title, MatchMode.Exact)`.
3. Keep `OverlayWindow` in the root `ModernOverlay` namespace, and window configuration in `ModernOverlay.Windows`.
4. `FrameRateLimit` controls ModernOverlay's loop delay; `PresentMode` controls what the backend asks for during presentation. Use `FrameRateLimit.Unlimited` plus `PresentMode.Immediate` when comparing uncapped behavior.
5. `UpdateLayeredWindow` and `DirectComposition` are still fallback request modes, not exact backend implementations.
6. Capture-backed output-duplication rendering is documented research, not a shipped backend.
7. The current alpha bar is playful and practical: buildable, sample-backed, and useful for experimenting.

## When You Want To Compare Changes

1. Start from one sample.
2. Make only one rendering/windowing/API change at a time.
3. Save a screenshot or short note for each variant.
4. Keep the variant that feels better to use, then promote it into docs/tests later.

## Tiny A/B Loop

1. Run `tools\Start-ModernOverlaySample.ps1 Basic`.
2. Change one visual or option in `samples\BasicOverlay\Program.cs`.
3. Run the same launcher command again.
4. Move to `Shapes`, `Sticky`, `Input`, or `Transparency` when you want to compare a specific subsystem.

For less churn in the sample tree:

1. Create `Basic-A` and `Basic-B` with `tools\New-ModernOverlayPlayground.ps1`.
2. Edit each playground differently.
3. Run each playground directly with `dotnet run --project artifacts\playgrounds\<name>\<name>.csproj --configuration Release`.
4. Promote the better variant back into a real sample or doc snippet later.
