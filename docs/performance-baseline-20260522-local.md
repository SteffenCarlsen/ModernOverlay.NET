# Performance Baseline 2026-05-22/23 Local

This is the first recorded non-dry local performance baseline. It covers public immediate-mode draw-dispatch overhead, Direct2D HWND clear/present, Direct2D primitive/present, Direct2D resize, hidden overlay lifecycle paths, and target-tracking paths.

## Environment

1. Machine: `DESKTOP-PIO5DGP`.
2. OS: Microsoft Windows 10 IoT Enterprise LTSC `10.0.19044`, 64-bit.
3. CPU: AMD Ryzen 9 9950X3D 16-Core Processor.
4. RAM: 61.6 GB visible.
5. GPU: NVIDIA GeForce RTX 3080, driver `32.0.15.9174`.
6. GPU: AMD Radeon(TM) Graphics, driver `32.0.11024.2`.
7. PowerShell: `7.6.0`.
8. .NET SDK: `11.0.100-preview.4.26230.115`.
9. BenchmarkDotNet: `0.15.8`.
10. Toolchain: `InProcessEmitToolchain`.

## Command

The draw-dispatch benchmark was run from the repository root, so BenchmarkDotNet wrote those artifacts under the root-level `BenchmarkDotNet.Artifacts` directory instead of the benchmark project directory.

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet build benchmarks\ModernOverlay.Benchmarks\ModernOverlay.Benchmarks.csproj --configuration Release -m:1
dotnet run --project benchmarks\ModernOverlay.Benchmarks\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*DrawContextBenchmarks*" --noOverwrite
```

The Direct2D render benchmark was run from the benchmark project directory, so its artifacts are under `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts`.

```powershell
$env:MSBuildEnableWorkloadResolver='false'
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*Direct2DRenderBenchmarks*" --noOverwrite
Pop-Location
```

The overlay lifecycle benchmark was also run from the benchmark project directory.

```powershell
$env:MSBuildEnableWorkloadResolver='false'
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*OverlayLifecycleBenchmarks*" --noOverwrite
Pop-Location
```

The target-tracking benchmark was run from the benchmark project directory after the class was added.

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet build benchmarks\ModernOverlay.Benchmarks\ModernOverlay.Benchmarks.csproj --configuration Release -m:1
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*TargetTrackingBenchmarks*" --noOverwrite
Pop-Location
```

## Artifacts

