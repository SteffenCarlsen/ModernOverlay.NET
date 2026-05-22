# GameOverlay.NET Modernization Implementation Specification

**Date:** 2026-05-22  
**Prepared for:** modernization of GameOverlay.NET into a modern Windows/.NET overlay library  
**Working project name:** `ModernOverlay`  
**Target platform:** Windows desktop only  
**Primary target framework:** `net11.0-windows`  
**Stable contingency target:** `net10.0-windows` if .NET 11 preview or Vortice compatibility blocks implementation  
**Primary rendering stack:** Vortice + Direct2D + DirectWrite + WIC + Win32  
**Compatibility goal:** functional parity with GameOverlay.NET, not API drop-in compatibility  
**Security posture:** standalone and cooperative/authorized integration only; no stealth, anti-cheat bypass, protected-process bypass, or kernel-level integration  

---

## 0. Summary of material changes from the reviewed draft

This revision keeps the original direction—Windows-only, Vortice-based, Direct2D-first, not a drop-in replacement—but makes the plan more implementable and less risky.

| Area | Change |
|---|---|
| Runtime baseline | Keeps `net11.0-windows` as the primary target, but defines `net10.0-windows` as a stable contingency branch and CI validation target while .NET 11 remains preview. |
| Dependency baseline | Updates current package assumptions: `Vortice.Win32` has moved beyond the version in the prior draft; all Vortice package versions must be pinned centrally and verified in Phase 0. |
| Window transparency | Adds a required transparency validation spike. Direct2D HWND render target, DWM glass extension, layered-window alpha, and DirectComposition have different composition behavior; parity must be proven before overcommitting to one path. |
| Rendering architecture | Makes the Direct2D HWND backend the parity backend, but explicitly designs the backend interface around a future DirectComposition/DXGI swap-chain backend. |
| Resource lifetime | Strengthens the native-resource model with explicit device-independent descriptors, device-dependent realizations, generation IDs, debug leak tracking, and predictable recreation behavior. |
| Threading | Defines a single owner thread per overlay by default, with explicit COM initialization, message pump, waitable timer scheduling, and no cross-thread access to Direct2D resources. |
| Public API | Refines API naming, introduces a command-sink concept, and separates immediate-mode drawing from future IPC/command-list ingestion. |
| Target tracking | Makes sticky overlays a target-tracking subsystem rather than a separate window type. Adds target reacquisition and z-order limitations. |
| Diagnostics | Adds first-class frame diagnostics, native handle/resource tracking, optional ETW/EventSource, and structured logging. |
| Testing | Adds Windows-only integration test categories, visual regression snapshots where practical, Win32 style assertions, device-loss/recreate tests, and CI skip rules for headless runners. |
| Safety | Keeps optional integration extension points but narrows them to owned/authorized applications and sample hosts. The core package must never require or perform injection. |

---

## 1. Executive summary

Build a new Windows-only C# overlay library that preserves the useful model of GameOverlay.NET—simple transparent overlays with immediate-mode 2D drawing—while replacing SharpDX with Vortice and modernizing the architecture around predictable resource ownership, safer lifecycle behavior, better diagnostics, and extensible rendering backends.

The recommended core stack is:

- **.NET 11 preview as the forward-looking target:** `net11.0-windows`.
- **.NET 10 LTS as the stable contingency path:** `net10.0-windows` branch or fallback TFM if .NET 11 preview blocks progress.
- **Vortice.Windows packages:** Direct2D, DirectWrite, DXGI, WIC, and low-level Win32 bindings.
- **Small internal P/Invoke layer:** only where Vortice.Win32 is incomplete, awkward, or produces unacceptable ergonomics.
- **Direct2D + DirectWrite + WIC:** the first-class drawing model.
- **Win32 top-level overlay windows:** transparent, no-activate, optionally click-through, optionally topmost, optionally target-following.
- **DirectComposition/DXGI backend:** a post-parity backend candidate, not a first milestone blocker.
- **Standalone external overlay first:** optional cooperative/authorized integration later.

This is **not** a compatibility shim. The public API should intentionally break from GameOverlay.NET when doing so improves usability, safety, lifetime management, or future extensibility. Every important GameOverlay.NET capability should still have a modern equivalent.

---

## 2. Current technical baseline

### 2.1 Source project state

GameOverlay.NET provides a proven feature set:

- Transparent click-through windows.
- Sticky windows that follow a target HWND.
- Direct2D drawing primitives.
- Text rendering.
- Image loading.
- Render loop and FPS limiting.
- Window helper APIs.
- SharpDX-based native graphics interop.

The modernization must preserve the functional value, not the internal architecture.

### 2.2 SharpDX decision

SharpDX must be removed completely. It is no longer maintained and should be treated as an implementation dependency to eliminate, not as a foundation to incrementally extend.

Rules:

- No `SharpDX.*` package references.
- No `using SharpDX`.
- No public API names that imply SharpDX types.
- Add a build/test guard that fails on SharpDX package references or source imports.

### 2.3 .NET target decision

The product requirement is modern .NET. Use `net11.0-windows` as the primary target, with explicit preview-risk handling.

Policy:

1. The `main` branch targets `net11.0-windows`.
2. Phase 0 must prove that Vortice packages can build and run smoke tests on .NET 11 preview.
3. If .NET 11 preview blocks Vortice usage, CI, packaging, or reliable Windows interop, create a documented `net10.0-windows` contingency branch.
4. Do not multi-target v1 unless Phase 0 proves it is low-cost. Multi-targeting can expand test burden and complicate API design.
5. Add `.NET 11 preview` warnings to NuGet metadata and docs until .NET 11 reaches GA.

### 2.4 Vortice package decision

Use Vortice as the DirectX/Win32 binding layer. Pin package versions centrally in `Directory.Packages.props`.

Required packages at project start:

- `Vortice.Direct2D1`
- `Vortice.DirectWrite`
- `Vortice.DXGI`
- `Vortice.WIC`
- `Vortice.Mathematics`
- `Vortice.Win32`

Depending on package dependency closure, some packages may be pulled transitively. Keep direct package references for APIs the project intentionally uses so version upgrades remain explicit.

Phase 0 must produce a dependency report containing:

- exact package versions;
- supported TFMs;
- smoke-test status under `net11.0-windows`;
- smoke-test status under `net10.0-windows` if used as fallback;
- any known package-specific issues.

---

## 3. Product goals and non-goals

### 3.1 Goals

The library must:

