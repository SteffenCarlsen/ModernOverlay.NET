# Tests

The test suite uses MSTest and is split between normal local tests and desktop-sensitive Windows integration tests.

## Common Commands

Run the default non-integration suite:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet test tests\ModernOverlay.Tests\ModernOverlay.Tests.csproj --configuration Release --filter "TestCategory!=WindowsIntegration" --logger trx
```

Run the full local suite:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet test tests\ModernOverlay.Tests\ModernOverlay.Tests.csproj --configuration Release --logger trx
```

## Test Areas

1. Public options, namespace imports, and package-shape examples.
2. Drawing/resource command forwarding and validation.
3. Direct2D backend behavior and recreation signals.
4. Win32 style, z-order, query, and display-affinity behavior.
5. Overlay owner-thread lifecycle and render-loop behavior.
6. Target tracking and target events.
7. Diagnostics and EventSource coverage.
8. Cooperative IPC protocol, named-pipe transport, tokens, ACLs, patches, limits, and multi-client behavior.

Read more: [release validation checklist](../docs/release-validation-checklist.md), [feature completeness](../docs/feature-completeness.md).