1. Draw dispatch GitHub summary: `BenchmarkDotNet.Artifacts\20260522-233638\ModernOverlay.Benchmarks.DrawContextBenchmarks-report-github.md`.
2. Draw dispatch CSV report: `BenchmarkDotNet.Artifacts\20260522-233638\ModernOverlay.Benchmarks.DrawContextBenchmarks-report.csv`.
3. Draw dispatch HTML report: `BenchmarkDotNet.Artifacts\20260522-233638\ModernOverlay.Benchmarks.DrawContextBenchmarks-report.html`.
4. Direct2D GitHub summary: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005344\ModernOverlay.Benchmarks.Direct2DRenderBenchmarks-report-github.md`.
5. Direct2D CSV report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005344\ModernOverlay.Benchmarks.Direct2DRenderBenchmarks-report.csv`.
6. Direct2D HTML report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005344\ModernOverlay.Benchmarks.Direct2DRenderBenchmarks-report.html`.
7. Overlay lifecycle GitHub summary: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005822\ModernOverlay.Benchmarks.OverlayLifecycleBenchmarks-report-github.md`.
8. Overlay lifecycle CSV report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005822\ModernOverlay.Benchmarks.OverlayLifecycleBenchmarks-report.csv`.
9. Overlay lifecycle HTML report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-005822\ModernOverlay.Benchmarks.OverlayLifecycleBenchmarks-report.html`.
10. Target tracking GitHub summary: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-013344\ModernOverlay.Benchmarks.TargetTrackingBenchmarks-report-github.md`.
11. Target tracking CSV report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-013344\ModernOverlay.Benchmarks.TargetTrackingBenchmarks-report.csv`.
12. Target tracking HTML report: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260523-013344\ModernOverlay.Benchmarks.TargetTrackingBenchmarks-report.html`.

The project-level dry-run artifacts remain under `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts`.

## Draw Dispatch Results

| Method | Mean | Error | StdDev | Allocated |
|---|---:|---:|---:|---:|
| `Clear` | 2.6127 ns | 0.0812 ns | 0.0759 ns | 0 B |
| `DrawLine` | 0.2866 ns | 0.0341 ns | 0.0319 ns | 0 B |
| `FillRectangle` | 0.2765 ns | 0.0226 ns | 0.0211 ns | 0 B |
| `ClipScope` | 1.5174 ns | 0.0247 ns | 0.0231 ns | 0 B |
| `MeasureText` | 0.6037 ns | 0.0249 ns | 0.0233 ns | 0 B |

## Direct2D Render Results

| Method | Mean | Error | StdDev | Median | Gen0 | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| `ClearAndPresent` | 4,564.535 us | 90.7632 us | 240.6911 us | 4,554.145 us | - | 565 B |
| `DrawPrimitiveBatchAndPresent` | 4,450.942 us | 88.3770 us | 231.2673 us | 4,362.270 us | - | 552 B |
| `ResizeRenderTarget` | 3.539 us | 0.0834 us | 0.2458 us | 3.537 us | 0.0076 | 404 B |

## Overlay Lifecycle Results

| Method | Mean | Error | StdDev | Allocated |
|---|---:|---:|---:|---:|
| `CreateAndDisposeHiddenOverlay` | 138.9 ms | 2.77 ms | 5.21 ms | 14.22 KB |
| `CreateRecreateAndDisposeHiddenOverlay` | 231.4 ms | 4.55 ms | 6.80 ms | 16.29 KB |

## Target Tracking Results

| Method | Mean | Error | StdDev | Median | Allocated |
|---|---:|---:|---:|---:|---:|
| `QueryWindowBoundsByHwnd` | 18.9079 ns | 0.3763 ns | 0.5275 ns | 18.9178 ns | 0 B |
| `QueryClientBoundsByHwnd` | 33.7683 ns | 0.6827 ns | 0.8385 ns | 33.2940 ns | 0 B |
| `FindTargetByTitleContains` | 170,081.9187 ns | 6,405.3464 ns | 18,583.0728 ns | 172,693.1885 ns | 800 B |
| `FindTargetByProcessId` | 75,583.1546 ns | 2,558.5673 ns | 7,132.2636 ns | 74,133.0566 ns | 184 B |
| `ResolveCustomProvider` | 0.0004 ns | 0.0015 ns | 0.0014 ns | 0.0000 ns | 0 B |
| `CreateHiddenOverlayWithHwndTarget` | 122,549,875.0000 ns | 2,408,428.6937 ns | 2,773,550.0661 ns | 122,147,725.0000 ns | 14,362 B |

## Interpretation

1. This proves real non-dry BenchmarkDotNet runs for every current benchmark class on the current machine.
2. This does not prove DirectComposition, `UpdateLayeredWindow`, or capture-backed performance because those benchmark cases do not exist yet.
3. The in-process emit toolchain remains a preview-era compromise because BenchmarkDotNet/.NET 11 preview support is still not ideal for the default out-of-process SDK toolchain.
4. `ResolveCustomProvider` measures indistinguishable from the empty-method overhead. Keep it as a correctness/discovery sentinel rather than using it as meaningful timing evidence.
5. Title/process discovery results include desktop enumeration cost on the local machine and should be compared only against similarly captured runs on the same environment.
6. Future baseline work should add benchmark cases for DirectComposition, `UpdateLayeredWindow`, and capture-backed overlays when those implementations exist.