- Create transparent Windows desktop overlay windows.
- Support click-through and interactive input modes.
- Avoid stealing focus by default.
- Support topmost overlays and target-following z-order.
- Follow target windows or client areas.
- Render high-quality 2D primitives, text, and images.
- Provide deterministic resource disposal.
- Recreate device-dependent resources after device loss, resize, HWND recreation, or backend recreation.
- Offer simple quick-start APIs for common cases.
- Offer low-level control for advanced Windows/graphics developers.
- Provide diagnostics suitable for debugging frame pacing, resource leaks, target tracking, and native failures.
- Keep core behavior standalone and safe.

### 3.2 Non-goals for v1

The v1 core library will not:

- Be a drop-in GameOverlay.NET replacement.
- Support non-Windows platforms.
- Render over exclusive fullscreen in a guaranteed way.
- Bypass anti-cheat, protected-process, capture-protection, DRM, or security boundaries.
- Require injection, hooks, or in-process target integration.
- Expose raw Vortice objects from the main public API.
- Implement a full retained-mode UI framework.
- Compete with WPF/WinUI for application UI composition.

---

## 4. Architecture principles

1. **Single owner thread for native rendering objects.** HWND, message pump, Direct2D render target/device context, DirectWrite objects, WIC realizations, and device-dependent resources are owned by the overlay thread unless explicitly documented otherwise.

2. **Value types for geometry and colors.** Public drawing geometry should be small readonly structs using floats in DIPs unless explicitly named as pixels.

3. **Device-independent descriptors, device-dependent realizations.** User resources survive recreation through descriptors. Native objects are disposable realizations tied to a backend generation.

4. **No hot-path surprise allocation.** Common render paths should avoid avoidable heap allocation after warm-up.

5. **Backends are internal by default.** The public API should be stable even if the rendering backend changes from HWND render target to DirectComposition.

6. **Explicit error surfaces.** Native failures should include operation name, HWND/backend state where safe, HRESULT, and a suggested next diagnostic step.

7. **Safe extensibility.** Advanced integration and plugin hooks must be opt-in, isolated, diagnosable, and unnecessary for normal overlay usage.

8. **DPI correctness by default.** Public coordinates are DIPs; window bounds APIs explicitly state whether they use physical pixels or DIPs.

---

## 5. Package and solution layout

Recommended solution:

```text
ModernOverlay.sln

src/
  ModernOverlay/
    Public facade, public drawing API, options, resource handles,
    render callback contracts, diagnostics facade.

  ModernOverlay.Win32/
    HWND creation, class registration, window procedure, message pump,
    target discovery, target tracking, DPI, window effects, z-order helpers.

  ModernOverlay.Direct2D/
    Vortice Direct2D/DirectWrite/WIC renderer implementation.

  ModernOverlay.Diagnostics/
    EventSource, logging adapters, debug inspectors, leak tracking,
    frame diagnostics and optional developer overlay helpers.

  ModernOverlay.Integration/
    Optional cooperative IPC and owned-app integration abstractions.
    No injection and no hooks.

  ModernOverlay.Integration.Experimental/
    Optional post-parity package for authorized experimental integration
    against project-owned sample hosts only.

samples/
  BasicOverlay/
  StickyWindowOverlay/
  InteractiveOverlay/
  ImageAndTextOverlay/
  GeometryOverlay/
  DiagnosticsOverlay/
  IpcOverlayDemo/
  SampleOwnedHost/

tests/
  ModernOverlay.Tests/
  ModernOverlay.Win32.Tests/
  ModernOverlay.Direct2D.Tests/
  ModernOverlay.Integration.Tests/

benchmarks/
  ModernOverlay.Benchmarks/
```

NuGet packaging recommendation:

- First public package: `ModernOverlay`.
- Optional package later: `ModernOverlay.Integration`.
- Experimental package later: `ModernOverlay.Integration.Experimental`.
- Do not force users to reference backend packages for the common scenario.

---

## 6. Public API design

### 6.1 Minimal overlay

```csharp
using ModernOverlay;
using ModernOverlay.Drawing;
using ModernOverlay.Windows;

await using var overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "Demo Overlay",
    Bounds = WindowBounds.FromPixels(100, 100, 800, 600),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    IsVisible = true,
    FrameRateLimit = FrameRateLimit.Fixed(144)
});

using var white = overlay.Resources.CreateSolidBrush(ColorRgba.White);
using var font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Text("Hello overlay", font, white, new PointF(24, 24));
    frame.Draw.Rectangle(white, new RectF(20, 60, 240, 120), strokeWidth: 2);
};

await overlay.RunAsync();
```

### 6.2 Sticky target overlay

```csharp
var target = WindowTarget.ByProcessName("notepad")
    .WithBoundsMode(TargetBoundsMode.ClientArea)
    .WithReacquire(true)
    .WithTrackingInterval(TimeSpan.FromMilliseconds(33));

await using var overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Target = target,
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.FollowTarget,
    FrameRateLimit = FrameRateLimit.Fixed(144)
});

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    // draw HUD content here
};

await overlay.RunAsync();
```

### 6.3 Interactive overlay

```csharp
overlay.InputMode = OverlayInputMode.Interactive;

overlay.PointerPressed += e =>
{
    // Optional interactive UI behavior.
};

overlay.Hotkeys.Register("ToggleClickThrough", KeyGesture.CtrlAltO, () =>
{
    overlay.InputMode = overlay.InputMode == OverlayInputMode.ClickThrough
        ? OverlayInputMode.Interactive
        : OverlayInputMode.ClickThrough;
});
```

### 6.4 Core public types

#### `OverlayWindow`

Primary user-facing overlay object.

Required members:

- `static ValueTask<OverlayWindow> CreateAsync(OverlayWindowOptions options, CancellationToken ct = default)`
- `ValueTask RunAsync(CancellationToken ct = default)`
- `ValueTask StopAsync(CancellationToken ct = default)`
- `ValueTask ShowAsync(CancellationToken ct = default)`
- `ValueTask HideAsync(CancellationToken ct = default)`
- `void Pause()`
- `void Resume()`
- `void SetBounds(WindowBounds bounds)`
- `void MovePixels(int x, int y)`
- `void ResizePixels(int width, int height)`
- `ValueTask RecreateAsync(CancellationToken ct = default)`
- `FrameStats FrameStats { get; }`
- `OverlayResourceManager Resources { get; }`
- `OverlayInputMode InputMode { get; set; }`
- `OverlayZOrder ZOrder { get; set; }`
- `WindowHandle Hwnd { get; }`
- `event OverlayRenderHandler Render`
- `event OverlayLifecycleHandler Loaded`
- `event OverlayLifecycleHandler Unloaded`
- `event OverlayDeviceHandler DeviceLost`
- `event OverlayDeviceHandler DeviceRestored`
- `event OverlayWindowChangedHandler BoundsChanged`
- `event OverlayWindowChangedHandler VisibilityChanged`
- `event OverlayTargetChangedHandler TargetChanged`

