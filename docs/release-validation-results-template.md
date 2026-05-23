# Release Validation Results

Date:
Validator:
Machine:
OS:
GPU/driver:
Monitor layout:
SDK:

## Command Gate

- `tools\Invoke-ModernOverlayReleaseValidation.ps1`
- Result:
- Root binlogs retained:
- TRX result path:

## Windowing

| Check | Result | Notes |
|---|---|---|
| Transparent overlay appears | Not run | |
| Clear-to-transparent works | Not run | |
| Click-through passes input to target below | Not run | |
| Interactive mode receives pointer input | Not run | |
| Overlay show does not steal focus | Not run | |
| Topmost mode behaves as expected | Not run | |
| Follow-target z-order works best-effort | Not run | |

## Rendering

| Check | Result | Notes |
|---|---|---|
| Text renders crisply | Not run | |
| Images render | Not run | |
| Dashed shapes render | Not run | |
| Geometry paths render | Not run | |
| Clip and transform scopes behave correctly | Not run | |
| Resize does not crash or leak native resources | Not run | |
| Manual recreation does not crash and resources draw again | Not run | |

## Targeting

| Check | Result | Notes |
|---|---|---|
| Whole-window target tracking works | Not run | |
| Client-area target tracking works | Not run | |
| Target minimize/restore behavior works | Not run | |
| Target lost/reacquired events fire | Not run | |
| Restricted/elevated/protected/fullscreen targets fail safely | Not run | |

## DPI and Monitors

| Check | Result | Notes |
|---|---|---|
| Per-monitor DPI changes resize backend correctly | Not run | |
| Mixed-DPI monitors behave acceptably | Not run | |
| Negative-coordinate monitor layouts behave acceptably | Not run | |

## Diagnostics

| Check | Result | Notes |
|---|---|---|
| Frame timing updates | Not run | |
| Resource counts update | Not run | |
| Target HWND/bounds diagnostics update | Not run | |
| Native failure diagnostics show useful context | Not run | |
| EventSource/logging adapter emits expected events | Not run | |

## Decision

- Approved for alpha validation:
- Remaining blockers:
- Follow-up issues:
