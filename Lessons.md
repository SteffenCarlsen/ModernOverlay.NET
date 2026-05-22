# Lessons

## Build Diagnostics

- MSBuild binlogs are useful for diagnosing `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` failures, but they should not accumulate in the repository root.
- Keep only the five most recent relevant `*.binlog` files unless the user asks to preserve a specific diagnostic trace.
- Binlogs are ignored by git, so deleting older ones is a workspace cleanup step, not a source change.