#### `OverlayWindowOptions`

Use init-only options for configuration that is not expected to mutate frequently.

Required properties:

```csharp
public sealed record OverlayWindowOptions
{
    public string? Title { get; init; }
    public WindowBounds Bounds { get; init; }
    public bool IsVisible { get; init; } = true;
    public OverlayInputMode InputMode { get; init; } = OverlayInputMode.ClickThrough;
    public OverlayZOrder ZOrder { get; init; } = OverlayZOrder.TopMost;
    public FrameRateLimit FrameRateLimit { get; init; } = FrameRateLimit.DisplayDefault;
    public PresentMode PresentMode { get; init; } = PresentMode.BackendDefault;
    public RenderQualityOptions Quality { get; init; } = RenderQualityOptions.Default;
    public OverlayTarget? Target { get; init; }
    public DpiMode DpiMode { get; init; } = DpiMode.PerMonitorV2;
    public TransparencyMode TransparencyMode { get; init; } = TransparencyMode.Auto;
    public WindowClassOptions WindowClass { get; init; } = WindowClassOptions.Randomized;
    public HiddenRenderPolicy HiddenRenderPolicy { get; init; } = HiddenRenderPolicy.Pause;
    public RenderExceptionPolicy ExceptionPolicy { get; init; } = RenderExceptionPolicy.StopOverlay;
    public bool EnableBlurBehind { get; init; }
}
```

#### `DrawContext`

Per-frame immediate-mode drawing API. It is valid only during the render callback.

Required members:

- `Clear(ColorRgba color)`
- `Draw.Line(...)`
- `Draw.Rectangle(...)`
- `Fill.Rectangle(...)`
- `Draw.RoundedRectangle(...)`
- `Fill.RoundedRectangle(...)`
- `Draw.Circle(...)`
- `Fill.Circle(...)`
- `Draw.Ellipse(...)`
- `Fill.Ellipse(...)`
- `Draw.Triangle(...)`
- `Fill.Triangle(...)`
- `Draw.Geometry(...)`
- `Fill.Geometry(...)`
- `Draw.Image(...)`
- `Draw.Text(...)`
- `Measure.Text(...)`
- `PushClip(RectF clip)`
- `PopClip()`
- `PushTransform(Matrix3x2F transform)`
- `PopTransform()`
- `ScopedClip Clip(RectF clip)`
- `ScopedTransform Transform(Matrix3x2F transform)`

Add scoped helpers so users can write:

```csharp
using (frame.Clip(new RectF(0, 0, 400, 400)))
using (frame.Transform(Matrix3x2F.CreateTranslation(10, 10)))
{
    frame.Draw.Text("Clipped", font, brush, new PointF(0, 0));
}
```

### 6.5 Resource handles

Public handles:

- `BrushHandle`
- `SolidBrushHandle`
- `LinearGradientBrushHandle`
- `FontHandle`
- `ImageHandle`
- `GeometryPath`
- `StrokeStyleHandle`
- `TextLayoutHandle` for advanced text reuse
- `LayerHandle` or `CommandListHandle` later, if Direct2D command lists become useful

Handle rules:

- All handles implement `IDisposable`.
- Handles validate use-after-dispose.
- Handles do not expose raw Vortice objects in the common public API.
- Advanced native access, if needed, belongs behind an explicitly marked diagnostics/interop package, not the core user path.
- Handles carry a backend generation ID internally.
- On device recreation, the resource manager realizes a fresh native object from the descriptor.

---

## 7. Windowing subsystem

### 7.1 Window style model

Create a borderless top-level popup window.

Base style:

- `WS_POPUP`

Base extended styles:

- `WS_EX_LAYERED`
- `WS_EX_NOACTIVATE`
- `WS_EX_TOOLWINDOW` by default, configurable
- `WS_EX_TRANSPARENT` when input mode is click-through
- `WS_EX_TOPMOST` when z-order is topmost

Use `SetWindowLongPtr` / `GetWindowLongPtr` for runtime style changes. Call `SetWindowPos` with `SWP_FRAMECHANGED` where required.

### 7.2 Input modes

`OverlayInputMode.ClickThrough`:

- Set `WS_EX_TRANSPARENT`.
- Keep `WS_EX_NOACTIVATE`.
- Mouse input passes to windows below.
- Do not register mouse capture.

`OverlayInputMode.Interactive`:

- Remove `WS_EX_TRANSPARENT`.
- Keep no-activate by default unless explicitly configured otherwise.
- Dispatch pointer/mouse messages through overlay events.

Future option:

- `OverlayInputMode.HitTest(Func<PointF, OverlayHitTestResult>)` can allow selective interaction without recreating the HWND.

### 7.3 Transparency modes

Transparency must be validated early because Windows composition behavior differs by backend.

Required enum:

```csharp
public enum TransparencyMode
{
    Auto,
    DwmGlassFrame,
    LayeredWindowAttributes,
    UpdateLayeredWindow,
    DirectComposition
}
```

Required validation spike:

1. Create a transparent overlay with the Direct2D HWND backend.
2. Validate clear-to-transparent on Windows 10 and Windows 11.
3. Validate click-through and no-activate behavior.
4. Validate resize and DPI changes.
5. Validate layered-window alpha and DWM frame extension.
6. Determine whether `UpdateLayeredWindow` fallback is needed for exact per-pixel alpha.
7. Document which path is the default and which paths are fallback/experimental.

Decision guidance:

- `DwmGlassFrame`/DWM frame extension may preserve GameOverlay.NET-style behavior on normal desktops.
- `LayeredWindowAttributes` is useful for global alpha/color key behavior but does not by itself guarantee a high-performance per-pixel alpha Direct2D path.
- `UpdateLayeredWindow` can provide reliable per-pixel alpha but may introduce CPU copies and should be fallback, not first choice, unless parity requires it.
- `DirectComposition` is the preferred long-term architecture if it passes parity.

### 7.4 DPI and coordinate policy

Use Per Monitor V2 DPI awareness.

Rules:

- Window bounds APIs have explicit pixel/DIP variants.
- Drawing coordinates are DIPs.
- Target tracking reads physical pixel bounds from Win32 and converts as needed.
- Multi-monitor negative coordinates are supported.
- DPI changes trigger bounds recalculation and render target resizing.
- WM_DPICHANGED is handled and logged.

Required types:

- `WindowBounds` for physical pixel bounds.
- `RectF` for drawing-space DIPs.
- `DpiScale` for conversion.

### 7.5 Message pump

