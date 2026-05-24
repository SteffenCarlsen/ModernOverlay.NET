# ModernOverlay 1.1 UI Validation

Date: 2026-05-24
Validator: Codex local command gate
Branch: `feature/modernoverlay-1.1-interactive-ui-analysis`
Base commit before validation doc: `6d7db69`
Machine: `DESKTOP-PIO5DGP`
OS: Microsoft Windows 10 IoT Enterprise LTSC `10.0.19044`, 64-bit
SDK: `11.0.100-preview.4.26230.115`

## Command Evidence

| Check | Result | Evidence |
|---|---|---|
| Focused retained UI suite | Passed | `dotnet test tests\ModernOverlay.Tests\ModernOverlay.Tests.csproj --configuration Debug --filter FullyQualifiedName~OverlayUi --verbosity minimal` passed with 116 tests. |
| Win32 input plumbing suite | Passed | `dotnet test tests\ModernOverlay.Tests\ModernOverlay.Tests.csproj --configuration Debug --filter FullyQualifiedName~OverlayWindowThreadingTests --verbosity minimal` passed with 18 tests. |
| Full Release solution tests | Passed | `dotnet test ModernOverlay.sln --configuration Release --verbosity minimal` passed with 234 tests. |
| Interactive UI sample launch | Passed | `tools\Start-ModernOverlaySample.ps1 InteractiveUiOverlay` exited with code 0 after the sample's built-in timed run. |
| Release validation gate | Passed | `$env:MSBuildEnableWorkloadResolver='false'; tools\Invoke-ModernOverlayReleaseValidation.ps1` completed build, full tests, non-integration tests, pack, package-consumer smoke, package boundary checks, and transparency sample execution on the current working tree. |
| Root binlog retention | Passed | Five root `*.binlog` files were retained after validation. |

Release gate result files:

- Full test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-24_14_01_56_net11.0.trx`, 234/234 passed.
- Non-integration test result: `tests\ModernOverlay.Tests\TestResults\TaF_DESKTOP-PIO5DGP_2026-05-24_14_02_18_net11.0.trx`, 87/87 passed.

## Fixes Found During Validation

1. `ModernOverlay.sln` still contained a virtual `src` solution folder and `NestedProjects` entry. The release gate's solution-shape assertion rejected it, so the solution now keeps projects at the root.
2. The package-consumer smoke test could restore a stale global NuGet cache entry when local packages reused the same package version. The release validation script now gives the generated consumer an artifact-local `RestorePackagesPath`.
3. Earlier focused control validation found a `UiWindow` minimized-height restore issue; that fix is covered by `OverlayUiWindowTests`.

## Interactive UI Manual Visual Checklist

This checklist is the optional visual pass for `samples/InteractiveUiOverlay`. The command gate proves launch, build, input plumbing, tests, and package-consumer behavior; it does not replace human visual inspection on target desktops.

| Area | Expected Visual Check | 2026-05-24 Status |
|---|---|---|
| Selective click-through | Non-control overlay regions pass clicks through while controls receive pointer input. | Automated native hit-test coverage passed; full human visual pass not run. |
| Window chrome | Floating windows drag, resize, close, minimize, restore, and retain readable chrome. | Covered by tests and sample launch; full human visual pass not run. |
| Popups | Combo box, menus, context menus, and tooltips draw above normal content and dismiss predictably. | Covered by tests and sample launch; full human visual pass not run. |
| Text input | Text boxes receive focused text input, caret movement, selection, and disabled/read-only visuals. | Covered by tests and sample launch; full human visual pass not run. |
| Navigation | Tab, segmented, menu, list, combo, and keyboard focus cues are visible and ordered. | Covered by tests and sample launch; full human visual pass not run. |
| Theme/runtime changes | Default and alternate themes remain readable after runtime changes. | Covered by tests and sample launch; full human visual pass not run. |
| Bounds/DPI controls | Bounds and DPI movement controls update layout without crashes or obvious clipping. | Covered by tests and sample launch; full human visual pass not run. |
| Persistence | In-memory `IUiLayoutStore` restore keeps layout persistence interface-only. | Covered by tests and sample launch; full human visual pass not run. |

## Decision

1. Approved for local 1.1 MVP validation: yes.
2. Approved as production-ready: not applicable; the project is using a practical MVP/alpha release bar.
3. Remaining confidence gaps: human visual checks on more desktops, Windows 11, mixed-DPI monitor movement, fullscreen/borderless scenarios, and broader GPU/driver coverage.
4. Follow-up scope: general `ScrollViewer`, virtualization, UI Automation providers, IME composition, clipboard editing, and grapheme-aware text editing remain out of 1.1.
