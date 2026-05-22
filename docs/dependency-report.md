# Dependency Report

Date: 2026-05-22

## Runtime

- Primary TFM: `net11.0-windows`
- SDK verified locally: `11.0.100-preview.4.26230.115`
- Stable contingency SDK present locally: `10.0.108`

## Pinned Packages

| Package | Version | Notes |
|---|---:|---|
| `Vortice.Direct2D1` | `3.8.3` | Direct2D package baseline. |
| `Vortice.DXGI` | `3.8.3` | DXGI package baseline. |
| `Vortice.DirectX` | `3.8.3` | Direct package reference added because the assembly is part of the usable Vortice baseline. |
| `Vortice.Mathematics` | `2.1.1` | Math helpers used by Vortice. |
| `Vortice.Win32` | `2.4.1` | Low-level Win32 binding package pinned for renderer/windowing use. |
| `MSTest` | `4.2.3` | Modern MSTest metapackage for tests. |
| `Microsoft.CodeAnalysis.NetAnalyzers` | `11.0.100-preview.4.26230.115` | Analyzer package aligned with the preview SDK. |
| `BenchmarkDotNet` | `0.15.8` | Pinned for the planned benchmark project. |

## Package Interpretation

`Vortice.DirectWrite` and `Vortice.WIC` were listed in the spec as direct package references, but NuGet flat-container checks returned 404 for both package IDs. A restore/build against the resolvable Vortice packages did not provide `Vortice.DirectWrite.dll` or `Vortice.WIC.dll` transitively. The current smoke test records those names as unresolved spec assumptions instead of pretending they are available.

## Smoke Status

- `net11.0-windows`: `dotnet build ModernOverlay.sln --configuration Release --no-restore` succeeded locally. `dotnet test ModernOverlay.sln --configuration Release --no-build --logger trx` passed 9 tests.
- `net10.0-windows`: not enabled in the project files. It remains a contingency path per ADR 0001.
