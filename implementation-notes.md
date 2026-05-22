# Implementation Notes

## Design Decisions

- The initial implementation targets the spec's Phase 0 and early Phase 1 architecture slice: solution layout, central package management, nullable/analyzer settings, CI, public API skeleton, Win32 style/lifecycle foundation, Direct2D dependency proof, smoke tests, and documentation artifacts.
- `ModernOverlay` is the public facade and currently references `ModernOverlay.Win32` directly so `OverlayWindow.CreateAsync` is usable immediately. A later backend composition layer can invert this dependency if the API surface stabilizes around multiple windowing hosts.
- The first `DrawContext` is a no-op command surface with validation for clip/transform stack balance. This lets samples and tests compile against the intended immediate-mode API before the Direct2D command sink is implemented.
- `FrameRateLimit.DisplayDefault` currently maps to 60 FPS for scheduling. The real display refresh query is deferred until the render loop moves fully onto the native owner thread.
- MSTest is used for the first test project because the installed .NET skill guidance recommends the modern `MSTest` metapackage for new projects.
- HWND creation, style mutation, destruction, and render callbacks now marshal through a dedicated Win32 owner thread. The owner thread initializes COM and waits with `MsgWaitForMultipleObjectsEx` on a work signal plus the Windows message queue.
- `TransparencyMode.Auto` currently maps to full-client `DwmExtendFrameIntoClientArea`. That is a provisional bootstrap default and must still be validated visually with the real Direct2D renderer.

## Deviations

- The spec lists `Vortice.DirectWrite` and `Vortice.WIC` as direct package references, but those package IDs do not currently resolve from NuGet and no matching assemblies were present through package closure. The implementation pins the resolvable Vortice packages (`Vortice.Direct2D1`, `Vortice.DXGI`, `Vortice.DirectX`, `Vortice.Mathematics`, `Vortice.Win32`) and records DirectWrite/WIC as unresolved spec package assumptions.
- Render scheduling is still driven by managed `PeriodicTimer` and then marshaled to the owner thread. The spec's preferred final shape is a native-owner-thread scheduler using a waitable timer or equivalent inside the message loop.
- The SharpDX guard is split between an MSBuild package-reference guard and a test that scans source/project files. This satisfies build/test enforcement without introducing a custom MSBuild file-content scanner.
- Generated solution format is the classic `.sln`, even though the .NET 11 SDK defaults to `.slnx`, because the spec explicitly names `ModernOverlay.sln`.

## Tradeoffs

- I used direct P/Invoke for the initial HWND lifecycle rather than wrapping every call through Vortice.Win32. This keeps the native boundary explicit and testable while still pinning `Vortice.Win32` for APIs the renderer will use.
- I used raw `nint` handles in the internal Win32 layer for the first pass. Public API exposes `WindowHandle`; a SafeHandle wrapper can be introduced once ownership and handle transfer rules are finalized.
- The initial Direct2D project contains a dependency probe instead of a renderer. This reduces risk while proving the .NET 11/Vortice package baseline before locking the transparent rendering strategy.
- DWM glass and layered-alpha experiments are implemented as narrow Win32 methods instead of a broader transparency service. That keeps the spike small until the renderer backend proves which path is actually needed.

## Open Questions

- Confirm whether the first production renderer should use Direct2D HWND render targets as the parity backend, or whether we should move directly to a DirectComposition/DXGI swap-chain spike before implementing drawing primitives.
- Confirm whether `.NET 11 preview` should remain the only TFM on `main`, or whether you want a checked-in `net10.0-windows` branch/worktree before renderer work begins.
- Confirm whether the public package split should remain four projects for alpha, or whether `ModernOverlay.Diagnostics` and `ModernOverlay.Win32` should stay internal implementation assemblies until API review.

## Validation

- `dotnet restore ModernOverlay.sln -bl:{{}}` succeeded after one transient NuGet timeout retry.
- `dotnet build ModernOverlay.sln --configuration Release --no-restore -bl:{{}}` succeeded with 0 warnings and 0 errors.
- `dotnet test ModernOverlay.sln --configuration Release --no-build --logger trx -bl:{{}}` passed 11 tests.