Each overlay owns one dedicated native thread by default.

Loop requirements:

- Create HWND on the owner thread.
- Initialize COM explicitly on the owner thread.
- Create native render resources on the owner thread.
- Process pending messages without starving render scheduling.
- Use `MsgWaitForMultipleObjectsEx`, a waitable timer, or equivalent.
- Avoid `Thread.Sleep(1)` as the primary scheduling mechanism.
- Support cancellation and deterministic shutdown.

Pseudo-flow:

```text
Overlay thread starts
  Initialize COM
  Register window class
  Create HWND
  Create backend
  Raise Loaded
  while running:
    wait for messages, frame timer, or cancellation
    drain messages
    update target tracking if due
    render frame if visible, not paused, and due
  Raise Unloaded
  Dispose resources/backend
  Destroy HWND
  Unregister class
  Uninitialize COM
```

---

## 8. Rendering subsystem

### 8.1 Backend abstraction

Define `IRenderBackend` internally. Keep it small and lifecycle-oriented.

Required shape:

```csharp
internal interface IRenderBackend : IDisposable
{
    RenderBackendKind Kind { get; }
    RenderBackendGeneration Generation { get; }

    void Initialize(RenderBackendInitializeContext context);
    void Resize(PixelSize size, DpiScale dpi);
    BeginFrameResult BeginFrame(in FrameInfo frameInfo);
    EndFrameResult EndFrame();

    void Clear(ColorRgba color);
    IDrawCommandSink CommandSink { get; }
    IBackendResourceFactory Resources { get; }

    void SetQuality(RenderQualityOptions quality);
    void SetPresentMode(PresentMode presentMode);
}
```

### 8.2 Initial backend: Direct2D HWND backend

First parity backend:

- Vortice Direct2D.
- DirectWrite.
- WIC.
- HWND render target or equivalent Direct2D target.
- Premultiplied alpha where supported.
- `B8G8R8A8_UNorm` preferred.
- DPI-aware render target.
- Resize handling.
- Device loss/recreate handling.

Acceptance:

- Clear transparent works in the selected transparency mode.
- Translucent color clear works.
- Text, images, and shapes render correctly.
- Resize does not leak resources.
- HWND recreation triggers backend recreation.

### 8.3 Future backend: DirectComposition/DXGI

Post-parity backend candidate:

- Direct3D11 device.
- DXGI swap chain for composition.
- Direct2D device context.
- DirectComposition visual targeting overlay HWND.

The DirectComposition backend becomes default only if it proves:

- transparent composition;
- click-through/no-activate compatibility;
- reliable resizing;
- device-loss recovery;
- text/image/shape parity;
- equivalent or better frame pacing;
- no unacceptable complexity for users.

### 8.4 Command sink model

Even though the user-facing API is immediate mode, internally route draw calls through a command sink.

Benefits:

- Common path for immediate draw and future IPC commands.
- Easier diagnostics: draw counts, primitive counts, resource use.
- Potential command-list backend later.
- Safer integration with cooperative host/IPC scenarios.

Initial implementation may directly translate commands to Direct2D calls. Do not overbuild a retained scene graph in v1.

### 8.5 Color and alpha

Use explicit color semantics:

```csharp
public readonly record struct ColorRgba(float R, float G, float B, float A);
```

Rules:

- Public `ColorRgba` uses straight alpha.
- Backend converts to premultiplied alpha where required.
- Include helpers for byte-based colors.
- Avoid ambiguous `System.Drawing.Color` dependency in the core API.
- Document sRGB assumptions.

---

## 9. Resource management

### 9.1 Resource categories

Device-independent descriptors:

- brush color;
- gradient stops;
- font family/weight/style/size;
- image source path/bytes/stream copy;
- geometry path commands;
- stroke style descriptors;
- text layout options.

Device-dependent realizations:

- Direct2D brushes;
- Direct2D bitmap resources;
- DirectWrite text format/layout objects when tied to backend;
- geometry realizations;
- stroke styles;
- backend target resources.

### 9.2 Resource manager

`OverlayResourceManager` responsibilities:

- Create public resource handles.
- Store immutable descriptors.
- Lazily realize native resources on the overlay thread.
- Track backend generation.
- Dispose native realizations on device/backend loss.
- Recreate resources automatically when possible.
- Surface diagnostics for native resource counts and leaked handles.
- Reject resource creation from the render hot path if configured to do so.

Acceptance:

- Simple brushes/fonts/images survive resize and HWND recreation.
- Disposed resources throw meaningful exceptions.
- Resource creation failures include source information.
- Debug builds can list live resources and allocation sites when feasible.

### 9.3 Image handling

Supported sources:

- file path;
- byte array;
- stream;
- memory buffer.

Supported formats at minimum:

- PNG;
- JPEG/JPG;
- BMP.

Recommended later:

- WEBP if WIC codec is available on the system;
- ICO only if there is a compelling overlay use case.

Rules:

- Do not perform blocking file I/O in the render callback.
- Decode during setup or explicit async load.
- Preserve source bytes or a stable path for recreation.
- Report WIC codec failures clearly.

### 9.4 Text handling

Text API requirements:

- font family;
- size;
- weight;
- style;
- stretch where feasible;
- locale;
- alignment;
- wrapping;
- measuring;
- optional reusable text layout for high-frequency text.

Add guidance:

- `Draw.Text` is simple and may allocate internally for complex layout.
- `TextLayoutHandle` is recommended for repeated static/multi-line text.
- Provide diagnostics for excessive text layout creation per frame.

---

## 10. Drawing parity

The modern API must cover these GameOverlay.NET-equivalent capabilities.

| Capability | Modern API |
|---|---|
| Clear transparent/color | `frame.Clear(ColorRgba.Transparent)` |
| Lines | `frame.Draw.Line(...)` |
| Dashed lines | `StrokeStyleHandle` with dash style |
| Rectangles | `frame.Draw.Rectangle(...)`, `frame.Fill.Rectangle(...)` |
| Rounded rectangles | `frame.Draw.RoundedRectangle(...)`, `frame.Fill.RoundedRectangle(...)` |
| Circles | `frame.Draw.Circle(...)`, `frame.Fill.Circle(...)` |
| Ellipses | `frame.Draw.Ellipse(...)`, `frame.Fill.Ellipse(...)` |
| Triangles | `frame.Draw.Triangle(...)`, `frame.Fill.Triangle(...)` |
| Dashed shapes | shape overloads accepting `StrokeStyleHandle` |
| Arrows | `frame.Draw.Arrow(...)` helper |
| Box helpers | `frame.Draw.Box(...)` helper |
| Crosshair helpers | `frame.Draw.Crosshair(...)` helper |
| Text | `frame.Draw.Text(...)`, `frame.Measure.Text(...)` |
| Images | `frame.Draw.Image(...)` |
| Geometry/path | `GeometryPath` + `Draw.Geometry` / `Fill.Geometry` |
| Clip | `PushClip`/`PopClip` and scoped clip |
| Transform | `PushTransform`/`PopTransform` and scoped transform |
| Antialias options | `RenderQualityOptions` |

