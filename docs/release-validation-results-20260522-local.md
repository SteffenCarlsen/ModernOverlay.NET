# Release Validation Results

Date: 2026-05-22
Validator: Codex local command gate
Machine: `LAPTOP-F39LC9P0`
OS: Microsoft Windows 10 Home `10.0.19045`, 64-bit
GPU/driver: NVIDIA GeForce RTX 3050 Laptop GPU `32.0.15.7283`; AMD Radeon(TM) Graphics `30.0.14018.15002`
SDK: `11.0.100-preview.4.26230.115`
PowerShell: `7.5.5`

## Command Gate

- Command: `tools\Invoke-ModernOverlayReleaseValidation.ps1`
- Result: Passed
- Root binlogs retained: 5
- Full test result: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_06_28_net11.0.trx`, 84/84 passed
- Non-integration test result: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_06_42_net11.0.trx`, 44/44 passed
- Transparency sample: passed as part of the command gate
- Benchmark dry run: passed through `tools\Invoke-ModernOverlayReleaseValidation.ps1 -SkipTransparencySample -RunBenchmarkDry`; the log contains no BenchmarkDotNet issue markers
- Benchmark dry-run log: `benchmarks\ModernOverlay.Benchmarks\benchmark-dryrun-latest.log`
- Benchmark dry-run reports: `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260522-180655`

## Latest Local Command Gate

- Date/time: 2026-05-22 23:16 local
- Machine: `DESKTOP-PIO5DGP`
- OS: Microsoft Windows 10 IoT Enterprise LTSC `10.0.19044`, 64-bit
- GPU/driver: NVIDIA GeForce RTX 3080 `32.0.15.9174`; AMD Radeon(TM) Graphics `32.0.11024.2`
- SDK: `11.0.100-preview.4.26230.115`
- PowerShell: `7.6.0`
- Command: `$env:MSBuildEnableWorkloadResolver='false'; tools\Invoke-ModernOverlayReleaseValidation.ps1 -SkipTransparencySample -RunBenchmarkDry`
- Result: Passed
- Full test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_23_16_56_net11.0.trx`, 101/101 passed
- Non-integration test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_23_17_10_net11.0.trx`, 61/61 passed
- Pack result: passed and emitted exactly the five alpha packages: `ModernOverlay`, `ModernOverlay.Direct2D`, `ModernOverlay.Win32`, `ModernOverlay.Diagnostics`, and `ModernOverlay.Integration`
- Package metadata inspection: release gate now asserts all five alpha packages include `README.md` and release notes with the required `net11.0-windows` and DWM/color-key fallback caveats; `ModernOverlay.Integration.Experimental` was not packed
- Artifact manifest: `docs/release-artifacts-20260522-alpha.md` records the five package names, sizes, SHA-256 hashes, package-boundary caveats, and release caveats
- Benchmark dry run: passed; latest dry-run reports were written under `benchmarks\ModernOverlay.Benchmarks\BenchmarkDotNet.Artifacts\20260522-231721`
- Transparency sample: passed in the immediately preceding full command gate; skipped in this benchmark-dry gate

## Additional Feature-Completeness Validation

