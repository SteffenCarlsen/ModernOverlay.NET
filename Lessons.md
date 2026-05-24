# Lessons

## Build Diagnostics

- MSBuild binlogs are useful for diagnosing `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` failures, but they should not accumulate in the repository root.
- Keep only the five most recent relevant `*.binlog` files unless the user asks to preserve a specific diagnostic trace.
- Binlogs are ignored by git, so deleting older ones is a workspace cleanup step, not a source change.
- Add projects to `ModernOverlay.sln` with `dotnet sln ModernOverlay.sln add --in-root ...`; virtual solution folders and `NestedProjects` entries intentionally fail CI.
- Release preflight should prove external version availability before publishing: check local/remote tags, GitHub releases, NuGet package versions, and the release workflow package list.
- When package-consumer smoke tests use local packages with reused or preview versions, isolate restore output with an artifact-local `RestorePackagesPath`; otherwise the global NuGet cache can hide stale package contents.
- Adding a new published package requires both repo pipeline coverage and NuGet trusted publishing coverage. Verify the workflow packs the project and that nuget.org has a policy matching package owner, repository owner, repository, workflow file, and environment.

## ModernOverlay UI Samples

- If a draggable sample `UiWindow` appears to snap back while other windows move normally, check whether its fixed width/height exceeds the current overlay bounds. Placement clamping collapses the valid move range when the window is larger than the overlay, so sample windows should size adaptively before treating drag as a framework bug.
- For placed `UiWindow` resize bugs, verify that the manual placement size is updated during pointer movement. Frame-loop layout can run before pointer release and reapply stale placement bounds if only `Width`/`Height` changed.
- For `TextBox` caret/selection bugs, do not rely on fixed `fontSize * 0.56` character advances once text is rendered with proportional fonts. Caret, selection, scrolling, and click-to-caret should share measured text advances when available.
- For tooltip, popup, combo box, or context menu rendering issues, check whether activated `UiWindow` instances have climbed into the popup Z-index band. Floating windows should stay below `UiLayer.Popup` so transient UI draws above them.
- For tiny window chrome glyphs, prefer drawn geometry over text glyphs. Font metrics can pass origin-based tests while `-` or `x` still looks visually off-center.
- If an anchored or draggable `UiWindow` snaps to the far left and only moves vertically, check whether content measurement inflated the placement size beyond the overlay width. Explicit window dimensions should constrain child measurement before placement clamping runs.
- For raw `ToggleButton` visuals, do not rely on subtle inset marks. Checked state should read as a selected button, indeterminate state should have its own obvious indicator, and samples should expose the current state in the button text or adjacent status.
- Treat screenshot-reported UI precision bugs as shared geometry suspects first. Hit testing, caret placement, slider bounds, and popup placement usually belong in shared measurement, transform, bounds, or z-layer code rather than one-off component fixes.
- Keep the UI A/B sample useful as a validation tool, not just a showcase. Add visible state, labels, and layout previews when controls otherwise look inert or ambiguous.
- For retained text input, caret, selection, and scrolling should share measured text advances. Any fallback heuristic must be treated as a temporary approximation and tested against proportional text.

## PR Review And Triage

- External review severity labels are not evidence. Inspect the code path, classify each item as accepted, rejected, or deferred, and explain the reasoning in the PR when the review is non-trivial.
- Do not add cleanup code for owned parent-child event cycles unless an external publisher roots the subscriber or child independently. .NET collects unreachable cycles; false-positive leak fixes can add noise without reducing risk.
- Do not throw across Win32 `WindowProc` boundaries for routine message handling such as `WM_NCHITTEST`; record diagnostics and return a safe native result instead.
