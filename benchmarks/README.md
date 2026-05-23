# Benchmarks

Benchmark projects live under `benchmarks/ModernOverlay.Benchmarks` and use BenchmarkDotNet with the in-process emit toolchain while .NET 11 preview support is still settling.

## Benchmark Classes

| Class | Measures |
|---|---|
| `DrawContextBenchmarks` | Public immediate-mode draw-dispatch overhead. |
| `Direct2DRenderBenchmarks` | Direct2D HWND clear/present, primitive/present, and resize paths. |
| `OverlayLifecycleBenchmarks` | Hidden overlay create/dispose and recreate/dispose paths. |
| `TargetTrackingBenchmarks` | Owned-HWND bounds queries, title/process discovery, custom provider resolution, and targeted hidden overlay creation. |

## Dry Validation

Use dry runs to prove benchmark discovery and execution:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*" --job Dry --noOverwrite
Pop-Location
```

Dry runs are not performance evidence. Use normal BenchmarkDotNet runs for actual baseline numbers.

## Baseline Runs

Run a single benchmark class without `--job Dry` when capturing comparable numbers:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet build benchmarks\ModernOverlay.Benchmarks\ModernOverlay.Benchmarks.csproj --configuration Release -m:1
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*TargetTrackingBenchmarks*" --noOverwrite
Pop-Location
```

Read more: [performance guide](../docs/performance-guide.md), [local baseline](../docs/performance-baseline-20260522-local.md).
