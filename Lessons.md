# Lessons

## Build Diagnostics

- MSBuild binlogs are useful for diagnosing `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` failures, but they should not accumulate in the repository root.
- Keep only the five most recent relevant `*.binlog` files unless the user asks to preserve a specific diagnostic trace.
- Binlogs are ignored by git, so deleting older ones is a workspace cleanup step, not a source change.

## ModernOverlay UI Samples

- If a draggable sample `UiWindow` appears to snap back while other windows move normally, check whether its fixed width/height exceeds the current overlay bounds. Placement clamping collapses the valid move range when the window is larger than the overlay, so sample windows should size adaptively before treating drag as a framework bug.
- For placed `UiWindow` resize bugs, verify that the manual placement size is updated during pointer movement. Frame-loop layout can run before pointer release and reapply stale placement bounds if only `Width`/`Height` changed.
- For `TextBox` caret/selection bugs, do not rely on fixed `fontSize * 0.56` character advances once text is rendered with proportional fonts. Caret, selection, scrolling, and click-to-caret should share measured text advances when available.
- For tooltip, popup, combo box, or context menu rendering issues, check whether activated `UiWindow` instances have climbed into the popup Z-index band. Floating windows should stay below `UiLayer.Popup` so transient UI draws above them.
- For tiny window chrome glyphs, prefer drawn geometry over text glyphs. Font metrics can pass origin-based tests while `-` or `x` still looks visually off-center.
- If an anchored or draggable `UiWindow` snaps to the far left and only moves vertically, check whether content measurement inflated the placement size beyond the overlay width. Explicit window dimensions should constrain child measurement before placement clamping runs.
- For raw `ToggleButton` visuals, do not rely on subtle inset marks. Checked state should read as a selected button, indeterminate state should have its own obvious indicator, and samples should expose the current state in the button text or adjacent status.
