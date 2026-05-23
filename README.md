<div align="center">

<img width="900" alt="ModernOverlay.NET logo" src="assets/modernoverlay-logo.png" />

[![CI](https://img.shields.io/github/actions/workflow/status/SteffenCarlsen/ModernOverlay.NET/ci.yml?branch=main&label=CI&logo=github)](https://github.com/SteffenCarlsen/ModernOverlay.NET/actions/workflows/ci.yml "CI workflow") [![Release](https://img.shields.io/github/actions/workflow/status/SteffenCarlsen/ModernOverlay.NET/release.yml?label=release&logo=github)](https://github.com/SteffenCarlsen/ModernOverlay.NET/actions/workflows/release.yml "Release workflow") [![NuGet](https://img.shields.io/nuget/v/ModernOverlay.NET.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/ModernOverlay.NET/ "ModernOverlay.NET on NuGet") [![Downloads](https://img.shields.io/nuget/dt/ModernOverlay.NET.svg)](https://www.nuget.org/packages/ModernOverlay.NET/ "Downloads on NuGet") [![Open issues](https://img.shields.io/github/issues-raw/SteffenCarlsen/ModernOverlay.NET.svg?logo=github)](https://github.com/SteffenCarlsen/ModernOverlay.NET/issues "Open issues on GitHub") [![Closed issues](https://img.shields.io/github/issues-closed-raw/SteffenCarlsen/ModernOverlay.NET.svg)](https://github.com/SteffenCarlsen/ModernOverlay.NET/issues?q=is%3Aissue+is%3Aclosed "Closed issues on GitHub") [![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/SteffenCarlsen/ModernOverlay.NET/blob/main/LICENSE "ModernOverlay.NET license")

![Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows) ![.NET 11 Preview](https://img.shields.io/badge/.NET-11_preview-512BD4?logo=dotnet)

</div>

ModernOverlay is a Windows-only overlay library for modern .NET. It replaces the useful parts of the [GameOverlay.NET](https://github.com/michel-pi/GameOverlay.Net) library stack with a new Vortice + Direct2D/DirectWrite/WIC + Win32 implementation.

This package is not a drop-in [GameOverlay.NET](https://github.com/michel-pi/GameOverlay.Net) replacement, but feature-wise is heavily inspired by the library from Michel. The API intentionally uses new names, explicit lifetimes, safer target tracking, and first-class diagnostics.

## Preview Status

This repository currently targets `net11.0-windows` on a .NET 11 preview SDK. APIs, package layout, backend registration, and packaging metadata may change before .NET 11 GA.

Package-facing caveats for the MVP/alpha release:

1. The common `ModernOverlay` package bundles the Direct2D backend assembly for the preview one-package path. `ModernOverlay.Direct2D` is still also emitted as a separate backend package.
2. `ModernOverlay.Integration.Experimental` is source-only for alpha and should not be published until there is a real authorized experimental provider.
3. `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` are request modes that currently fall back to the DWM/color-key Direct2D HWND path with diagnostics. True CPU-copy layered alpha and DirectComposition/DXGI per-pixel alpha remain future backend work.
4. The release bar is a hobbyist project aiming to be useful, buildable, sample-backed, and caveated.

## Quick Start

Install the `ModernOverlay.NET` package. The package includes the Direct2D backend assembly for the common path, and the core facade auto-discovers `ModernOverlay.Direct2D` when the assembly is present.

Package install:

```powershell
dotnet add package ModernOverlay.NET
```

Minimal overlay:

```csharp
using ModernOverlay;
using ModernOverlay.Drawing;
using ModernOverlay.Windows;

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Bounds = WindowBounds.FromPixels(100, 100, 640, 360),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Text("Hello overlay", font, white, new PointF(24, 24));
};

await overlay.RunAsync();
```

Drawing coordinates are DIPs. `WindowBounds` represents physical pixels unless constructed through the DIP helpers.

Read more: [quick start](docs/quick-start.md), [A/B development testing](docs/ab-development-testing.md), [installation](docs/installation.md), [DPI and multi-monitor](docs/dpi-and-multi-monitor.md).

## Features

| Feature | What is available now | Read more |
|---|---|---|
| Overlay window lifecycle | Borderless Win32 popup overlays with async create/run/dispose, manual recreation, show/hide, pause/resume, and owner-thread dispatch. | [window modes](docs/window-modes.md), [device recreation](docs/device-recreation.md) |
| Drawing | Immediate-mode clear, lines, rectangles, rounded rectangles, circles, ellipses, triangles, geometry paths, text, images, clips, transforms, and overlay helpers like boxes, corner boxes, crosshairs, and arrows. | [drawing primitives](docs/drawing-primitives.md), [resource lifetime](docs/resource-lifetime.md) |
| Target tracking | Follow HWNDs, process ids/names, titles, class names, foreground windows, or custom providers; track whole-window, client-area, or custom bounds. | [target tracking](docs/target-tracking.md), [troubleshooting](docs/troubleshooting.md) |
| Input modes | Click-through overlays by default, optional interactive mode with pointer move, button, and wheel events. | [window modes](docs/window-modes.md), [quick start](docs/quick-start.md) |
| Transparency | Usable DWM/color-key transparency for the Direct2D HWND backend, with diagnostic fallback events for reserved backend modes. | [transparency validation](docs/transparency-validation.md), [DirectComposition note](docs/directcomposition-spike.md) |
| DPI and monitors | Per Monitor V2 awareness, physical-pixel window bounds, DIP drawing coordinates, DPI conversion helpers, negative-coordinate monitor support, and display-default frame pacing. | [DPI and multi-monitor](docs/dpi-and-multi-monitor.md) |
| Diagnostics | EventSource diagnostics, Microsoft.Extensions.Logging bridge, frame stats, target stats, native failure tracking, resource leak reports, and a diagnostics sample. | [troubleshooting](docs/troubleshooting.md), [performance guide](docs/performance-guide.md) |
| Cooperative IPC | Optional owned-host command protocol over named pipes, reusable remote resources, command patches, payload limits, command tokens, current-user/custom pipe ACLs, and bounded multi-client handling. | [integration boundary](docs/integration-boundary.md) |
| Performance evidence | BenchmarkDotNet harness, dry-run gate, local non-dry baselines for current classes, and a performance regression template. | [performance guide](docs/performance-guide.md), [baseline](docs/performance-baseline-20260522-local.md) |

## Performance Snapshot

The current local baseline was captured on Windows 10 IoT Enterprise LTSC `10.0.19044`, AMD Ryzen 9 9950X3D, NVIDIA RTX 3080, .NET SDK `11.0.100-preview.4.26230.115`, and BenchmarkDotNet `0.15.8` with the in-process emit toolchain. Treat these as same-machine comparison numbers, not cross-hardware guarantees.

| Area | Benchmark | Mean | Allocation |
|---|---|---:|---:|
| Draw dispatch | `Clear` | 2.6127 ns | 0 B |
| Draw dispatch | `DrawLine` | 0.2866 ns | 0 B |
| Draw dispatch | `FillRectangle` | 0.2765 ns | 0 B |
| Direct2D render | `ClearAndPresent` | 4,564.535 us | 565 B |
| Direct2D render | `DrawPrimitiveBatchAndPresent` | 4,450.942 us | 552 B |
| Direct2D render | `ResizeRenderTarget` | 3.539 us | 404 B |
| Overlay lifecycle | `CreateAndDisposeHiddenOverlay` | 138.9 ms | 14.22 KB |
| Target tracking | `QueryWindowBoundsByHwnd` | 18.9079 ns | 0 B |
| Target tracking | `FindTargetByProcessId` | 75,583.1546 ns | 184 B |

Read more: [performance guide](docs/performance-guide.md), [local baseline](docs/performance-baseline-20260522-local.md), [benchmarks index](benchmarks/README.md).

## License

ModernOverlay is licensed under the [MIT License](LICENSE).

## Minimal Examples

Track an existing window by title:

```csharp
var target = WindowTarget.ByTitle("Notepad")
    .WithBoundsMode(TargetBoundsMode.ClientArea);

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Target = target,
    ZOrder = OverlayZOrder.FollowTarget,
    InputMode = OverlayInputMode.ClickThrough,
});
```

Draw common overlay markers:

```csharp
overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.CornerBox(new RectF(40, 40, 160, 90), white, cornerLength: 18, strokeWidth: 2);
    frame.Draw.Crosshair(new PointF(320, 180), size: 14, white, strokeWidth: 2);
};
```

Use cooperative IPC from an owned process:

```csharp
var client = new NamedPipeOverlayCommandClient("modern-overlay-demo", commandToken: "local-token");
await client.SendAsync(OverlayCommandMessage.Update(
[
    OverlayDrawCommand.Clear(ColorRgba.Transparent),
    OverlayDrawCommand.TextRun("owned host", new PointF(24, 24), ColorRgba.White),
]));
```

Read more: [target tracking](docs/target-tracking.md), [drawing primitives](docs/drawing-primitives.md), [integration boundary](docs/integration-boundary.md).

## Samples

Read more: [samples index](samples/README.md).

Quick launcher:

```powershell
tools\Start-ModernOverlaySample.ps1 Basic
tools\Start-ModernOverlaySample.ps1 -List
tools\New-ModernOverlayPlayground.ps1 -From Basic -Name Basic-A
```

| Sample | Purpose |
|---|---|
| `samples/BasicOverlay` | Minimal render loop and drawing setup. |
| `samples/StickyTargetOverlay` / `samples/StickyWindowOverlay` | Target tracking against an owned test window. |
| `samples/InputModeOverlay` / `samples/InteractiveOverlay` | Click-through versus interactive input behavior. |
| `samples/ShapesOverlay` / `samples/GeometryOverlay` | Shape, helper, and geometry drawing. |
| `samples/ImageOverlay` / `samples/ImageAndTextOverlay` | Image and text rendering. |
| `samples/TextLayoutOverlay` | Reusable text layouts. |
| `samples/DiagnosticsOverlay` | Frame, resource, target, and native diagnostics. |
| `samples/ShowcaseOverlay` | Screenshot-oriented primitive gallery with live developer metrics. |
| `samples/HotkeyOverlay` | Overlay hotkey handling. |
| `samples/TransparencyValidationOverlay` | Manual transparency validation. |
| `samples/IpcOverlayDemo` and `samples/SampleOwnedHost` | Cooperative named-pipe command protocol. |

## Documentation

Read more: [docs index](docs/README.md).

1. Start here: [quick start](docs/quick-start.md), [A/B development testing](docs/ab-development-testing.md), [installation](docs/installation.md), [GameOverlay.NET mapping](docs/gameoverlay-migration.md).
2. Core usage: [window modes](docs/window-modes.md), [target tracking](docs/target-tracking.md), [DPI and multi-monitor](docs/dpi-and-multi-monitor.md), [drawing primitives](docs/drawing-primitives.md), [resource lifetime](docs/resource-lifetime.md).
3. Runtime behavior: [device recreation](docs/device-recreation.md), [troubleshooting](docs/troubleshooting.md), [performance guide](docs/performance-guide.md).
4. Integration and boundaries: [integration boundary](docs/integration-boundary.md), [transparency validation](docs/transparency-validation.md), [capture-backed overlay spike](docs/capture-backed-overlay-spike.md), [DirectComposition decision note](docs/directcomposition-spike.md).
5. Release/project status: [task list](Tasks.md), [modernization spec](docs/modernization-spec.md), [feature completeness](docs/feature-completeness.md), [next action points](docs/next-action-points.md), [implementation history](docs/implementation-history.md), [development notes](docs/development-notes.md), [public API and package review](docs/public-api-package-review.md), [release validation checklist](docs/release-validation-checklist.md), [release publishing](docs/release-publishing.md).

## Project Layout

1. [src](src/README.md): package/project responsibilities and source entry points.
2. [samples](samples/README.md): runnable capability demos.
3. [tests](tests/README.md): test areas and common commands.
4. [benchmarks](benchmarks/README.md): BenchmarkDotNet classes and dry-run command.
5. [tools](tools/README.md): release validation command gate.
