# Lessons

## Build Diagnostics

- MSBuild binlogs are useful for diagnosing `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` failures, but they should not accumulate in the repository root.
- Keep only the five most recent relevant `*.binlog` files unless the user asks to preserve a specific diagnostic trace.
- Binlogs are ignored by git, so deleting older ones is a workspace cleanup step, not a source change.

## ModernOverlay UI Samples

- If a draggable sample `UiWindow` appears to snap back while other windows move normally, check whether its fixed width/height exceeds the current overlay bounds. Placement clamping collapses the valid move range when the window is larger than the overlay, so sample windows should size adaptively before treating drag as a framework bug.