Frame safety:

- Draw calls outside a render callback throw `InvalidOperationException` with a clear message.
- Push/pop stacks are validated at frame end.
- Mismatched pops throw immediately.
- Frame end auto-resets stacks in fail-safe mode and logs diagnostics.

---

## 11. Render loop and scheduling

### 11.1 Frame rate modes

Required `FrameRateLimit` modes:

```csharp
public readonly record struct FrameRateLimit
{
    public static FrameRateLimit Unlimited { get; }
    public static FrameRateLimit DisplayDefault { get; }
    public static FrameRateLimit Fixed(double framesPerSecond);
}
```

Required `PresentMode` modes:

- `BackendDefault`
- `Immediate`
- `VSync`

Backend support varies; unsupported combinations should warn once and fall back predictably.

### 11.2 Frame info

`FrameInfo` should include:

- frame index;
- timestamp;
- elapsed time;
- delta time;
- target frame interval;
- actual frame interval;
- render-thread ID;
- DPI scale;
- window bounds;
- target bounds if attached.

### 11.3 Frame stats

`FrameStats` should expose:

- current FPS;
- average FPS;
- last frame time;
- moving-average frame time;
- worst frame time in window;
- skipped/dropped frames;
- draw call count;
- primitive count if available;
- resource realization count;
- target tracking updates;
- backend generation.

### 11.4 Exception policy

Render callback exceptions must not produce undefined behavior.

Policies:

- `StopOverlay`: stop rendering and surface exception.
- `PauseOverlay`: pause rendering and surface exception.
- `Continue`: log exception and continue next frame.
- `FailFast`: for debug/CI only.

Default: `StopOverlay`.

---

## 12. Target tracking and sticky overlays

### 12.1 Target discovery

Create `WindowTarget` factories:

- `FromHwnd(WindowHandle hwnd)`
- `ByProcessId(int processId)`
- `ByProcessName(string processName)`
- `ByTitle(string title, MatchMode mode = MatchMode.Contains)`
- `ByClassName(string className)`
- `Foreground()`
- custom provider through `IWindowTargetProvider`

### 12.2 Bounds modes

Required modes:

- `TargetBoundsMode.Window`
- `TargetBoundsMode.ClientArea`

Optional later:

- `TargetBoundsMode.Custom(Func<WindowHandle, WindowBounds?>)`

### 12.3 Tracking behavior

`WindowTracker` responsibilities:

- poll target bounds at configurable interval;
- detect move/resize/minimize/restore;
- hide or pause overlay when target is minimized, depending on policy;
- detect target loss;
- reacquire target when configured;
- update z-order when configured;
- avoid unnecessary `SetWindowPos` calls when bounds are unchanged.

Default tracking interval: 33 ms for GameOverlay.NET parity, but make it configurable.

### 12.4 Z-order

Modes:

- `Normal`
- `TopMost`
- `FollowTarget`

`FollowTarget` should use best-effort `SetWindowPos` placement above the target HWND. Document that Windows z-order can change due to activation, UAC prompts, secure desktops, shell behavior, virtual desktops, or other topmost windows.

---

## 13. Window helper modernization

Avoid a large static `WindowHelper` dumping ground. Use focused APIs.

### 13.1 `WindowQuery`

Required methods:

- `FindWindow`
- `FindChildWindow`
- `GetForegroundWindow`
- `GetActiveWindow`
- `GetDesktopWindow`
- `GetShellWindow`
- `GetOwnerWindow`
- `GetFirstChildWindow`
- `GetNextWindow`
- `GetPreviousWindow`
- `GetWindowProcessId`
- `GetWindowBounds`
- `GetClientBounds`
- `GetWindowStyles`
- `IsWindow`
- `IsVisible`

Return typed result objects where useful.

### 13.2 `WindowZOrder`

Required methods:

- `MakeTopmost`
- `RemoveTopmost`
- `PlaceAbove`

### 13.3 `WindowEffects`

Required methods:

- `ExtendFrameIntoClientArea`
- `EnableBlurBehind` where supported

Rules:

- Effects must fail gracefully on unsupported OS/composition states.
- Do not hide native failure information from diagnostics.

---

## 14. Integration architecture

### 14.1 Core integration modes

The core library supports external overlays:

- window discovery;
- target tracking;
- sticky bounds;
- target-following z-order;
- standalone render callbacks;
- command providers;
- IPC-friendly command model.

### 14.2 Cooperative integration package

`ModernOverlay.Integration` may provide:

- named-pipe transport;
- local socket transport;
- shared-memory state transfer later;
- command protocol;
- owned-app host SDK;
- sample owned host.

This package must not require injection or hooks.

### 14.3 Experimental integration package

`ModernOverlay.Integration.Experimental` may define interfaces for authorized deeper integration research:

- `IOverlayIntegrationProvider`
- `IRenderBridge`
- `IOverlayCommandTransport`
- `IWindowTargetProvider`

Rules:

- opt-in only;
- separate package;
- no stealth behavior;
- no anti-cheat bypass;
- no protected-process bypass;
- no kernel driver;
- no code targeting third-party processes in samples;
- validate against project-owned sample hosts.

If a target application blocks overlays, capture, hooks, injected modules, or external windows, the library should report that limitation rather than trying to defeat it.

---

## 15. Diagnostics and observability

### 15.1 Logging

Provide structured diagnostics through:

- `Microsoft.Extensions.Logging` adapter;
- optional `EventSource`/ETW provider;
- debug trace output for samples;
- diagnostics overlay sample.

### 15.2 Diagnostic events

Events to surface:

- HWND created/destroyed;
- backend initialized/disposed;
- device lost/restored;
- render exception;
- frame over budget;
- skipped frame;
- target lost/reacquired;
- z-order placement failed;
- DPI changed;
- resource leak detected;
- native call failure.

### 15.3 Developer diagnostics overlay

Provide a sample overlay that displays:

- FPS;
- frame time;
- render thread;
- draw call count;
- resource count;
- backend kind;
- HWND;
- target HWND;
- DPI;
- last native error/HRESULT.

---

## 16. Performance design

### 16.1 Mandatory practices

