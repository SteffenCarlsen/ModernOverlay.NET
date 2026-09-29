# Release Validation Results

Date: 2026-09-29
Validator: Codex, local automated checks
Release: v1.1.3
Machine: DESKTOP-PIO5DGP
OS: Windows 10 IoT Enterprise LTSC, 10.0.19044
GPU/driver: NVIDIA GeForce RTX 3080, 32.0.15.9174; AMD Radeon Graphics, 32.0.11024.2
Monitor layout: Not recorded; no manual monitor-layout validation claimed
SDK: 11.0.100-rc.1.26425.128

## Command Gate

```powershell
$env:MSBuildEnableWorkloadResolver='false'
tools\Invoke-ModernOverlayReleaseValidation.ps1 -RunBenchmarkDry
```

Result: Passed, exit code 0.

| Check | Result | Evidence |
|---|---|---|
| Solution shape and Release build | Passed | Zero warnings and errors |
| Full test suite, including Windows integration | Passed | 283 passed, zero failed or skipped |
| Non-integration test subset | Passed | 93 passed, zero failed or skipped |
| Package output and metadata | Passed | Six expected packages; README, icons, transparency caveats, and bundled Direct2D DLL/XML checked |
| Package consumer | Passed | Isolated restore, build, execution, public API checks, and bundled backend output |
| Transparency sample | Passed | Six-second sample execution completed; no manual visual assessment |
| Benchmark dry run | Passed | 16 benchmarks executed; no benchmark issue markers |

Local evidence paths, ignored by Git:

- Gate log: `artifacts/release-1.1.3/validation.log`
- Full-suite TRX: `tests/ModernOverlay.Tests/TestResults/TaF_DESKTOP-PIO5DGP_2026-09-29_20_50_26_net11.0.trx`
- Non-integration TRX: `tests/ModernOverlay.Tests/TestResults/TaF_DESKTOP-PIO5DGP_2026-09-29_20_50_49_net11.0.trx`
- Benchmark log: `benchmarks/ModernOverlay.Benchmarks/benchmark-dryrun-latest.log`
- Root binlogs retained: five, as required by the gate.

The local gate uses the default development package version, `1.0.0`. The release workflow derives `1.1.3` from the tag and rebuilds the published artifacts with that version. Local success does not establish remote publication success.

## Manual Validation

The manual windowing, rendering, targeting, DPI/multi-monitor, and diagnostics checks from the [results template](release-validation-results-template.md) were not repeated. Automated Windows integration and sample execution passed; no additional hardware coverage or visual-quality claim is made.

The benchmark run verifies that the harness works with RC1. It is not a new performance baseline; the May 2026 measurements retain their original SDK attribution.

## Decision

- Local automated release checks passed for the SDK/analyzer and documentation update.
- No local validation blockers remain.
- GitHub CI, the release workflow, and public NuGet availability must be verified separately.
