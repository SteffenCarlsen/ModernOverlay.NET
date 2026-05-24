# ModernOverlay 1.1 Interactive UI Analysis

Date: 2026-05-23

This document initializes the version 1.1 feature analysis for a retained, interactive UI layer in ModernOverlay. The source inspiration is the local proof of concept at `C:\Users\TaF\Documents\phitp_cs2\phitp_cs2\GameOverlay`, but the feature target is a generic overlay UI framework. Game-specific behavior, input synthesis, stealth behavior, memory access, anti-cheat bypasses, and app-specific forms are out of scope.

## Goal

ModernOverlay 1.1 should let users build interactive overlay UI without manually wiring every pointer event and draw call. The library should support both:

1. Manually placed UI, where users position panels and controls directly.
2. Dynamically placed UI, where panels can anchor, follow overlay or target bounds, remember user placement, and be dragged or resized at runtime.

The intended feature is a small retained UI layer over the existing immediate-mode drawing and window/input primitives. It should not become a full WPF or WinUI replacement.

## Architecture Decisions

The following decisions define the 1.1 MVP scope:

1. Ship the UI layer as a separate `ModernOverlay.UI` project/package.
2. Support selective click-through, so non-UI overlay regions can remain click-through while UI elements receive input.
3. Include keyboard and text input in the MVP; `TextBox` is not deferred.
4. Treat the listed control set as mandatory for the MVP rather than optional follow-up work.
5. Include `ComboBox` in the MVP, which makes popup infrastructure part of the first implementation.
6. Expose layout persistence through `IUiLayoutStore`, not a built-in JSON or file storage dependency.
7. Ship a built-in theme with customization hooks.
8. Include close and minimize behavior in `UiWindow`.
9. Include layout and higher-level controls such as `Grid`, tabs, segmented controls, and color picker in the minimal 1.1 release.
10. Let `ModernOverlay.UI` reference `ModernOverlay.Diagnostics` for consistency with the rest of the repo.
11. Make `OverlayUiRoot` disposable, manually disposable for early cleanup, and automatically detached/disposed when the owning overlay is disposed.
12. Require explicit `ui.Render(frame)` calls so users control UI render order and future multi-root scenarios remain possible.
13. Require explicit selective click-through opt-in through overlay/window options; `OverlayUiRoot` should provide input-region resolution but not silently change input behavior.
14. Use `ui.Root.Children.Add(...)` as the collection-shaped API, with `ui.Root.Add(...)` as a convenience that returns the added element.
15. Add `MinimizeBehavior` and implement title-bar collapse, hidden-until-restored, and dock/tray minimized window modes.
16. Ship both `TabControl` and `SegmentedControl` in the MVP.
17. Include `Menu`, `ContextMenu`, `GroupBox`, and `ToolTip` in the MVP; defer `SearchBox`.
18. Focused text input controls should own keyboard/text message input while editing.
19. Defer `ScrollViewer` and general virtualization until after the first UI MVP.
20. Add a lightweight property-change/invalidation model, deferred mutation policy, popup/focus/capture rules, text input scope, resource lifetime semantics, and command-state rules before implementing controls.
21. Keep UI Automation provider support out of scope for 1.1 while still requiring keyboard navigation, visible focus, disabled states, and documented accessibility limitations.

These decisions push 1.1 from a narrow control spike into a complete retained overlay UI layer. The implementation should still be sliced vertically, but the acceptance bar is the full MVP surface.

## Existing ModernOverlay Baseline

The current library already has the right foundation for this feature:

- `OverlayInputMode.Interactive` toggles the overlay out of click-through mode.
- `OverlayWindow` exposes pointer moved, pressed, released, and wheel events.
- `DrawContext` already supports the primitives needed to render controls.
- `OverlayResourceManager` provides reusable brushes, fonts, images, and geometry handles.
- Target tracking and explicit pixel/DIP conversion are already present.
- Samples already demonstrate interactive input mode, hotkeys, diagnostics, and drawing.

The main missing pieces are not overlay-window pieces. They are UI-framework pieces: element tree, layout, input-region resolution, routed input, focus, pointer capture, keyboard/text input, control state, and placement persistence.

## Proof Of Concept Findings

The local PoC has useful framework concepts, but it should be mined for behavior rather than copied as implementation.

| PoC area | Useful concept | ModernOverlay direction |
|---|---|---|
| `UIControl` | Shared bounds, min size, margin, background/border draw, update/draw split | Convert into a public `UiElement`/`UiControl` model with DIPs, invalidation, style, and frame-safe rendering. |
| `UIContainer` | Vertical stacking and parent-driven child positioning | Generalize as `StackPanel` and layout pass. Avoid direct mutation of child positions during draw. |
| `UIContainerInline` | Horizontal stacking | Generalize as horizontal `StackPanel` or `WrapPanel` later. |
| `UIForm` | Draggable/resizable panel, active layer, focus, header | Promote as `UiWindow` or `UiPanelWindow` with z-index, drag handles, constraints, and placement persistence. |
| Controls | Button, checkbox, slider, combo box, labels, list, progress, textbox | Implement a first focused control set; keep complex controls incremental. |
| Open combo drawing last | Popup layering | Model popups and overlays explicitly through z-order/layer hosts. |
| Global input polling | Simple key/button state machine | Replace with event-driven `OverlayWindow` input routing. Do not import input synthesis or global polling. |
| Form initialization | Declarative composition of panels and controls | Provide builder-friendly APIs, but keep app-specific forms outside the library. |