- Avoid hot-path allocations after warmup.
- Cache DirectWrite text formats.
- Reuse WIC/Direct2D image realizations.
- Reuse stroke styles.
- Avoid per-primitive locks.
- Do not decode images on the render thread during normal frames.
- Use dirty checks for target movement and z-order updates.
- Keep callback invocation on the render thread.
- Avoid exceptions for normal control flow.
- Prefer structs for frame info and common geometry.
- Use source-generated or preallocated event args where it helps without hurting usability.

### 16.2 Benchmark cases

BenchmarkDotNet cases:

- empty overlay frame;
- 1,000 lines;
- 10,000 lines;
- 1,000 rectangles;
- 1,000 circles/ellipses;
- 1,000 text draws;
- 1,000 image draws;
- resize stress;
- resource creation/disposal stress;
- target tracking overhead;
- IPC command ingestion overhead after integration package exists.

Acceptance:

- Benchmarks compile and run on Windows.
- Results are saved as artifacts.
- No arbitrary “must be X% faster” target blocks release.
- Severe regressions become bounded issues.

---

## 17. Testing strategy

### 17.1 Unit tests

Use for:

- option validation;
- geometry/value types;
- color conversion;
- frame scheduler;
- resource manager with null backend;
- command sink behavior;
- target query parsing;
- dependency guard.

### 17.2 Windows integration tests

Use for:

- HWND creation;
- style verification;
- no-activate behavior;
- click-through style toggling;
- show/hide/move/resize;
- DPI conversion;
- z-order calls;
- target tracking against a test window;
- Direct2D backend creation;
- resource creation/disposal;
- forced recreate.

Mark these tests with a Windows-only trait. Allow CI skip when no interactive desktop is available.

### 17.3 Visual validation

Where practical:

- render known primitive scenes;
- read back or screenshot expected areas;
- compare with tolerance;
- keep manual screenshots for release validation.

Do not rely exclusively on screenshot tests because composition, DPI, drivers, and color management can vary.

### 17.4 Manual checklist

Before release:

- transparent overlay appears;
- clear-to-transparent works;
- click-through works;
- interactive mode receives input;
- no focus steal on show;
- topmost works;
- target-follow z-order works best-effort;
- client-area tracking works;
- text renders crisply;
- images render;
- dashed shapes render;
- resize does not leak;
- recreate does not crash;
- device-loss simulation path works where possible;
- multi-monitor negative coordinates work;
- DPI changes are handled;
- target minimize/restore behavior works;
- troubleshooting docs match observed limitations.

---

## 18. Development phases

## Phase 0 — Repository bootstrap and dependency proof

### Goals

- Create the solution.
- Prove .NET 11 + Vortice viability.
- Establish fallback conditions.

### Tasks

- Create solution and project layout.
- Add central package management.
- Add nullable, analyzers, editorconfig, deterministic builds.
- Pin Vortice package versions.
- Add Windows CI.
- Install .NET 11 preview SDK in CI.
- Add optional .NET 10 LTS validation job.
- Create Direct2D, DirectWrite, WIC, DXGI, and Win32 smoke tests.
- Add SharpDX dependency/source guard.
- Add dependency report artifact.
- Add initial architecture decision records.

### Acceptance criteria

- `dotnet build` succeeds on Windows with .NET 11 SDK.
- Smoke tests pass on a Windows developer machine.
- CI runs on Windows.
- No SharpDX references exist.
- If .NET 11 fails, a `net10.0-windows` fallback issue records exact failure output and mitigation.

## Phase 1 — Window foundation and transparency spike

### Goals

- Implement HWND lifecycle.
- Validate transparency strategy before locking renderer assumptions.

### Tasks

- Register/unregister window class.
- Create popup/no-activate/layered overlay HWND.
- Implement randomized class/title options.
- Implement show/hide/move/resize/dispose.
- Implement click-through/interactive toggling.
- Implement topmost/normal z-order.
- Implement DWM frame extension and layered-window experiments.
- Implement transparency validation sample.
- Implement per-monitor DPI awareness.
- Implement message pump skeleton.

### Acceptance criteria

- Overlay appears borderless.
- Overlay does not activate.
- Click-through passes mouse interaction to windows beneath.
- Interactive mode receives input.
- Topmost works.
- Transparency strategy is documented with screenshots/notes.
- Cleanup destroys HWND and unregisters class.

## Phase 2 — Direct2D renderer foundation

### Goals

- Implement the parity renderer.

### Tasks

- Implement `IRenderBackend`.
- Implement Direct2D HWND backend.
- Create Direct2D, DirectWrite, WIC factories.
- Create render target/device context.
- Implement begin/end frame.
- Implement clear transparent/color.
- Implement resize.
- Implement present mode fallback behavior.
- Implement render quality options.
- Implement backend generation.

### Acceptance criteria

- Transparent clear works with selected window mode.
- Translucent color clear works.
- Resize updates render target.
- Backend disposes native resources.
- Recreate tears down and restores backend cleanly.

## Phase 3 — Resource system

### Goals

- Implement robust public handles and internal native realizations.

### Tasks

- Implement geometry/value structs.
- Implement resource manager.
- Implement solid brushes.
- Implement linear gradients.
- Implement fonts/text formats.
- Implement image loading from path, stream, byte array.
- Implement stroke styles.
- Implement geometry path builder.
- Implement debug leak tracking.
- Implement resource recreation.

### Acceptance criteria

- Resources draw correctly.
- Resources dispose deterministically.
- Simple resources survive backend recreation.
- Use-after-dispose errors are clear.
- Debug leak report identifies live resources.

## Phase 4 — Drawing API parity

### Goals

- Cover GameOverlay.NET drawing functionality.

### Tasks

- Implement lines, rectangles, rounded rectangles, circles, ellipses, triangles.
- Implement fill variants.
- Implement dashed variants.
- Implement text drawing/measuring.
- Implement image drawing/scaling/opacity.
- Implement geometry stroke/fill.
- Implement transforms and clipping.
- Implement arrows, crosshairs, boxes.
- Implement frame validation.

### Acceptance criteria

- Every primitive has a sample or test path.
- Dashed variants work.
- Text wrapping works.
- Image scaling works.
- Clip/transform stacks nest and unwind safely.
- Draw calls outside a frame fail clearly.

## Phase 5 — Render loop and lifecycle

### Goals

- Deliver a user-friendly overlay host.

### Tasks

- Implement run/stop loop.
- Implement waitable scheduling.
- Implement frame limiting.
- Implement frame stats.
- Implement pause/resume.
- Implement hidden render policy.
- Implement lifecycle events.
- Implement render exception policy.

### Acceptance criteria

