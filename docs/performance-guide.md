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

## Frame Pacing

- `FrameRateLimit.Fixed(...)` uses the configured interval directly.
- `FrameRateLimit.Unlimited` renders continuously while still draining queued Win32 work and messages. It removes ModernOverlay's loop delay; it does not force the backend or the Windows compositor to present faster than they are willing to.
- `FrameRateLimit.DisplayDefault` rechecks the overlay HWND's current monitor refresh rate while the frame loop is running, so target frame pacing can follow monitor migration after the overlay moves or tracks a target onto another display.
- `PresentMode.BackendDefault` uses the backend's normal presentation path. With the current Direct2D HWND backend, that can be compositor/display paced and may look like a monitor-refresh cap even when `FrameRateLimit.Unlimited` is selected.
- `PresentMode.Immediate` requests immediate presentation from the backend. The Direct2D HWND backend maps this to `PresentOptions.Immediately`, but OS, driver, compositor, and windowing behavior can still affect the observed rate.
- `FrameStats.CurrentFramesPerSecond` and `FrameStats.ActualFrameInterval` describe completed-frame cadence, including presentation time. `FrameStats.LastFrameDuration`, `RenderDuration`, and `PresentDuration` are useful when you need to see whether rendering work or presentation is dominating the frame.

## Benchmarks

The benchmark project is under `benchmarks/ModernOverlay.Benchmarks`.

Use a dry run to validate benchmark correctness:

```powershell
Push-Location benchmarks\ModernOverlay.Benchmarks
dotnet run --project .\ModernOverlay.Benchmarks.csproj --configuration Release --no-build -- --filter "*" --job Dry --noOverwrite
Pop-Location
```

Dry runs are not performance evidence. Use normal or short BenchmarkDotNet jobs for actual measurements, and compare results across changes on the same machine.

The first scoped non-dry baseline is recorded in `docs/performance-baseline-20260522-local.md`. It covers `DrawContextBenchmarks`, `Direct2DRenderBenchmarks`, `OverlayLifecycleBenchmarks`, and `TargetTrackingBenchmarks`.

`TargetTrackingBenchmarks` adds owned-HWND target lookup coverage for window bounds, client bounds, title/process discovery, custom provider resolution, and hidden overlay creation with an HWND target. Repeat its non-dry run whenever target tracking changes are performance-sensitive.

If a repeatable regression is found, file it with `.github/ISSUE_TEMPLATE/performance-regression.yml` and include the baseline, current build, environment, BenchmarkDotNet summary, and user impact.
