# Tools

Tools are local validation helpers for the preview release process.

## Sample Launcher

`Start-ModernOverlaySample.ps1` runs samples by short name and sets the local .NET preview workload-resolver workaround for you:

```powershell
tools\Start-ModernOverlaySample.ps1 Basic
tools\Start-ModernOverlaySample.ps1 Shapes
tools\Start-ModernOverlaySample.ps1 Sticky
tools\Start-ModernOverlaySample.ps1 -List
```

## Playground Generator

`New-ModernOverlayPlayground.ps1` copies a sample into `artifacts\playgrounds` so you can compare variants without changing the canonical sample projects:

```powershell
tools\New-ModernOverlayPlayground.ps1 -From Basic -Name Basic-A
tools\New-ModernOverlayPlayground.ps1 -From Shapes -Name Shapes-B
tools\New-ModernOverlayPlayground.ps1 -List
```

The generated playground gets its own editable `Program.cs` and project references rewritten for the scratch folder, so linked-source alias samples are safe to fork.

## Release Validation

`Invoke-ModernOverlayReleaseValidation.ps1` is the main command gate. It validates solution shape, Release build, full tests, non-integration tests, pack output, package boundaries, package-consumer smoke restore/build/run, optional transparency sample execution, optional BenchmarkDotNet dry run, and binlog retention.

Full local gate:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry
```

Non-visual automation gate:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1 -SkipTransparencySample -RunBenchmarkDry
```

The gate is evidence for the hobbyist MVP/alpha release bar. It does not replace manual visual validation on additional Windows versions, monitor layouts, fullscreen/borderless targets, or GPU/driver combinations.

The package-consumer smoke check is intentionally small. It proves the emitted package can be restored by a scratch app, the intended namespace imports compile, removed v1 aliases stay absent, and the bundled Direct2D backend reaches the consumer output.

Read more: [release validation checklist](../docs/release-validation-checklist.md), [latest local result](../docs/release-validation-results-20260523-local.md).