- Render callback fires at configured cadence.
- Pause/resume works.
- Hidden overlays pause by default.
- Shutdown is deterministic.
- Exceptions are handled according to policy.
- Frame stats update correctly.

## Phase 6 — Target tracking and sticky parity

### Goals

- Replace `StickyWindow` with composable target tracking.

### Tasks

- Implement target providers.
- Implement client/window bounds tracking.
- Implement minimize/restore behavior.
- Implement target lost/reacquire behavior.
- Implement z-order follow.
- Add sticky sample.

### Acceptance criteria

- Overlay follows target move/resize.
- Client-area mode works.
- Whole-window mode works.
- Target loss/reacquire events fire.
- Follow-target z-order works best-effort and is documented.

## Phase 7 — Window helper modernization

### Goals

- Preserve helper coverage without one large static helper.

### Tasks

- Implement `WindowQuery`.
- Implement `WindowZOrder`.
- Implement `WindowEffects`.
- Add typed return results.
- Add failure diagnostics.

### Acceptance criteria

- Original helper functionality has modern equivalents.
- Unsupported effects fail gracefully.
- Docs map old helper methods to new APIs.

## Phase 8 — Samples and documentation

### Required samples

1. `BasicOverlay`
2. `StickyWindowOverlay`
3. `InteractiveOverlay`
4. `ImageAndTextOverlay`
5. `GeometryOverlay`
6. `DiagnosticsOverlay`
7. `IpcOverlayDemo` after integration package exists
8. `SampleOwnedHost` for cooperative/experimental integration validation

### Required docs

- quick start;
- installation;
- .NET 11 preview warning;
- .NET 10 fallback note;
- GameOverlay.NET concept mapping;
- window modes;
- transparency modes;
- target tracking;
- DPI and multi-monitor behavior;
- drawing primitives;
- resource lifetime;
- device loss/recreation;
- performance guide;
- troubleshooting;
- integration boundary and safety statement.

### Acceptance criteria

- Samples build.
- Basic and sticky samples run manually.
- Docs state this is not a drop-in replacement.
- Docs state exclusive fullscreen and protected/restricted targets are not guaranteed.

## Phase 9 — Benchmarks and performance review

### Goals

- Provide bounded performance evidence.

### Tasks

- Add BenchmarkDotNet project.
- Add benchmark cases.
- Add optional comparison against GameOverlay.NET where practical.
- Add artifact output.
- Add regression issue template.

### Acceptance criteria

- Benchmarks run on Windows.
- Results are saved.
- Severe findings become bounded issues.

## Phase 10 — Optional post-parity features

### 10.1 DirectComposition backend spike

Add only after parity.

Acceptance:

- transparency works;
- click-through works;
- resize works;
- device loss works;
- all drawing primitives work;
- benchmark comparison exists;
- decision documented.

### 10.2 Cooperative integration package

Acceptance:

- owned app can send draw commands to overlay;
- named-pipe transport works;
- start/stop/update/clear commands exist;
- failures are isolated;
- docs explain standalone, cooperative, IPC, and experimental modes.

### 10.3 Plugin providers

Acceptance:

- `IWindowTargetProvider`;
- `IOverlayCommandTransport`;
- `IOverlayIntegrationProvider`;
- plugin failure isolation;
- no implicit access to internal native resources.

---

## 19. Concrete backlog

### Foundation

#### `P0-001` Create solution skeleton
Create projects, central package management, analyzers, nullable settings, deterministic builds, and Windows CI.

#### `P0-002` Add dependency guard
Fail build/test if SharpDX package references or `using SharpDX` appear.

#### `P0-003` Verify Vortice under .NET 11
Smoke-test Direct2D, DirectWrite, WIC, DXGI, and Win32 under `net11.0-windows`.

#### `P0-004` Add fallback decision record
Document exactly when the project may move implementation work to `net10.0-windows`.

#### `P0-005` Add native error helper
Create internal helpers for HRESULT, Win32 last-error, and operation-context diagnostics.

### Windowing

#### `WIN-001` Register/unregister window class
Implement class lifecycle with duplicate-name handling.

#### `WIN-002` Create transparent no-activate overlay HWND
Create borderless popup with correct extended styles.

#### `WIN-003` Validate transparency modes
Build a sample and document which transparency path is reliable.

#### `WIN-004` Implement lifecycle operations
Show, hide, move, resize, set bounds, recreate, dispose.

#### `WIN-005` Implement input mode toggling
Switch click-through/interactive at runtime without recreation where possible.

#### `WIN-006` Implement DPI handling
Per-monitor awareness, WM_DPICHANGED, pixel/DIP conversion.

### Rendering

#### `REN-001` Implement backend interface
Allow Direct2D and null test backends.

#### `REN-002` Implement Direct2D HWND backend
Initialize, begin/end frame, clear, resize, dispose.

#### `REN-003` Implement render quality options
Primitive and text antialiasing.

#### `REN-004` Implement backend generation
Invalidate/recreate resources safely.

#### `REN-005` Implement frame command sink
Route draw calls through a diagnosable command sink.

### Resources

#### `RES-001` Implement resource manager
Descriptors, realizations, generation tracking, disposal.

#### `RES-002` Implement brushes
Solid and linear gradient brushes.

#### `RES-003` Implement fonts/text formats
Font options, measurement, wrapping.

#### `RES-004` Implement images
Path, stream, byte array, PNG/JPEG/BMP.

#### `RES-005` Implement geometry paths
Move/line/bezier/arc where feasible/close.

#### `RES-006` Implement stroke styles
Solid, dashed, custom dashes.

#### `RES-007` Implement leak diagnostics
Debug resource registry and optional allocation stack capture.

### Drawing

#### `DRAW-001` Implement primitive draw/fill
Lines, rectangles, rounded rectangles, circles, ellipses, triangles.

#### `DRAW-002` Implement dashed primitives
Shared stroke-style path.

#### `DRAW-003` Implement text and image drawing
Text draw/measure, image draw/scaling/opacity.

#### `DRAW-004` Implement geometry drawing
Stroke/fill path geometries.

#### `DRAW-005` Implement transform and clip stacks
Push/pop and scoped helpers.

#### `DRAW-006` Implement helpers
Arrows, crosshairs, boxes.

### Loop and lifecycle

#### `LOOP-001` Implement run loop
Message pump + scheduler.

#### `LOOP-002` Implement frame limiting
Unlimited, fixed, display default.

#### `LOOP-003` Implement frame stats
FPS, timings, draw counts.

#### `LOOP-004` Implement pause/resume/hidden behavior
Pause by default when hidden.

#### `LOOP-005` Implement exception policy
Stop, pause, continue, fail-fast.

### Targeting

