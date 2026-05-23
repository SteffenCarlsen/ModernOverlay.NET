# ModernOverlay Samples

Samples are small console projects that exercise one capability at a time. They are meant for local experimentation and validation, not as production app templates.

For quick comparison work, see the [A/B development testing guide](../docs/ab-development-testing.md).

Run a sample from the repository root:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet run --project samples\BasicOverlay\BasicOverlay.csproj --configuration Release
```

Or use the sample launcher:

```powershell
tools\Start-ModernOverlaySample.ps1 Basic
tools\Start-ModernOverlaySample.ps1 -List
```

For disposable A/B edits, copy a sample into `artifacts\playgrounds`:

```powershell
tools\New-ModernOverlayPlayground.ps1 -From Basic -Name Basic-A
tools\New-ModernOverlayPlayground.ps1 -From Basic -Name Basic-B
```

The playground generator makes a local editable `Program.cs` even when the original sample is a linked-source alias.

## Sample Map

| Sample | Shows |
|---|---|
| `BasicOverlay` | Minimal overlay creation, resources, render callback, and text/shape drawing. |
| `StickyTargetOverlay` | Target tracking against an owned helper window. |
| `StickyWindowOverlay` | Spec-named alias for sticky-window style target tracking. |
| `InputModeOverlay` | Click-through versus interactive pointer mode. |
| `InteractiveOverlay` | Spec-named alias for interactive input behavior. |
| `ShapesOverlay` | Lines, rectangles, circles, helpers, and geometry-style drawing. |
| `GeometryOverlay` | Spec-named alias for shape/geometry drawing. |
| `ImageOverlay` | Image loading and drawing. |
| `ImageAndTextOverlay` | Spec-named alias for image plus text composition. |
| `TextLayoutOverlay` | Reusable text layout handles and text measurement. |
| `DiagnosticsOverlay` | Frame stats, resource counts, native failure status, and diagnostics-oriented display. |
| `HotkeyOverlay` | Overlay hotkey registration and handling. |
| `TransparencyValidationOverlay` | Local visual validation for current transparency modes and fallbacks. |
| `IpcOverlayDemo` | Overlay-side cooperative named-pipe command host. |
| `SampleOwnedHost` | Owned-host sender for the cooperative IPC demo. |

## Pairing The IPC Samples

Start the overlay command host:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet run --project samples\IpcOverlayDemo\IpcOverlayDemo.csproj --configuration Release
```

Then run the owned sender in another terminal:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
dotnet run --project samples\SampleOwnedHost\SampleOwnedHost.csproj --configuration Release
```

The IPC samples use the supported cooperative model: a local owned process sends explicit draw commands to an overlay host. They do not inject into, hook, or bypass other processes.
