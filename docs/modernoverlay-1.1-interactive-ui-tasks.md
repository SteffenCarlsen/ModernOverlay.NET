# ModernOverlay 1.1 Interactive UI Tasks

Status date: 2026-05-24

This checklist tracks the 1.1 retained interactive UI work separately from the alpha release task list. The goal is a generic overlay UI framework with layout, controls, input routing, dynamic placement, and samples. Game-specific forms, input synthesis, process-memory behavior, bypass behavior, and app-specific feature panels stay out of scope.

## 0. Initialization

- [x] Create working branch for the 1.1 analysis.
- [x] Review the local PoC at `C:\Users\TaF\Documents\phitp_cs2\phitp_cs2\GameOverlay`.
- [x] Capture generic UI framework concepts without importing game-specific behavior.
- [x] Record the safety and scope boundary for the feature.
- [x] Decide the 1.1 package boundary: ship as separate `ModernOverlay.UI` package.
- [x] Decide the package naming convention.
  - Use `ModernOverlay.UI` for both internal and external naming to maintain consistency and simplicity.
- [ ] Add an ADR for the UI package boundary, safety boundary, and release scope.
- [x] Define the 1.1 minimum viable control set: complete retained UI MVP including layout, selective click-through, text input, popups, window chrome, and all listed controls.
- [x] Incorporate external architecture review feedback for property invalidation, reentrancy, popup/focus/capture policy, text-input scope, resource lifetime, command state, and sample strategy.

## 1. Project And Build Setup

- [x] Add `src/ModernOverlay.UI/ModernOverlay.UI.csproj`.
- [x] Add the project to `ModernOverlay.sln`.
- [x] Reference `ModernOverlay` from `ModernOverlay.UI`.
- [x] Decide whether `ModernOverlay.UI` references `ModernOverlay.Diagnostics` or exposes diagnostics through core-neutral hooks only.
  - Should reference `ModernOverlay.Diagnostics` for the sake of consistency.
- [x] Add nullable/analyzer/package metadata matching the existing repo style.
- [x] Add XML documentation generation.
- [ ] Add package metadata and package README coverage if it ships as a NuGet package.
  - Include usage instructions, sample highlights, and API overview in the package README.
- [ ] Add release-gate validation that the UI package is either emitted or intentionally absent according to the package decision.
- [x] Add a UI namespace guard so public UI types live under `ModernOverlay.UI`.

## 2. Public API Design

- [x] Draft `OverlayUi.Attach(OverlayWindow overlay, OverlayUiOptions? options = null)`.
- [x] Draft `OverlayUiRoot` lifetime ownership and disposal behavior.
- [x] Decide whether `OverlayUiRoot` is disposable, async disposable, or owned by the overlay lifetime.
  - Make `OverlayUiRoot : IDisposable` and have it dispose automatically when the overlay is disposed, but also allow manual disposal if needed for early cleanup.
- [x] Decide whether `OverlayUiRoot` automatically subscribes to render events or requires explicit `ui.Render(frame)`.
  - Require explicit `ui.Render(frame)` to give users control over render timing and allow for potential multi-root scenarios in the future.
- [x] Decide whether `OverlayUiRoot` can temporarily enable selective UI input routing, or whether users must opt in explicitly through overlay options.
  - Do not let `OverlayUiRoot` silently mutate input behavior. Make selective click-through an explicit overlay/window option, then let `OverlayUiRoot` provide input-region resolution.
- [x] Define how users add root content: `ui.Root.Add(...)`, `ui.Children`, or `ui.Content`.
  - Use `ui.Root.Children.Add(...)`, with convenience `ui.Root.Add(...)`.
- [ ] Define fluent/builder helpers only after object-model APIs are stable.
- [ ] Add sample compile tests for the intended public API shape.
- [ ] Add XML doc comments for all public UI entry points.

## 3. Core UI Value Types

- [x] Implement `Thickness` with uniform, horizontal/vertical, and full constructors.
- [x] Implement `UiSize` for width/height in DIPs.
- [ ] Implement `UiPoint` only if `PointF` is not sufficient.
- [ ] Implement `UiRect` only if `RectF` is not sufficient.
- [x] Implement `UiConstraints` or equivalent min/max layout constraints.
  - `UiConstraints` centralizes min/max validation and clamping while `UiElement` keeps the existing `MinWidth`, `MinHeight`, `MaxWidth`, and `MaxHeight` convenience properties.
- [x] Implement `UiAlignment` for horizontal and vertical alignment.
- [x] Implement `UiOrientation` for horizontal/vertical layout.
- [x] Implement `UiVisibility`: visible, hidden, collapsed.
- [x] Implement `UiZLayer` or layer constants for content, floating windows, popups, adorners.
- [ ] Add tests for value-type equality, defaults, invalid sizes, and clamping.

## 4. Retained Element Model