#### `TARGET-001` Implement discovery providers
HWND, PID, process name, title, class, foreground.

#### `TARGET-002` Implement sticky tracking
Bounds polling and synchronization.

#### `TARGET-003` Implement target loss/reacquire
Events and policy.

#### `TARGET-004` Implement follow-target z-order
Best-effort placement above target.

### Helpers

#### `HELP-001` Implement WindowQuery
Focused query API.

#### `HELP-002` Implement WindowZOrder
Topmost/remove/place-above.

#### `HELP-003` Implement WindowEffects
DWM frame and blur support.

### Diagnostics

#### `DIAG-001` Add logging adapter
`Microsoft.Extensions.Logging` integration.

#### `DIAG-002` Add EventSource
Frame/backend/resource events.

#### `DIAG-003` Add diagnostics sample
Runtime overlay displaying key stats.

### Docs and samples

#### `DOC-001` Write quick start
Minimal copy/paste overlay.

#### `DOC-002` Write migration guide
Map GameOverlay.NET concepts to ModernOverlay.

#### `DOC-003` Write troubleshooting guide
Fullscreen, DPI, multi-monitor, click-through, target lost.

#### `SAMPLE-001` Basic overlay
Text, line, rectangle.

#### `SAMPLE-002` Sticky overlay
Follows target client area.

#### `SAMPLE-003` Interactive overlay
Input mode toggle.

#### `SAMPLE-004` Image/text overlay
Image loading and font rendering.

#### `SAMPLE-005` Geometry overlay
Paths, clips, transforms.

#### `SAMPLE-006` Diagnostics overlay
Frame/resource stats.

### Benchmarks

#### `BENCH-001` Add benchmark project
BenchmarkDotNet setup.

#### `BENCH-002` Add draw benchmarks
Lines, rectangles, text, images.

#### `BENCH-003` Add lifecycle benchmarks
Resize, recreate, resource churn.

---

## 20. Risk register

| Risk | Impact | Mitigation |
|---|---:|---|
| .NET 11 preview instability | High | Phase 0 smoke tests; `net10.0-windows` fallback decision record. |
| Vortice package mismatch with .NET 11 | Medium | Pin versions; smoke-test factories and HWND renderer; keep .NET 10 contingency. |
| Transparent layered HWND behavior differs by Windows version/GPU | High | Required transparency spike; document selected mode and fallbacks. |
| Direct2D HWND render target is not the best long-term backend | Medium | Use it for parity; isolate behind backend abstraction; add DirectComposition spike after parity. |
| COM/native resource leaks | High | Central resource manager, generation tracking, debug leak diagnostics, recreate stress tests. |
| DPI and multi-monitor bugs | Medium | Per Monitor V2, explicit pixel/DIP APIs, manual multi-monitor checklist. |
| Z-order follow cannot be guaranteed | Medium | Document best-effort behavior; provide topmost fallback. |
| Render callback exceptions destabilize owner thread | Medium | Configurable exception policy and clear diagnostics. |
| Excessive API abstraction | Medium | Keep common path simple; advanced APIs in secondary namespaces/packages. |
| Integration features create unsafe expectations | High | Separate optional packages; owned/authorized use only; no stealth or bypass behavior. |
| CI lacks interactive desktop | Medium | Mark integration tests; skip with clear reason; require manual release checklist. |

---

## 21. Release criteria

### Alpha 1

- Repository and CI exist.
- Dependency proof complete.
- Window foundation complete.
- Transparency strategy selected.
- Direct2D backend clears transparent/color frames.
- Basic overlay sample runs.
- No SharpDX references.

### Alpha 2

- Render loop implemented.
- Solid brushes implemented.
- Major primitives implemented.
- Text rendering implemented.
- Frame stats available.

### Alpha 3

- Images implemented.
- Gradients implemented.
- Geometry implemented.
- Clip/transform stacks implemented.
- Resource recreation tested.

### Beta 1

- Sticky target tracking complete.
- Window helper equivalents complete.
- Samples complete.
- Documentation draft complete.

### Beta 2

- Benchmarks complete.
- Device/resource recreation stress tested.
- Diagnostics overlay complete.
- Manual validation checklist completed.

### 1.0

- Functional parity matrix satisfied.
- SharpDX absent.
- Vortice is the DirectX binding layer.
- Public API reviewed for usability.
- Known limitations documented.
- Samples available.
- Benchmarks available.
- Integration features isolated and opt-in.
- No stealth, anti-cheat bypass, protected-process bypass, or kernel-level integration.

---

## 22. Definition of done

The modernization is complete when:

- The project targets `net11.0-windows`, or a documented fallback branch targets `net10.0-windows` due to proven .NET 11/Vortice blockers.
- The project builds and tests on Windows.
- SharpDX is entirely absent.
- Vortice is the DirectX/Win32 binding layer.
- Every GameOverlay.NET capability in the parity matrix has a modern equivalent.
- The public API is documented as intentionally breaking and not drop-in compatible.
- Transparent, click-through, no-activate overlays work.
- Sticky window behavior works.
- Resource/device recreation works.
- At least six samples demonstrate core usage.
- Benchmarks exist and can be run.
- Diagnostics exist for frame timing, resources, target tracking, and native failures.
- Optional integration features are isolated, opt-in, and documented for owned/authorized applications only.
- Stealth, anti-cheat bypass, protected-process bypass, and kernel-level integration are not implemented.

---

## 23. Source references checked during this revision

- GameOverlay.NET repository: https://github.com/michel-pi/GameOverlay.Net
- GameOverlay.NET NuGet package: https://www.nuget.org/packages/GameOverlay.Net
- SharpDX repository: https://github.com/sharpdx/SharpDX
- Vortice.Windows repository: https://github.com/amerkoleci/Vortice.Windows
- Vortice.Win32 repository: https://github.com/amerkoleci/Vortice.Win32
- Vortice.Direct2D1 NuGet package: https://www.nuget.org/packages/Vortice.Direct2D1
- Vortice.Win32 NuGet package: https://www.nuget.org/packages/Vortice.Win32
- .NET 11 download/status page: https://dotnet.microsoft.com/en-us/download/dotnet/11.0
- .NET 11 overview: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- Microsoft Direct2D overview: https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-overview
- Microsoft DirectWrite overview: https://learn.microsoft.com/en-us/windows/win32/directwrite/direct-write-portal
- Microsoft WIC documentation: https://learn.microsoft.com/en-us/windows/win32/wic/-wic-lh
- Microsoft layered windows documentation: https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
- Microsoft DirectComposition overview: https://learn.microsoft.com/en-us/windows/win32/directcomp/directcomposition-portal
