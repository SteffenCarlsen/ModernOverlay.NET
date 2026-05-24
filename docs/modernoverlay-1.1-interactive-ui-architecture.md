# ModernOverlay 1.1 Interactive UI Architecture Notes

Date: 2026-05-23

This note records the implementation architecture after the initial task-breakdown review. The 1.1 goal is a complete retained overlay UI MVP, not just a small clickable-control spike.

## Accepted Direction

- Ship `ModernOverlay.UI` as a separate project/package, using `ModernOverlay.UI` consistently for internal and external naming.
- Add selective click-through so only UI regions intercept pointer input.
- Include keyboard/text input and `TextBox` in the MVP.
- Include the complete control and layout set tracked in the task breakdown.
- Include `ComboBox`, which requires popup infrastructure in the first release.
- Use an interface-based layout persistence model.
- Ship a built-in theme with customization hooks.
- Include drag, resize, close, minimize, focus, and z-order behavior for `UiWindow`.
- Include `Grid`, tabs, segmented controls, and `ColorPicker` in the minimal 1.1 release.
- Defer `ScrollViewer` and virtualization until after the first UI MVP because scrolling introduces transformed input regions, clipped coordinate spaces, popup placement complications, and wheel-routing policy.
- Reference `ModernOverlay.Diagnostics` from `ModernOverlay.UI` for diagnostic consistency.
- Make `OverlayUiRoot` `IDisposable`, allow manual disposal, and also detach/dispose it when the owning overlay is disposed.
- Require explicit `ui.Render(frame)` calls instead of automatic render subscription.
- Require explicit overlay/window opt-in for selective click-through; `OverlayUiRoot` provides input-region resolution but does not silently mutate input behavior.
- Use `ui.Root.Children.Add(...)` as the core content API, with `ui.Root.Add(...)` as a convenience helper.
- Add `MinimizeBehavior` and implement title-bar collapse, hidden-until-restored, and dock/tray minimized window modes.
- Ship both `TabControl` and `SegmentedControl`.
- Include `Menu`, `ContextMenu`, `GroupBox`, and `ToolTip` in the MVP; defer `SearchBox`.
- Focused text input controls should own keyboard/text message input while editing.

## External Review Alignment

The follow-up architecture review is accepted with one scope adjustment: `ScrollViewer` is removed from the 1.1 MVP instead of being defined in deeper detail now. The reasoning is:

- property invalidation is foundational. A retained UI without consistent layout/render/input/resource invalidation would spread fragile setter-specific behavior across every control.
- deferred mutation is required for ordinary UI behavior, not polish. Click handlers, popup close operations, capture release, and focus changes all need deterministic ordering while events or layout passes are in progress.
- popup, focus, and capture rules should be explicit because `ComboBox`, menus, context menus, and tooltips all cross the normal element tree boundary.
- text input must state its international-input limits honestly. Basic `WM_CHAR` text entry is not the same as complete IME, clipboard, Unicode text-element, and locale-sensitive editing support.
- theme/resource mutation must integrate with the existing resource-lifetime and device-recreation model so UI controls do not create native resources during render or leak resources after theme/backend changes.
- command support needs `CanExecuteChanged` and parameter semantics even without full data binding, otherwise button enabled state and disabled visuals become inconsistent.
- the sample strategy should split teaching from stress validation. A tiny quick-start sample is easier to learn from; a separate stress sample is better for exercising popups, text input, DPI movement, theme changes, and persistence.

## Architectural Consequences

### Package boundary

`ModernOverlay.UI` should reference the core `ModernOverlay` package and `ModernOverlay.Diagnostics`, and render through `DrawContext`. Core should not depend on UI. Any integration needed for selective click-through should be expressed as a core input-region callback or input-mode extension so the dependency direction stays one-way.

### Selective click-through

Selective click-through is the largest core-windowing change. The likely implementation path is:

1. Add an explicit `OverlayInputMode.SelectiveClickThrough` mode or equivalent option to `OverlayWindowOptions`.
2. Add an input-region resolver API that answers whether a point should be handled by the overlay or passed through.
3. Have the Win32 window procedure answer `WM_NCHITTEST` from that input-region decision.
4. Let `OverlayUiRoot` resolve input regions by walking the retained UI tree.
5. Return transparent for non-UI regions and client hit for interactive UI regions.

Public API language should avoid `HitTestProvider`. "Hit testing" is the Win32 implementation detail; "input region" or "selective click-through" is the user-facing concept. UI elements should participate in input regions only when they are visible, enabled, and configured to receive input. Controls that normally seem display-only, such as labels, can still receive input when the developer opts them in and attaches handlers.

Recommended public shape:

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    InputMode = OverlayInputMode.SelectiveClickThrough,
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions
{
    RegisterInputRegions = true,
});
```

This should be implemented early because it affects input routing, samples, and user expectations. The public enum value should be `OverlayInputMode.SelectiveClickThrough`.

### Public API ownership

`OverlayUiRoot` should be a disposable attachment object:

```csharp
using OverlayUiRoot ui = OverlayUi.Attach(overlay);
```

Manual disposal should detach event subscriptions and release UI theme resources. The owning overlay should also dispose attached roots during overlay disposal so callers do not leak event subscriptions if they forget to dispose the UI root explicitly.

Rendering should remain explicit:

```csharp
overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    ui.Render(frame);
};
```

That keeps render ordering in user code and leaves room for multiple UI roots or custom draw layers.

### Keyboard and text input

`TextBox`, `NumberBox`, keyboard focus, list navigation, combo-box navigation, tabs, and accessibility basics all depend on event-driven keyboard support. The core windowing layer should expose key and text events from Win32 messages rather than relying on global polling.

When a text input control is focused and enters editing mode, it should own keyboard/text message input for the overlay editing session. The implementation can temporarily use an activation-capable text-entry state if Windows requires it, then return to the previous selective click-through/no-activate behavior when editing completes or focus leaves the text input. This is the MVP default behavior, not an optional best-effort path.

Minimum core events:

- key pressed;
- key released;
- text input;
- modifier state where practical;
- repeat/scancode metadata where useful for advanced users.

### Layout engine

The layout engine should stay deterministic and DIP-based:

- measure pass;
- arrange pass;
- invalidation propagation;
- explicit bounds and desired size;
- margin, padding, alignment, min/max constraints;
- collapsed versus hidden behavior.

The first implementation should avoid data binding, templates, animations, general scrolling, and virtualization.

### Dynamic placement

Window placement should be first-class data rather than ad hoc coordinate mutation. `Manual` placement stores explicit overlay-local DIP bounds. `Anchor` resolves against the overlay's current client size. `TargetAnchor` resolves against the overlay's current tracked target bounds after converting screen pixels into overlay-local DIPs. `Cursor` resolves against the last overlay-local pointer position and treats `Thickness` as a signed offset: left/top add to the pointer position, right/bottom subtract from it. `Persisted` restores saved manual placement through `IUiLayoutStore`; the core UI package intentionally does not ship a built-in JSON or file store.

Target bounds should be sampled during render/input layout rather than pushed into the UI tree from target-tracking events. Target tracking can run outside the UI root's thread-affine render/input path, so polling the overlay's current target state during `EnsureLayout` keeps dynamic placement aligned with the existing thread model.

### Property changes and invalidation

Plain CLR properties are acceptable, but the UI layer still needs a consistent property-change model. Every UI property should declare its invalidation category:

- layout-affecting: invalidates measure and arrange;
- arrange-affecting: invalidates arrange only;
- render-affecting: invalidates render only;
- input-region-affecting: invalidates input-region resolution;
- focus/state-affecting: updates focus, capture, routed state, or visual state;
- resource-affecting: invalidates realized theme/control resources.

Controls should use internal helper APIs so property metadata is declared once rather than repeating handwritten invalidation calls in every setter. This is intentionally not a full dependency-property system. It is a lightweight change/invalidation layer for predictable layout, rendering, resource, and input behavior.

Inherited properties should be explicitly limited in 1.1. Theme and enabled/visibility state may flow through the tree; arbitrary inherited styling and data-binding semantics are out of scope.

State changes should be render-safe. Property setters that run during protected phases can record the change immediately, but tree mutations and layout-affecting consequences should be deferred according to the reentrancy policy.

The UI tree should be treated as thread-affine to the owning overlay/UI root. Cross-thread mutation is out of scope for the first implementation unless it goes through an explicit dispatcher/deferred-operation API. This keeps render, input dispatch, layout, and property invalidation deterministic without pretending that arbitrary control mutation is lock-free or safe from any thread.

### Reentrancy and deferred operations

The UI tree should define protected phases for measure, arrange, render, routed event dispatch, focus changes, popup dismissal, and capture release. During these phases, structural mutations should not modify the live tree immediately.

Tree operations requested during protected phases should be queued and applied after the current phase completes. Ordering should be FIFO within the same root, with cleanup operations such as capture release and popup dismissal applied before new focus/capture assignment where needed.

Required behaviors:

- removing the clicked element during its click handler is allowed but deferred until the current routed event finishes;
- closing a popup during event bubbling is allowed but deferred until dispatch leaves the popup branch;
- invalidating layout during arrange schedules another layout pass after the current arrange completes;
- if the capture owner is removed, disabled, hidden, or detached before pointer-up, capture is released before the next pointer event is routed;
- focus is cleared or moved predictably when the focused element disappears.

This avoids one-off defensive code in controls and makes complex interactions such as combo boxes, context menus, and window close buttons testable.

### Control implementation strategy

Build shared base classes before individual controls:

- `UiElement`;
- `UiPanel`;
- `UiControl`;
- `ContentControl`;
- `RangeBase`;
- `Selector`;
- popup host;
- focus manager;
- pointer capture manager.

Controls should be thin once those primitives exist.

### Popup, focus, and capture

Popup behavior needs a dedicated policy because popups cross normal parent/child boundaries.

Rules for 1.1:

- popups have an owner element and a popup root/layer;
- opening a popup does not steal keyboard focus by default;
- a popup can contain focusable controls when explicitly configured to do so;
- clicking inside a popup preserves owner focus unless the clicked popup child is focusable;
- pointer capture from a popup child is tracked by the owning `OverlayUiRoot`, not by a separate popup-only root;
- if the owner closes, hides, disables, or detaches, owned popups close before capture/focus is rerouted;
- nested popups are supported only for menus/context menus if implemented; otherwise nested arbitrary popups are rejected for 1.1;
- outside-click and Escape dismissal run before the next normal pointer/key dispatch.

These rules should be covered before implementing `ComboBox`, `Menu`, `ContextMenu`, and `ToolTip`.

### Theme and resources

The built-in theme should be descriptor-driven and realized through `OverlayResourceManager` outside render callbacks. Controls can expose customization through theme values and direct override properties, but native backend resources must not be created during render.

Runtime theme changes should be explicit and deterministic:

- theme descriptors are cheap managed values;
- realized brushes/fonts/images are owned by the UI root or a shared theme resource cache;
- element-level overrides can reuse root caches when descriptors match;
- backend/device recreation invalidates realized native resources and re-realizes them through existing `OverlayResourceManager` generation behavior;
- theme swaps invalidate render and any resource-dependent measurement;
- resource creation failures should produce diagnostics and use a documented fallback style where possible.

Resource ownership should be aligned with existing `docs/resource-lifetime.md` and `docs/device-recreation.md` rather than inventing a separate lifetime model for UI.

### Text editing scope

`TextBox` is part of the MVP, but robust international text support is a separate depth problem. The 1.1 text scope should be explicit before release:

- `WM_CHAR` text input is required;
- key down/up routing is required;
- caret movement, selection, deletion, and basic shortcuts are required;
- surrogate pairs and Unicode beyond BMP can be inserted as delivered .NET strings, but caret movement and deletion are UTF-16 code-unit based in 1.1;
- dead keys should work only to the extent they are delivered through normal Windows character messages;
- IME composition through `WM_IME_*` is out of scope for 1.1 unless a later implementation adds explicit composition state and tests;
- clipboard copy/cut/paste is deferred for 1.1 to avoid adding a WinForms/STA clipboard dependency before the UI package boundary is stable;
- caret blink timing belongs to the UI root scheduler, not ad hoc control timers.

When a text input control is focused and enters editing mode, it should own keyboard/text message input for the overlay editing session. The implementation can temporarily use an activation-capable text-entry state if Windows requires it, then return to the previous selective click-through/no-activate behavior when editing completes or focus leaves the text input. This is the MVP default behavior, not an optional best-effort path.

### Scrolling and virtualization boundary

`ScrollViewer` and general virtualization are deliberately post-MVP. The first release should not carry a half-designed scroll host because scrolling changes several root assumptions at once:

- layout and render coordinates need scroll transforms and clipped content bounds;
- input-region traversal must apply clipping and transforms before selecting an input target;
- pointer wheel routing needs a nearest-scroll-host policy;
- scrollbars need a normal-tree versus adorner-tree decision;
- popups opened from scrolled content need transformed anchor coordinates and overlay clamping.

Controls that commonly show multiple items in 1.1 should provide local keyboard or wheel navigation without depending on a general scroll container.

### Command model

`UiCommand` should be simple but complete enough not to feel bolted on:

- command execution callback;
- optional command parameter;
- `CanExecute`;
- `CanExecuteChanged`;
- control subscription/unsubscription on attach/detach and command replacement;
- automatic enabled/disabled visual refresh when `CanExecuteChanged` fires.

Full data binding remains out of scope for 1.1, but command state must still flow into controls predictably.

### Persistence

Persistence should be interface-only for 1.1:

```csharp
public interface IUiLayoutStore
{
    bool TryLoad(string key, out UiPlacement placement);
    void Save(string key, UiPlacement placement);
}
```

The core `ModernOverlay.UI` package should not ship a built-in file or JSON layout-store implementation in 1.1. Samples can demonstrate persistence with a small in-memory store. Any file-backed or JSON-backed adapter should be a later out-of-core sample or companion helper only after the core interface is stable.

### Sample strategy

Use two sample personalities:

- quick-start sample: one window, a few controls, selective click-through, and explicit `ui.Render(frame)`;
- stress/integration sample: multiple floating windows, popup/menu/context menu, text input, tab navigation, DPI movement, runtime theme change, and layout persistence restore through `IUiLayoutStore`.

The quick-start sample should teach the public shape. The stress sample should prove interaction policies and catch regressions across the full MVP surface.

## Recommended Implementation Order

1. `ModernOverlay.UI` project and package skeleton.
2. Core value types, element tree, child collection, invalidation.
3. Layout engine with `Canvas`, `StackPanel`, `DockPanel`, `Grid`, and `WrapPanel`.
4. Theme/resource model and render ordering.
5. Core selective click-through input-region callback in `OverlayWindow`/Win32.
6. Pointer input-region resolution, routed input, capture, focus shell.
7. Basic controls: text block, image, button, toggle, checkbox, radio, progress, slider.
8. `UiWindow` with drag, resize, close, minimize, z-order, placement, and persistence.
9. Core keyboard/text plumbing and focus navigation.
10. Text input and numeric controls.
11. Popup host, list box, combo box, menu, context menu, and tooltip.
12. Tab control, segmented control, group box, and color picker.
13. Quick-start UI sample, stress/integration UI sample, docs, tests, package validation, and manual visual validation.

## Remaining Architecture Questions

None from the initial architecture review.