- Release build after cooperative image IPC updates: `dotnet build .\ModernOverlay.sln --configuration Release -m:1 -bl:{{}}`, passed with 0 warnings and 0 errors
- Full test result after cooperative image IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_23_26_net11.0.trx`, 88/88 passed
- Non-integration test result after cooperative image IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_22_52_net11.0.trx`, 48/48 passed
- Pack after cooperative image IPC updates: `dotnet pack .\ModernOverlay.sln --configuration Release --no-build -m:1 -bl:{{}}`, passed
- Full test result after cooperative geometry IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_28_25_net11.0.trx`, 89/89 passed
- Non-integration test result after cooperative geometry IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_27_59_net11.0.trx`, 49/49 passed
- Pack after cooperative geometry IPC updates: `dotnet pack .\ModernOverlay.sln --configuration Release --no-build -m:1 -bl:{{}}`, passed
- Full test result after cooperative gradient IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_33_13_net11.0.trx`, 90/90 passed
- Non-integration test result after cooperative gradient IPC updates: `tests\ModernOverlay.Tests\TestResults\bstaf_LAPTOP-F39LC9P0_2026-05-22_18_32_40_net11.0.trx`, 50/50 passed
- Pack after cooperative gradient IPC updates: `dotnet pack .\ModernOverlay.sln --configuration Release --no-build -m:1 -bl:{{}}`, passed
- Release build after moving drawing/resource primitives into `ModernOverlay.Drawing`: `dotnet build ModernOverlay.sln --configuration Release --no-restore -m:1`, passed with 0 warnings and 0 errors
- Focused namespace/resource/rendering tests after `ModernOverlay.Drawing` move: `dotnet test ModernOverlay.sln --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpecExampleCompileTests|FullyQualifiedName~DrawContextTests|FullyQualifiedName~DrawCommandSinkTests|FullyQualifiedName~OverlayResourceManagerTests|FullyQualifiedName~Direct2DRenderBackendTests|FullyQualifiedName~IntegrationCommandTests"`, passed 37/37
- Pack after `ModernOverlay.Drawing` move: `dotnet pack ModernOverlay.sln --configuration Release --no-build --no-restore -m:1`, passed and emitted `ModernOverlay`, `ModernOverlay.Win32`, `ModernOverlay.Direct2D`, `ModernOverlay.Diagnostics`, and `ModernOverlay.Integration` packages
- Release build after moving `WindowHandle`, `WindowBounds`, and `DpiScale` into `ModernOverlay.Windows`: `dotnet build ModernOverlay.sln --configuration Release --no-restore -m:1`, passed with 0 warnings and 0 errors
- Focused window/API tests after `ModernOverlay.Windows` move: `dotnet test ModernOverlay.sln --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpecExampleCompileTests|FullyQualifiedName~Win32StyleTests|FullyQualifiedName~TargetTrackingTests|FullyQualifiedName~OverlayWindowOptionsTests|FullyQualifiedName~OverlayWindowThreadingTests|FullyQualifiedName~Direct2DRenderBackendTests"`, passed 49/49
- Pack after `ModernOverlay.Windows` move: `dotnet pack ModernOverlay.sln --configuration Release --no-build --no-restore -m:1`, passed and emitted `ModernOverlay`, `ModernOverlay.Win32`, `ModernOverlay.Direct2D`, `ModernOverlay.Diagnostics`, and `ModernOverlay.Integration` packages
- Full command gate after namespace moves and foreground-test hardening: `tools\Invoke-ModernOverlayReleaseValidation.ps1`, passed on `DESKTOP-PIO5DGP`
- Full test result after namespace moves: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_22_32_29_net11.0.trx`, 91/91 passed
- Non-integration test result after namespace moves: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_22_32_42_net11.0.trx`, 51/51 passed
- Pack and transparency sample after namespace moves: passed as part of `tools\Invoke-ModernOverlayReleaseValidation.ps1`
- Full command gate after adding package-boundary assertions: `tools\Invoke-ModernOverlayReleaseValidation.ps1`, passed on `DESKTOP-PIO5DGP`
- Full test result after package-boundary assertions: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_22_35_10_net11.0.trx`, 91/91 passed
- Non-integration test result after package-boundary assertions: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-22_22_35_21_net11.0.trx`, 51/51 passed
- Package-boundary assertions: passed; stale packages are removed before pack, exactly five alpha packages are emitted, `ModernOverlay` includes the bundled Direct2D backend DLL/XML, and `ModernOverlay.Integration.Experimental` is not packed

## Manual Visual Checks

Manual visual release checks are not fully completed by this command-gate result. The transparency sample executed successfully, but this result does not by itself prove user-observed visual correctness, mixed-DPI movement, fullscreen behavior, or additional GPU/driver coverage. Use `docs/release-validation-results-template.md` for target-machine validation of windowing, rendering, target tracking, DPI/monitor movement, fullscreen behavior, and diagnostics.

## Decision

1. Approved for local MVP/alpha validation: yes.
2. Approved as production-ready: not applicable; this project is not using a production-readiness release bar.
3. Remaining confidence gaps: manual visual validation on more target environments; exact `UpdateLayeredWindow`/DirectComposition backend decisions remain documented fallback/future-backend work.
4. Follow-up issues: create target-environment validation entries for Windows 11, mixed-DPI monitors, fullscreen/borderless games, and additional GPU/driver combinations.