The PoC uses global state and app-specific configuration for scaling, mouse state, and form ownership. ModernOverlay should instead rely on per-overlay UI roots, explicit DPI/DIP units, and local focus/capture state.

## Safety And Scope Boundary

In scope:

- Generic controls and panels for owned overlays.
- Draggable, resizable, anchored, and persisted UI panels.
- Event-driven mouse/pointer, wheel, keyboard, and text input.
- Styling, themes, and resource reuse.
- Samples using neutral demo content.

Out of scope:
- Synthesizing user input with `SendInput`.

## Proposed Public Shape

The UI layer should be opt-in and likely live in a separate `ModernOverlay.UI` project/package so the 1.0 drawing/window surface remains small.

Example target API:

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Bounds = WindowBounds.FromPixels(100, 100, 900, 520),
    InputMode = OverlayInputMode.SelectiveClickThrough,
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions
{
    RegisterInputRegions = true,
});

UiWindow panel = ui.Root.Children.Add(new UiWindow
{
    Title = "Overlay Controls",
    Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(16)),
    MinWidth = 260,
    MinHeight = 180,
    CanDrag = true,
    CanResize = true,
});

panel.Content = new StackPanel
{
    Spacing = 8,
    Children =
    {
        new TextBlock { Text = "Demo controls" },
        new Button { Text = "Apply", Command = UiCommand.FromAction(() => Apply()) },
        new Slider { Minimum = 0, Maximum = 100, Value = 50 },
        new CheckBox { Text = "Enable overlay option" },
    },
};

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    ui.Render(frame);
};