- [x] Implement abstract `UiElement`.
- [x] Add `Parent`, `Root`, `Name`, `Tag`, `IsVisible`, `IsEnabled`, `Opacity`, and `ZIndex`.
- [x] Add layout properties: `Margin`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight`, `Width`, `Height`, `HorizontalAlignment`, `VerticalAlignment`.
- [x] Add state properties: `IsMouseOver`, `IsPressed`, `IsFocused`, `IsKeyboardFocusWithin`, `IsPointerCaptured`.
- [x] Add computed layout state: `DesiredSize`, `Bounds`, and `ContentBounds`.
- [x] Add invalidation APIs: `InvalidateMeasure`, `InvalidateArrange`, `InvalidateRender`.
- [x] Add protected virtual lifecycle hooks: `MeasureCore`, `ArrangeCore`, `RenderCore`, `OnAttached`, `OnDetached`.
- [x] Add public event hooks for loaded/unloaded or attached/detached if useful.
  - `UiElement.Attached` and `UiElement.Detached` fire alongside the protected lifecycle hooks.
- [x] Ensure child/parent mutations are deferred during measure, arrange, render, and routed event dispatch.
- [ ] Add tests for parent ownership, invalidation propagation, visibility, enabled state, and mutation guards.

### 4.1 Property Change And Invalidation Model

- [x] Define UI property invalidation categories: layout, arrange, render, input-region, focus/state, and resource.
- [x] Implement internal property-set helper APIs so controls do not repeat handwritten invalidation logic in every setter.
- [x] Define which properties are inherited through the UI tree, limited initially to theme and enabled/visibility-style state where needed.
  - 1.1 limits inheritance to theme/resource context and effective enabled/visibility-style state where needed for input, focus, and rendering. Arbitrary inherited styling and data-binding-style property inheritance are out of scope.
- [x] Ensure visual-state changes invalidate render and update input/focus state consistently.
  - `UiElement.VisualState` exposes normal, hover, pressed, disabled, and focused state; pointer, focus, enabled, and visibility updates already invalidate render/input/focus state through the root.
- [x] Ensure property changes during protected phases are render-safe and schedule deferred consequences when needed.
- [x] Define UI tree thread affinity and reject or marshal cross-thread mutation through an explicit root-owned API.
- [ ] Add tests proving property setters trigger the correct invalidation category and do not over-invalidate unrelated phases.

### 4.2 Reentrancy And Deferred Operations

- [x] Define protected phases: measure, arrange, render, routed event dispatch, focus changes, popup dismissal, and capture release.
- [x] Implement a per-root deferred operation queue for structural mutations requested during protected phases.
- [x] Define FIFO ordering guarantees for deferred mutations within the same root.
- [ ] Apply capture release and popup cleanup before new focus/capture assignment when deferred operations conflict.
- [x] Allow click handlers to remove the clicked element without corrupting event dispatch.
- [ ] Allow popup close during event bubbling without invalidating the current route.
- [ ] Schedule a follow-up layout pass when layout is invalidated during arrange.
- [x] Release capture predictably when the capture owner is removed, disabled, hidden, or detached.
- [ ] Add tests for removal during click, popup close during bubbling, invalidation during arrange, capture-owner removal, and focus-owner removal.

## 5. Panel And Child Collection Model

- [x] Implement `UiPanel` as the base class for elements with children.
- [x] Implement an owned `UiElementCollection` that manages parent assignment.
- [x] Reject adding one child to multiple parents.
- [x] Reject adding an ancestor as a descendant.
- [x] Preserve insertion order for equal z-index.
- [x] Add child added/removed invalidation.
- [x] Add tree traversal helpers for render order and input-region order.
- [ ] Add tests for collection mutation, parent assignment, order, and tree-cycle prevention.

## 6. Layout Engine

- [x] Implement a root layout pass that runs before rendering when measure/arrange is invalid.
- [x] Define coordinate units as DIPs throughout the UI tree.
- [x] Convert overlay/window pixel bounds to root DIP constraints using the current DPI scale.
- [x] Implement measure pass with available size constraints.
- [x] Implement arrange pass with final bounds.
- [x] Implement margin, padding, min/max, explicit width/height, and alignment.
- [x] Implement collapsed visibility removing an element from layout.
- [x] Implement hidden visibility preserving layout but skipping render/input.
- [x] Implement render clipping behavior for elements and panels.
- [x] Add layout diagnostics for repeated invalidation loops.
  - `OverlayUiRoot` caps repeated same-frame layout passes and emits `UiLayoutLoop` before leaving layout invalidation for a later frame.
- [ ] Add tests for measure/arrange order, constraints, min/max, margin, padding, alignment, collapsed/hidden behavior, and clipping.

## 7. Layout Panels

### 7.1 Canvas

- [x] Implement absolute `Canvas`.
- [x] Add attached coordinates or per-child placement records: left, top, right, bottom.
- [x] Support explicit child size and natural desired size.
- [x] Define behavior when both left/right or top/bottom are set.
  - If both sides are set on an axis, `Canvas` stretches the child across the remaining space on that axis. If only right or bottom is set, the child keeps its desired size and is anchored to that edge.
- [ ] Add tests for absolute placement, right/bottom anchoring, z-order, and clipping.

### 7.2 StackPanel

- [x] Implement vertical and horizontal `StackPanel`.
- [x] Add `Spacing`.
- [x] Support child margins.
- [x] Respect child alignment on the cross axis.
- [ ] Add tests for vertical stacking, horizontal stacking, spacing, margin, collapsed children, and cross-axis alignment.

### 7.3 DockPanel

- [x] Implement `DockPanel` for framed windows and toolbars.
- [x] Add left, top, right, bottom, fill-last-child behavior.
- [ ] Add tests for dock ordering and fill behavior.

### 7.4 Grid

- [x] Decide whether `Grid` is in 1.1 or deferred: include in 1.1 MVP.
- [x] Implement fixed, auto, and star rows/columns.
- [x] Add row/column span support only if required by samples.
  - `Grid` now exposes `SetRowSpan`, `SetColumnSpan`, `GetRowSpan`, and `GetColumnSpan`; span placement invalidates the parent layout like row/column changes.
- [ ] Add tests for fixed/auto/star sizing and spans.

### 7.5 WrapPanel

- [x] Decide whether `WrapPanel` is in 1.1 or deferred: include in 1.1 MVP.
- [x] Implement horizontal wrapping first.
- [ ] Add tests for wrapping, spacing, and available width changes.

## 8. Dynamic Placement

- [x] Implement `UiPlacement` records for manual, anchor, target anchor, cursor, and persisted placement.
- [x] Implement `OverlayAnchor` values: top-left, top, top-right, left, center, right, bottom-left, bottom, bottom-right.
- [x] Add placement margins and offsets.
- [x] Add placement constraints: clamp to overlay, allow overflow, preserve size, preserve visible header.
  - `UiWindow` exposes placement clamping toggles and preserves size while keeping the title/header visible when possible.
- [x] Recompute anchored placements when overlay bounds or DPI changes.
  - `OverlayUiRoot` invalidates layout when root DIP bounds change so anchored windows are recalculated.
- [x] Recompute target-anchored placements when target bounds change.
  - `OverlayUiRoot` reads the overlay's current target bounds during layout, converts them into overlay-local DIPs, and invalidates layout when those bounds differ from the last arranged target bounds.
  - Cursor placements use the last pointer position in overlay-local DIPs and apply `Thickness` as `left/top` positive offset minus `right/bottom` offset.
- [x] Convert dragged anchored panels to manual placement if configured.
- [x] Add `IUiLayoutStore` or equivalent persistence abstraction.
- [x] Decide not to ship a built-in JSON or file layout-store helper in the core 1.1 UI package.
  - Layout persistence stays interface-only through `IUiLayoutStore`; any file-backed or JSON-backed adapter must live outside the core package or be revisited after the interface is stable.
- [x] Add initial placement tests for manual placement, overlay anchoring, cursor placement, clamping, persisted fallback, persisted stored bounds, and target-bound recompute.
- [ ] Add tests for every placement kind, DPI changes, target-bound changes, clamping, and persisted fallback.

## 9. Rendering Foundation

- [x] Define `UiRenderContext` as a thin wrapper over `DrawContext` plus UI theme/resources.
- [x] Decide whether controls render directly with `DrawContext` or through `UiRenderContext` only.
- [x] Add default clipping around each element.
- [x] Add render layers: normal, floating, popup, adorner.
- [x] Add deterministic sort by layer, z-index, and insertion order.
- [x] Add default focus visuals and hover/pressed visuals.
  - Interactive controls resolve default hover, pressed, and focus brushes through shared style helpers.
- [ ] Add opacity support if it can be implemented without expensive per-control offscreen composition.
- [ ] Add tests using a fake draw command sink to verify render order and clipping calls.

## 10. Theming And Styling

- [x] Implement `UiTheme`.
- [x] Add resource descriptors for common brushes and fonts.
- [x] Realize theme resources through `OverlayResourceManager` outside the render callback.
- [x] Add default theme with neutral colors and readable typography.
- [x] Add style properties for background, foreground, border, accent, disabled, hover, pressed, focus, popup, and window chrome.
  - `UiElement` exposes caller-owned brush overrides for `Background`, `Foreground`, `BorderBrush`, `AccentBrush`, `DisabledBrush`, `HoverBackground`, `PressedBackground`, `FocusBrush`, `PopupBackground`, and `WindowChromeBackground`.
- [x] Decide whether 1.1 ships a built-in theme: ship a built-in theme with customization hooks.
- [x] Decide whether controls expose direct style properties, style classes, or both.
  - 1.1 uses direct style properties first; style classes are deferred until the object model and samples prove a real need.
- [x] Define runtime theme-swap semantics.
  - `OverlayUiRoot.ApplyTheme` replaces root-owned theme resources and invalidates measure/arrange/render/resource state.
- [x] Define eager versus lazy resource realization for theme resources.
  - Managed theme handles are realized eagerly on attach and theme swap; native backend realizations remain lazy through `OverlayResourceManager`.
- [x] Define disposal ownership for theme-realized resources and element-level overrides.
  - `UiThemeResources` owns and disposes root theme handles; ad hoc element-created handles stay owned by the creating control or caller until dedicated override APIs exist.
- [x] Re-realize UI resources on backend/device recreation using existing `OverlayResourceManager` generation behavior.
  - UI theme handles remain device-independent descriptors; `OverlayUiRoot` invalidates render/resource state on `DeviceRestored` so native realizations are rebuilt lazily when drawn.
- [ ] Add fallback style behavior and diagnostics for resource creation failures.
- [ ] Add high-contrast/readability checks for default colors.
- [ ] Add tests for theme resource creation, disposal, and backend generation behavior.

## 11. Input Event Model

- [x] Define `UiPointerEventArgs`.
- [x] Define `UiKeyboardEventArgs`.
- [x] Define `UiTextInputEventArgs`.
- [x] Define routed event phases: preview/tunnel, target, bubble, or choose a simpler direct-plus-bubble model.
  - 1.1 uses a direct-plus-bubble model; routed pointer, keyboard, and text input args expose `RoutePhase`, `Source`, and `OriginalSource`.
- [x] Add event handled semantics.
  - Routed pointer, keyboard, and text input args stop bubbling when `Handled` is set.
- [x] Translate `OverlayWindow.PointerMoved` to UI pointer move.
- [x] Translate `OverlayWindow.PointerPressed` to UI pointer down.
- [x] Translate `OverlayWindow.PointerReleased` to UI pointer up and click.
- [x] Translate `OverlayWindow.PointerWheel` to UI wheel.
- [x] Track pointer enter/leave.
- [x] Track hover target independent from captured target.
- [x] Add double-click detection if useful for window title bars and list items.
  - `OverlayUiRoot` classifies same-target/same-button clicks within configurable time and distance; routed pointer and button click args expose `ClickCount` and `IsDoubleClick`.
- [ ] Add tests for event routing, handled behavior, enter/leave, click, double-click if included, and wheel.

## 12. Input Regions

- [x] Implement point input-region resolution in root DIP coordinates.
- [x] Traverse topmost visible enabled elements first.
- [x] Respect clipping.
- [x] Respect per-element input participation.
- [x] Add `ReceivesInput` or equivalent per-element opt-in property.
- [x] Add optional custom input-region override per element.
  - `UiElement.InputRegion` lets an opt-in input element narrow or reshape its interactive area while preserving normal child traversal and clipping.
- [x] Add input-region behavior for transparent backgrounds.
  - Transparent-looking panels remain pass-through unless `ReceivesInput` is enabled; visual transparency does not imply input capture, and `InputRegion` can narrow controls with transparent regions.
- [x] Add popup-layer input-region resolution before normal content.
  - Open popup regions are resolved before normal root input so dropdown/content outside owner bounds can still receive pointer input.
- [ ] Add tests for nested panels, clipping, z-index, popups, disabled elements, hidden/collapsed elements, and custom input regions.

## 13. Pointer Capture And Drag State

- [x] Implement `CapturePointer(UiElement element)`.
- [x] Implement `ReleasePointerCapture(UiElement element)`.
- [x] Release capture automatically when element is removed, disabled, hidden, or root is disposed.
- [x] Route move/up/wheel to captured element when capture is active.
- [x] Track drag threshold to distinguish click from drag.
  - `OverlayUiRoot.DragThreshold` marks routed pointer move/release events as drag gestures after meaningful movement, and buttons suppress click activation on dragged releases.
- [ ] Add tests for capture, capture release, element removal during capture, and click suppression after drag.

## 14. Focus And Keyboard Input

- [x] Add focus manager to `OverlayUiRoot`.
- [x] Add `Focusable` property.
- [x] Add `Focus()`, `Blur()`, `MoveFocusNext()`, and `MoveFocusPrevious()`.
- [x] Add tab order using tree order plus optional `TabIndex`.
- [x] Add escape/enter command conventions for windows and buttons.
  - Buttons invoke only when Enter/Space and `CanExecute` are valid; windows close on Escape and restore from minimized state on Enter.
- [x] Define how focus behaves when a focused element is disabled, hidden, removed, or moved to another root.
- [ ] Add tests for focus set/clear, tab navigation, disabled elements, removed elements, and routed keyboard events.

## 15. Core Overlay Keyboard/Text Plumbing

- [x] Add `Win32KeyboardEvent` and `Win32TextInputEvent` records in `ModernOverlay.Win32`.
- [x] Add Win32 constants for `WM_KEYDOWN`, `WM_KEYUP`, `WM_SYSKEYDOWN`, `WM_SYSKEYUP`, `WM_CHAR`, and `WM_SYSCHAR`.
- [x] Add keyboard callback storage to `Win32OverlayWindow.WindowState`.
- [x] Add `SetKeyboardCallback` and `SetTextInputCallback`.
- [x] Translate virtual key, scan code, repeat count, extended-key flag, previous-state flag, and transition flag.
- [x] Translate character input from `WM_CHAR` without global keyboard polling.
- [x] Add public `OverlayWindow.KeyPressed`, `KeyReleased`, and `TextInput` events.
- [x] Add public key event args without exposing raw Win32 details unless useful.
- [x] Decide text-entry input ownership: focused text controls should own keyboard/text messages while editing.
- [x] Add text-entry activation handling so focused text controls own keyboard/text messages while editing.
  - Focused `TextBox` handles text-input events even when read-only or when characters are filtered, preventing text messages from bubbling to parents.
- [x] Define IME scope: `WM_IME_*` support, partial support, or explicit out-of-scope behavior for 1.1.
  - IME composition through `WM_IME_*` is out of scope for 1.1 unless a later implementation slice adds explicit composition state and tests.
- [x] Define dead-key and keyboard-layout expectations.
  - Dead keys and keyboard-layout behavior are supported only to the extent Windows delivers composed characters through `WM_CHAR`/`WM_SYSCHAR`.
- [x] Define surrogate pair / Unicode beyond BMP handling.
  - `TextBox` stores .NET strings and inserts delivered text, but caret movement/deletion are UTF-16 code-unit based in 1.1; full text-element/grapheme editing is deferred.
- [x] Define clipboard support scope and whether copy/paste ships in 1.1.
  - Clipboard copy/cut/paste is deferred for 1.1 to avoid adding a WinForms/STA clipboard dependency before the UI package boundary is stable.
- [x] Define caret blink timing ownership in the UI root scheduler.
  - `OverlayUiRoot.CaretBlinkInterval` owns caret blink timing; `TextBox` restarts the blink on caret movement and renders the caret only when the root blink state is visible.
- [ ] Add tests for Win32 message translation.
- [ ] Add sample coverage for text input once `TextBox` exists.

## 16. Command And Binding Model

- [x] Implement `UiCommand`.
- [x] Support command execution with `CanExecute`.
- [x] Add `CanExecuteChanged`.
- [x] Add optional command parameter support.
- [x] Define control subscription/unsubscription behavior for `CanExecuteChanged`.
- [x] Ensure command-enabled state automatically updates disabled visuals and input participation.
- [ ] Add simple property changed helpers only if control state needs binding.
- [x] Decide whether full data binding is out of scope for 1.1.
  - Full data binding is out of scope for 1.1; controls expose direct properties plus explicit changed events.
- [x] Add change callbacks for value controls: checked, selected item, slider value, text changed.
  - Toggle/check/radio controls expose `CheckedChanged`; selectors expose `SelectionChanged`; range and number controls expose `ValueChanged`; `TextBox` exposes `TextChanged`.
- [ ] Add tests for command execution, disabled command state, and event callbacks.

## 17. Control Base Classes

- [x] Implement `Control` or `UiControl` as the base class for interactive controls.
  - `UiControl` is the base for the main non-panel interactive controls.
- [x] Add common visual states: normal, hover, pressed, disabled, focused.
  - `UiVisualState` and `UiElement.VisualState` define the common state vocabulary.
- [x] Add content/alignment support where needed.
  - `ContentControl` exposes a retained `Content` child plus content horizontal/vertical alignment; `Button` uses it for child content.
- [x] Add `ContentControl` for controls with one child/content object.
- [x] Add `HeaderedContentControl` if windows/group boxes/tabs need it.
- [x] Add `RangeBase` for slider/progress/number controls.
- [x] Add `Selector` for list/combo/radio groups.
  - `Selector` now owns item collection, selected index/item, display-text callback, and selection-change notification for list/combo controls.
- [ ] Add tests for common visual states, command state, and inherited behavior.

## 18. Text And Display Controls

### 18.1 TextBlock / Label

- [x] Implement `TextBlock`.
- [x] Add text, font, foreground, alignment, wrapping, trimming, max lines, and line spacing.
  - `TextBlock` supports caller-owned font/foreground handles, horizontal text alignment, basic wrap/no-wrap layout, character ellipsis trimming, max lines, and line spacing.
- [x] Use existing text measurement APIs for desired size.
  - `TextBlock` measures lines through the active `DrawContext` during render-driven layout, with the previous font-size estimate retained as a fallback for input-region layout before a render frame is available.
- [x] Add `Label` as alias/subclass only if it adds target/access-key semantics.
  - `Label` subclasses `TextBlock` and adds target focus behavior on pointer press.
- [ ] Add tests for measurement, wrapping, trimming, empty text, and render output.

### 18.2 Image

- [x] Decide whether `Image` control is in 1.1: include in 1.1 MVP.
- [x] Support `ImageHandle`, stretch modes, alignment, opacity, and source rect.
  - Intrinsic image dimensions are not exposed by `ImageHandle`; `Image` uses `SourceRect` or explicit element size as natural size and otherwise draws into arranged content.
- [ ] Add tests for desired size and render command output.

### 18.3 ProgressBar

- [x] Implement `ProgressBar` on top of `RangeBase`.
- [x] Add minimum, maximum, value, orientation, indeterminate flag if low cost.
  - Minimum, maximum, value, and orientation are implemented; indeterminate remains optional.
- [ ] Add tests for value clamping, fill ratio, disabled visuals, and render output.

## 19. Button Controls

### 19.1 Button

- [x] Implement `Button`.
- [x] Support text content first.
- [x] Support child content if `ContentControl` exists.
  - `Button` now derives from `ContentControl`; `Text` remains the lightweight fallback when no retained child content is assigned.
- [x] Invoke click on pointer press/release within the control.
- [x] Invoke command on click and Enter/Space when focused.
- [ ] Add tests for click, command, disabled state, keyboard activation, and pointer cancel after drag/leave.

### 19.2 ToggleButton

- [x] Implement `ToggleButton`.
- [x] Add `IsChecked`.
- [x] Add checked/unchecked/indeterminate only if tri-state is needed.
  - `ToggleButton` exposes `UiToggleState`, `CheckState`, `IsThreeState`, `IsChecked`, and `IsIndeterminate`; `CheckBox` renders checked and indeterminate states.
- [ ] Add tests for toggle by pointer, keyboard, command callback, and disabled state.

### 19.3 CheckBox

- [x] Implement `CheckBox` based on `ToggleButton`.
- [x] Add box glyph plus text/content.
- [ ] Add tests for layout, glyph rendering, checked state, and keyboard activation.

### 19.4 RadioButton

- [x] Implement `RadioButton`.
- [x] Add group name and container-scoped group behavior.
- [x] Ensure selecting one option clears peers in the same group.
- [ ] Add tests for group behavior, keyboard activation, disabled state, and dynamic add/remove.

## 20. Range And Numeric Controls

### 20.1 Slider

- [x] Implement `Slider` on top of `RangeBase`.
- [x] Support horizontal orientation first.
- [x] Support vertical orientation if low cost.
- [x] Add minimum, maximum, value, step/frequency, small change, large change.
  - Minimum, maximum, value, small change, and large change are implemented through `RangeBase`; separate tick/frequency visuals remain optional.
- [x] Add pointer capture during thumb drag.
- [x] Add click-to-track behavior.
- [x] Add keyboard arrows/home/end/page behavior.
- [ ] Add tests for value clamping, drag, capture, track click, keyboard changes, disabled state, and render output.

### 20.2 NumberBox

- [x] Decide whether `NumberBox` is 1.1 or deferred: include in 1.1 MVP.
- [x] Compose `TextBox` plus increment/decrement buttons.
- [x] Add min/max/step, parsing, formatting, and validation.
- [ ] Add tests for parsing, invalid input, buttons, keyboard, and culture-invariant behavior.

## 21. Text Input Controls

### 21.1 TextBox

- [x] Decide whether `TextBox` can be deferred: include in 1.1 MVP.
- [x] Implement `TextBox` after core keyboard/text plumbing is in place.
- [x] Add text, caret index, selection start/length, placeholder, max length, read-only, password char if useful.
- [x] Handle text input, backspace, delete, left/right/home/end, Ctrl+A, copy/paste only if clipboard support is accepted.
- [x] Add mouse click caret placement.
- [x] Add drag selection if feasible.
- [x] Add horizontal scroll for long single-line text.
- [ ] Document IME, dead-key, Unicode, clipboard, and international input limitations before release.
- [ ] Add tests for text input, caret movement, deletion, selection, focus, disabled/read-only state, and rendering.

### 21.2 SearchBox

- [x] Decide whether `SearchBox` is a separate MVP control or a sample composition of `TextBox`: defer as post-MVP follow-up.

## 22. Selection And Popup Controls

### 22.1 Popup Infrastructure

- [x] Implement popup host/layer.
  - Popups participate in the retained tree at `UiLayer.Popup`, and root input dispatch prioritizes open popup regions.
- [x] Support placement relative to owner element.
  - `Popup`, `ContextMenu`, and `ToolTip` can place by owner anchor plus popup anchor and offset while preserving existing absolute placement.
- [x] Support popup clipping or screen/overlay clamping.
  - Popup controls clamp to root bounds by default; `ComboBox` can flip above the owner when there is not enough room below.
- [x] Close popups on outside click, Escape, owner removal, or root disposal.
- [x] Route input to popup before normal content.
- [ ] Add tests for popup placement, z-order, outside click, Escape, owner removal, and nested popups if allowed.
- [x] Define popup ownership rules.
  - Popups have an owner element and are hosted in the owning `OverlayUiRoot` popup layer; owner close, hide, disable, detach, or root disposal closes owned popups before normal input continues.
- [x] Define whether opening a popup steals keyboard focus; default should preserve owner focus.
  - Opening a popup preserves owner focus by default. Focus moves into the popup only when the user interacts with a focusable popup child or an explicitly focusable popup scenario opts in.
- [x] Define whether popup children can be focusable and how focus moves into them.
  - Popup children may be focusable when explicitly configured. Clicking a non-focusable popup surface preserves owner focus; clicking or tabbing to a focusable popup child moves focus to that child through the normal root focus manager.
- [x] Define pointer capture behavior for controls inside popups.
  - Pointer capture from popup children is tracked by the owning `OverlayUiRoot`, not by a popup-local root, so capture release and owner cleanup share the same rules as normal controls.
- [x] Define dismissal order for outside click, Escape, owner close, owner hide, owner disable, and root disposal.
- [x] Define nested popup policy for menus/context menus and reject arbitrary nested popups if unsupported in 1.1.
  - Nested popups are allowed only for menu/context-menu submenu scenarios implemented by the 1.1 controls; arbitrary nested popups remain unsupported and should be rejected or flattened until a general policy exists.
- [ ] Add tests for focus preservation, focusable popup children, capture inside popup, owner close while popup has capture, and nested popup policy.

### 22.2 ListBox

- [x] Implement `ListBox`.
- [x] Support item source as simple strings/objects with display text callback.
  - `ListBox` inherits `Selector`, accepts object items, and uses `DisplayTextSelector` or `ToString()`.
- [x] Support selected index and selected item.
- [x] Support keyboard up/down/home/end.
- [x] Support simple wheel/keyboard list navigation without depending on a general `ScrollViewer`.
- [ ] Add tests for selection, keyboard, pointer, disabled items, and dynamic items.

### 22.3 ComboBox

- [x] Decide whether `ComboBox` is in the first MVP: include in 1.1 MVP.
- [x] Implement `ComboBox` after popup host and list selection are stable.
- [x] Support selected index/item, placeholder, dropdown open/close, max dropdown height.
  - `ComboBox` inherits `Selector`, accepts object items, and uses `DisplayTextSelector` or `ToString()`.
- [x] Draw dropdown above normal controls.
- [ ] Add tests for open/close, item selection, outside click close, Escape close, keyboard navigation, and z-order.

### 22.4 Menu / ContextMenu

- [x] Decide whether `Menu` and `ContextMenu` are part of the complete 1.1 MVP or explicit 1.2 follow-up controls: include both in 1.1 MVP.
- [x] Implement `Menu`.
- [x] Implement `ContextMenu`.
- [ ] Add tests for menu open/close, keyboard navigation, submenu behavior if included, command invocation, and outside-click dismissal.
  - Menu and context menu keyboard navigation are implemented; tests remain open.

## 23. Window And Panel Controls

### 23.1 UiWindow

- [x] Implement `UiWindow` or `UiPanelWindow`.
- [x] Add title/header.
- [x] Add content slot.
- [x] Add draggable header.
- [x] Add resize grip.
- [x] Add min/max size.
- [x] Decide whether close/minimize are in 1.1: include both in 1.1 MVP.
- [x] Add close button with defined lifecycle semantics.
- [x] Decide minimized panel behaviors: add `MinimizeBehavior` enum and implement title-bar collapse, hide until restored, and dock/tray region.
- [x] Add `MinimizeBehavior` enum.
- [x] Add minimize button and all minimized panel behaviors.
- [x] Add active/focused visual state.
- [x] Bring to front on pointer press.
- [x] Persist manual placement through `IUiLayoutStore` if configured.
- [ ] Add tests for drag, resize, clamping, z-order, active state, close behavior, minimize behavior, and persistence callbacks.

### 23.2 GroupBox

- [x] Decide whether `GroupBox` is a separate MVP control or a styled `ContentControl`: include in 1.1 MVP.
- [x] Implement header plus content border.

### 23.3 ToolTip

- [x] Decide whether `ToolTip` is a required MVP control or a 1.2 follow-up: include in 1.1 MVP.
- [x] Implement delayed hover popup with placement/clamping.
  - `ToolTip` can subscribe to an owner element, open after `InitialDelay`, close on owner exit/press/unavailability, and reuse popup placement/clamping.

## 24. Scrolling And Virtualization

- [x] Decide whether `ScrollViewer` is required for 1.1: defer `ScrollViewer` and general virtualization.
- [x] Document `ScrollViewer` as post-MVP due to transformed input regions, clipping, wheel-routing, popup-placement, and virtualization complexity.
- [x] Ensure list/combo/menu controls provide local wheel or keyboard navigation without requiring a general scroll host.
- [ ] Revisit transformed coordinate handling before implementing `ScrollViewer` after 1.1.

## 25. Tabs And Segmented Controls

- [x] Decide whether tabs are required for the first 1.1 implementation: include tab or segmented navigation in the MVP.
- [x] Decide whether to ship `TabControl`, `SegmentedControl`, or both: include both in 1.1 MVP.
- [x] Implement `TabControl`, `TabItem`, selected index, header layout, and content switching.
- [x] Implement a lightweight `SegmentedControl` for mode switching.
- [ ] Add tests for selection, keyboard navigation, disabled tabs, and layout.
  - Tab keyboard navigation is implemented for arrow, Home, and End keys while skipping disabled tabs; tests remain open.

## 26. Color Controls

- [x] Decide whether `ColorPicker` is 1.1 or deferred: include in 1.1 MVP.
- [x] Start with swatches plus RGBA sliders before adding HSL/HSV.
- [x] Add color value model using `ColorRgba`.
- [ ] Add tests for value conversion, swatch selection, slider updates, and event callbacks.

## 27. Selective Click-Through

- [x] Decide whether selective click-through belongs in 1.1: include selective click-through in 1.1 MVP.
- [x] Decide public terminology: use selective click-through and input regions; keep hit testing as an implementation detail.
- [x] Decide whether to add `OverlayInputMode.SelectiveClickThrough` or shorter `OverlayInputMode.Selective`: use `OverlayInputMode.SelectiveClickThrough`.
- [x] Add `OverlayInputMode.SelectiveClickThrough`.
- [x] Add a callback from core Win32 hit testing to the UI root's input-region resolver.
- [x] Ensure non-UI overlay regions can remain click-through while UI controls receive input.
- [ ] Add Win32 tests for `WM_NCHITTEST` behavior.
- [ ] Add sample validation for mixed click-through and interactive UI regions.
- [ ] Document selective click-through behavior, input-region behavior, limitations, and fallback behavior.

## 28. Accessibility And Usability Baseline

- [x] Add keyboard navigation for all focusable controls.
  - Button, slider, text box, list box, combo box, tab control, segmented control, menu/context menu, and window chrome now have keyboard handling.
- [x] Add visible focus state.
  - Focusable controls render an accent focus cue.
- [x] Add disabled state for all controls.
  - Retained input routing already excludes disabled elements; common controls now render disabled text, borders, selection, range, popup, tab, segmented, color, and window states with theme disabled colors.
- [ ] Add minimum hit-target guidance.
- [ ] Add text contrast guidance for default theme.
- [x] Decide whether UI Automation support is out of scope for 1.1.
  - UI Automation providers, screen-reader tree exposure, and accessibility patterns are out of scope for 1.1; keyboard navigation, visible focus, disabled visuals, hit-target guidance, contrast guidance, and honest limitation docs remain in scope.
- [ ] Document accessibility limitations honestly.

## 29. Diagnostics

- [x] Add optional UI diagnostics counters: element count, layout passes, render passes, input-region checks, routed events, active popup count.
  - `OverlayUiRoot.Metrics` exposes a snapshot with element count, layout/render pass counts, input-region checks, routed events, and active popup count.
- [x] Add logging hooks for layout loops, invalid placement, unhandled exceptions, and resource realization failures.
  - UI diagnostics emit `UiLayoutLoop`, `UiInvalidPlacement`, `UiUnhandledException`, and `UiResourceRealizationFailure` through `OverlayEventSource`.
- [x] Decide whether diagnostics use `OverlayEventSource`, `Microsoft.Extensions.Logging`, or both.
  - Use `OverlayEventSource` as the primary UI diagnostics contract; applications can bridge to `Microsoft.Extensions.Logging` with the existing `OverlayEventSourceLogger`.
- [ ] Add tests for diagnostics counters where practical.
- [ ] Add diagnostics sample panel if useful.

## 30. Samples

- [x] Add `samples/InteractiveUiOverlay`.
- [x] Demonstrate anchored and manual panels.
- [x] Demonstrate draggable/resizable `UiWindow`.
- [x] Demonstrate text block, button, checkbox, radio buttons, slider, progress bar, list/combo, textbox, tabs, segmented control, and color picker.
- [ ] Demonstrate popup z-order.
- [ ] Demonstrate keyboard focus and tab navigation.
- [x] Demonstrate layout persistence with an in-memory `IUiLayoutStore` sample store.
- [x] Add a quick-start UI sample with one window, a few controls, selective click-through, and explicit `ui.Render(frame)`.
- [ ] Add a stress/integration UI sample with multiple floating windows, popup/menu/context menu, text input, tab navigation, DPI movement, theme change, and persistence restore.
- [x] Keep both samples neutral with no game-specific content.
- [ ] Add sample compile tests.
- [x] Add `tools\Start-ModernOverlaySample.ps1` support for the new sample.
- [x] Update `samples/README.md`.

## 31. Documentation

- [ ] Add `docs/interactive-ui.md`.
- [ ] Document retained UI concepts and how they relate to immediate-mode drawing.
- [ ] Document property-change categories and invalidation behavior.
- [ ] Document deferred mutation and protected-phase behavior.
- [ ] Document input mode requirements.
- [ ] Document layout panels and dynamic placement.
- [ ] Document popup ownership, focus, capture, dismissal, and nested popup policy.
- [ ] Document every shipped control with short examples.
- [ ] Document theming and resource lifetime.
- [ ] Document focus/keyboard/text input limitations.
- [ ] Document text editing limitations for IME, Unicode, dead keys, clipboard, and caret timing.
- [ ] Document selective click-through and input-region status.
- [ ] Document `ScrollViewer` and virtualization as post-MVP deferred features.
- [ ] Document safety boundary and non-goals.
- [ ] Update `docs/README.md` once `interactive-ui.md` exists.
- [ ] Update root README feature table when the feature is implemented.
- [ ] Update package README/release notes for 1.1.

## 32. Tests

- [ ] Add layout unit tests.
- [ ] Add property invalidation category tests.
- [ ] Add deferred mutation/reentrancy tests.
- [ ] Add placement unit tests.
- [ ] Add element-tree mutation tests.
- [ ] Add input-region tests.
- [ ] Add routed input tests.
- [ ] Add pointer capture tests.
- [ ] Add popup/focus/capture interaction tests.
- [ ] Add focus/keyboard routing tests.
- [ ] Add text input tests after core keyboard/text plumbing exists.
- [ ] Add text editing scope tests for supported Unicode/dead-key/clipboard behavior.
- [ ] Add render-order tests with fake draw command sink.
- [ ] Add theme/resource lifetime tests.
- [ ] Add command `CanExecuteChanged` and parameter tests.
- [ ] Add control-specific tests for every shipped control.
- [ ] Add sample compile tests.
- [ ] Add Win32 integration tests for keyboard/text events.
- [ ] Add optional manual visual checklist for the interactive UI sample.

## 33. Validation Gates

- [x] Run `dotnet build ModernOverlay.sln --configuration Release`.
- [ ] Run `dotnet test ModernOverlay.sln --configuration Release --verbosity minimal`.
- [ ] Run focused UI unit tests while iterating.
- [ ] Run Win32 integration tests for input plumbing on a desktop session.
- [ ] Run `tools\Start-ModernOverlaySample.ps1 InteractiveUiOverlay` for manual sample validation.
- [ ] Run the release gate after package/docs changes.
- [ ] Record the 1.1 validation result in a dated release validation doc or a dedicated UI validation doc.

## 34. Proposed Implementation Order

1. Package/project skeleton and ADR.
2. Core value types, element tree, child collection, and layout invalidation.
3. `Canvas`, `StackPanel`, root layout pass, and render ordering.
4. Theme/resource descriptors and basic render visuals.
5. Pointer input-region resolution, routed pointer events, capture, and focus shell.
6. `TextBlock`, `Button`, `ToggleButton`, `CheckBox`, `RadioButton`, `ProgressBar`, and `Slider`.
7. `UiWindow` with drag, resize, z-order, placement, clamping, and persistence hooks.
8. Popup host, `ListBox`, and `ComboBox`.
9. Core Win32 keyboard/text plumbing, UI keyboard routing, and `TextBox`.
10. Required higher-level controls: `NumberBox`, tabs, segmented control, and `ColorPicker`.
11. Quick-start UI sample, stress/integration UI sample, docs, package validation, and manual visual pass.
