# Performance Guide

ModernOverlay is immediate-mode. The renderer is designed so common paths can warm up native resources and then reuse them, but user code still controls how much work happens per frame.

## Resource Lifetime

- Create brushes, fonts, images, stroke styles, geometry paths, and reusable text layouts outside the render callback.
- Dispose handles deterministically when no longer needed.
- Use `overlay.Resources.CreateLeakReport()` and `FrameStats.NativeResourceCount` while debugging resource churn.
- Backend recreation disposes native realizations and recreates them lazily from public descriptors.

## Text

- `Draw.Text(string, ...)` and `Measure.Text(string, ...)` are counted as transient text-layout work.
- Use `TextLayoutHandle` for repeated static, wrapped, aligned, or trimmed text.
- `OverlayWindowOptions.ExcessiveTextLayoutCreationThreshold` emits diagnostics when transient text layout creation is unexpectedly high.

## Images

- Image handles can be created from path, byte array, read-only memory, or stream. Streams are copied into the handle so backend recreation does not depend on caller-owned streams.
- Direct2D caches native bitmaps per image handle and frame index for the active backend generation.
- PNG, JPEG/JPG, and BMP are covered by tests through WIC. Additional formats depend on installed WIC codecs.

## Target Tracking

- Target discovery and bounds polling are throttled by `TargetTrackingInterval`.
- `TimeSpan.Zero` polls every frame and is useful for precision testing, but costs more than the 33 ms default.
- Process/title/class discovery can be more expensive than following a known HWND or a custom provider.

## Benchmarks

The benchmark project is under `benchmarks/ModernOverlay.Benchmarks`.

Use a dry run to validate benchmark correctness:

```powershell
dotnet run --project benchmarks\ModernOverlay.Benchmarks\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*" --job Dry --noOverwrite
```

Dry runs are not performance evidence. Use normal or short BenchmarkDotNet jobs for actual measurements, and compare results across changes on the same machine.