await overlay.RunAsync();
```

Names are provisional. The important shape is that UI attachment owns input dispatch and control state while rendering still flows through the existing `DrawContext`. Public API language should use selective click-through and input regions; Win32 hit testing remains an implementation detail.

## Architecture Proposal

### Project boundary

Add a new project:

```text
src/ModernOverlay.UI/
tests/ModernOverlay.Tests/...
samples/InteractiveUiOverlay/
```

Package and namespace the UI layer consistently as `ModernOverlay.UI`. A separate package is safer for 1.1 because the UI layer will evolve faster than the core overlay primitives.

### Core objects

- `OverlayUiRoot`: attaches to an `OverlayWindow`, subscribes to input events, owns root elements, layout, focus, capture, and render order.
- `UiElement`: base retained element with `Bounds`, `DesiredSize`, `Margin`, `Padding`, `IsVisible`, `IsEnabled`, `ZIndex`, `Parent`, and invalidation.
- `UiControl`: interactive element base with hover, pressed, focused, captured, and command/event hooks.
- `UiPanel`: container base for children.
- `StackPanel`: vertical/horizontal sequential layout.
- `Canvas`: absolute/manual positioning.
- `UiWindow`: draggable/resizable framed panel with title/header, z-order, constraints, and placement policy.
- `UiPlacement`: manual, anchored, target-relative, overlay-relative, and persisted placement descriptors.
- `UiTheme`: brush/font/resource descriptors realized through the overlay resource manager.
- `IUiLayoutStore`: interface-based placement persistence without a built-in JSON or file storage dependency.

### Input model

The UI root should translate overlay input events into routed UI events:

- Pointer enter/leave/move.
- Pointer pressed/released/click.
- Pointer wheel.
- Pointer capture for sliders, drag handles, and resize handles.
- Focus and blur.
- Keyboard pressed/released.
- Text input.

Current ModernOverlay pointer events are enough for basic mouse controls. Keyboard and text entry need a core `OverlayWindow` expansion so Win32 `WM_KEYDOWN`, `WM_KEYUP`, and `WM_CHAR` can reach the UI root without global polling.

### Rendering model

The retained UI layer should render using `DrawContext`; it should not expose backend-native Direct2D objects. UI resources should be created outside the render callback and reused through existing `OverlayResourceManager` handles.

Rendering order should be deterministic:

1. Normal content controls.
2. Floating windows by `ZIndex`.
3. Popups and dropdowns.
4. Drag/resize adorners or focus visuals.

Controls should clip to their layout bounds by default unless they explicitly open a popup layer.

## Dynamic Placement Model

ModernOverlay 1.1 should treat placement as first-class data rather than ad hoc mutation.

Proposed placement kinds:

- `Manual`: explicit DIP position and size in overlay coordinates.
- `Anchor`: attach to overlay edge/corner with margin.
- `TargetAnchor`: attach to the currently tracked target bounds.
- `Cursor`: temporary placement near the pointer for context menus/tooltips.
- `Persisted`: restore a saved manual placement, falling back to another placement if invalid.

The UI root should clamp panels into the visible overlay bounds unless the user opts out. Placement should be recomputed when overlay bounds, DPI, or target bounds change, but user-dragged panels should become manual/persisted placement unless configured otherwise.

Implementation note: target bounds are screen-pixel data from the overlay target tracker and must be converted to overlay-local DIPs during layout. The UI root should sample that current overlay state from render/input layout instead of mutating the UI tree from target-tracking events, because target tracking can run outside the UI root's thread-affine path. Cursor placement should use the last overlay-local pointer position; `Thickness` acts as a signed offset where left/top add and right/bottom subtract.

## Initial Control Set

Required 1.1 MVP controls and layout primitives:

- `TextBlock` / `Label`
- `Image`
- `Button`
- `ToggleButton`
- `CheckBox`
- `RadioButton`
- `Slider`
- `NumberBox`
- `ProgressBar`
- `StackPanel`
- `Canvas`
- `DockPanel`
- `Grid`
- `WrapPanel`
- `UiWindow` / draggable panel
- `TextBox`
- `ListBox`
- `ComboBox`
- `TabControl`
- `SegmentedControl`
- `ColorPicker`
- `Menu`
- `ContextMenu`
- `GroupBox`
- `ToolTip`

Popup infrastructure, keyboard/text input, focus handling, selective click-through, and theming are therefore MVP dependencies rather than follow-up polish.

`ScrollViewer` is intentionally not part of the 1.1 MVP. It should return only after transformed coordinate handling, clipping, wheel routing, and popup placement inside scrolled content have a dedicated design.

## Implementation Milestones

### Spike 1 - UI root, layout, and render

- Add the `ModernOverlay.UI` project.
- Implement `UiElement`, `UiPanel`, `StackPanel`, `Canvas`, `Thickness`, `UiSize`, and `OverlayUiRoot`.
- Render simple panels/text/buttons through existing `DrawContext`.
- Add unit tests for measure/arrange and render ordering.

### Spike 2 - UI foundation policy

- Implement property-change metadata and invalidation helpers.
- Implement deferred mutation queues and protected-phase rules.
- Define UI tree thread affinity and any dispatcher/deferred-operation entry point.
- Implement popup ownership, focus, dismissal, and capture policy.
- Define text editing scope for IME, Unicode, dead keys, clipboard, and caret timing.
- Define runtime theme swap, resource re-realization, and command-state refresh semantics.

### Spike 3 - Pointer routing and controls

- Route pointer move/press/release/wheel through input-region resolution.
- Add hover, pressed, click, and captured states.
- Implement button, checkbox, slider, progress, and basic window panel dragging/resizing.
- Implement selective click-through input regions for UI elements that can and should receive input.
- Add unit tests for input regions, event routing, capture, and z-order.

### Spike 4 - Placement and persistence

- Implement manual and anchored placement.
- Add target-relative placement using `FrameInfo.TargetBounds` or overlay target events.
- Add `IUiLayoutStore` for persisted panel positions.
- Add sample demonstrating user-driven repositioning.

### Spike 5 - Keyboard and text input

- Extend `OverlayWindow` and `Win32OverlayWindow` with key and character callbacks.
- Add focus manager and keyboard routing.
- Implement `TextBox`, keyboard navigation, and text entry in the MVP. When a text input is focused and editing, it should own keyboard/text message input for that editing session.

### Spike 6 - Complete MVP controls

- Implement popup infrastructure, `ListBox`, and `ComboBox`.
- Implement `Grid`, `WrapPanel`, and `DockPanel`.
- Implement `NumberBox`, tabs or segmented controls, and `ColorPicker`.
- Implement `UiWindow` close and minimize behavior.

### Spike 7 - Package, docs, and samples

- Add a quick-start `InteractiveUiOverlay` sample.
- Add a stress/integration UI sample.
- Document safety boundary, input modes, dynamic placement, accessibility scope, and known limitations.
- Publish the UI layer as a separate package.

## Resolved Architecture Decisions

There are no remaining architecture questions from the initial review. The selective click-through mode should be exposed as `OverlayInputMode.SelectiveClickThrough`.

## Validation Plan

- Unit tests for layout, invalidation, input-region resolution, event routing, capture, focus, and placement clamping.
- Core Win32 tests for keyboard/text callback translation once added.
- Sample compile tests for the target public API.
- Manual interactive sample validation for click, drag, resize, slider capture, checkbox, combo/list popup, text input, theme changes, DPI movement, persistence restore, and target-bound placement.
- Release validation should run the existing command gate plus UI-specific unit/sample tests.

## Immediate Next Step

Start with foundation work for `ModernOverlay.UI`: root, element tree, layout engine, selective click-through input-region contract, focus/capture model, built-in theme, and render ordering. Then implement the first vertical slice with `Canvas`, `StackPanel`, `TextBlock`, `Button`, `CheckBox`, `Slider`, `ProgressBar`, and `UiWindow`, while keeping the architecture ready for keyboard/text input, popups, and the full control set required by the MVP.
