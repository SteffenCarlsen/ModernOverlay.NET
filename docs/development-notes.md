# Development Notes

These notes collect repository maintenance habits that are useful to contributors while the project is still moving toward an alpha release. They are intentionally practical and should stay short; long project status belongs in [next action points](next-action-points.md), [feature completeness](feature-completeness.md), or the root [task list](../Tasks.md).

## Build Diagnostics

- MSBuild binlogs are useful for diagnosing `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` failures, but they should not accumulate in the repository root.
- Binlogs are ignored by git. Removing old root-level `*.binlog` files is workspace cleanup, not a source change.
- Keep only recent, relevant binlogs unless a specific diagnostic trace needs to be preserved for an issue or release note.

## Solution Shape

- Keep `ModernOverlay.sln` as a classic solution containing real C# project entries only.
- Prefer `dotnet sln ModernOverlay.sln add --in-root ...` when adding projects. Earlier virtual solution-folder entries caused IDE load problems, so the filesystem layout provides grouping instead.

## Testing

- When changing public behavior or semantics, update the focused tests that encode the old behavior in the same pass.
- Treat tests as part of the contract. A passing implementation with stale expectations is not release-ready.
- GUI integration tests should be serialized unless they are proven isolated from process DPI, z-order, foreground-window, and desktop pixel state.

## Windows Integration Tests

- Tests that show, minimize, restore, or sample real HWNDs can leave short-lived compositor state behind.
- Prefer native test harness windows and explicit cleanup over broad UI framework dependencies when validating overlay pixels inside the same test host.

