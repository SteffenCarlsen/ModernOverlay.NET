# Interactive UI

`ModernOverlay.UI` is the retained interactive UI layer for ModernOverlay 1.1. It is optional and separate from immediate-mode drawing: an app can keep drawing directly with `DrawContext`, attach a UI root, or mix both in the same render callback.

The UI package is generic overlay infrastructure. It does not provide app-specific panels, process-memory behavior, input synthesis, hooks, injection, protected-process bypass, anti-cheat bypass, or kernel integration.

## Package Shape

Add a reference to `ModernOverlay.UI` when you want retained controls, layout, popups, focus, and selective click-through input regions. The dependency direction is one-way:

- `ModernOverlay.UI` uses `ModernOverlay` for windows, input, drawing, resources, DPI, and target bounds.
- `ModernOverlay.UI` uses `ModernOverlay.Diagnostics` for UI diagnostics.
- The core `ModernOverlay` package does not depend on `ModernOverlay.UI`.

The UI root is disposable and should be attached explicitly:

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    InputMode = OverlayInputMode.SelectiveClickThrough,
    NoActivate = false,
    Bounds = new WindowBounds(100, 100, 640, 420),
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay);
```

Rendering is explicit. Call `ui.Render(frame)` from the overlay render callback, in the order you want the retained UI to appear relative to immediate-mode drawing:

```csharp
overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    ui.Render(frame);
};
```

## Minimal Window

The root content is a `Canvas`. Add windows, panels, popups, and controls through `ui.Root.Children.Add(...)` or the `ui.Root.Add(...)` convenience helper.

```csharp
var panel = new StackPanel
{
    Spacing = 8,
    Padding = new Thickness(12),
};

panel.Children.Add(new TextBlock { Text = "Status" });
panel.Children.Add(new Button { Text = "Apply" });
panel.Children.Add(new TextBox { Placeholder = "Type here" });

ui.Root.Children.Add(new UiWindow
{
    Title = "Controls",
    Width = 320,
    Height = 220,
    Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(16)),
    Content = panel,
});
```

## Selective Click-Through

Use `OverlayInputMode.SelectiveClickThrough` when non-UI parts of the overlay should pass pointer input through to whatever is behind the overlay while UI controls remain interactive.

`OverlayUi.Attach(...)` registers the UI root as an input-region resolver by default. It does not silently change the overlay input mode. The overlay must opt in through `OverlayWindowOptions.InputMode`.

Input region resolution follows these rules:

- Coordinates are overlay-local DIPs.
- Open popups are checked before normal root content.
- Visible, enabled elements can participate in input.
- Display-only elements such as `TextBlock` are pass-through unless `ReceivesInput` is enabled or a control opts in by default.
- `UiElement.InputRegion` can narrow or reshape the interactive area for an element.
- Hidden and collapsed elements are skipped.
- Disabled elements are skipped.

This behavior is intended for ordinary UI controls, not for bypassing other applications or protected input boundaries.

## Layout And Placement

UI layout uses DIPs throughout the retained tree. The root measures and arranges before rendering when layout is invalid.

Available panels:

- `Canvas`: absolute placement with `Canvas.SetLeft`, `SetTop`, `SetRight`, and `SetBottom`.
- `StackPanel`: vertical or horizontal stacking with spacing.
- `DockPanel`: docked children with fill-last-child behavior.
- `Grid`: fixed, auto, and star rows/columns with row and column spans.
- `WrapPanel`: horizontal wrapping.

Common element layout properties include `Margin`, `Padding`, `Width`, `Height`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight`, `HorizontalAlignment`, `VerticalAlignment`, `Visibility`, and `ZIndex`.

`UiWindow.Placement` supports:

- `UiPlacement.Manual(...)`;
- `UiPlacement.AnchorTo(...)`;
- `UiPlacement.TargetAnchor(...)`;
- `UiPlacement.Cursor(...)`;
- `UiPlacement.Persisted(...)`.

Placement persistence is interface-only in the core UI package:

```csharp
public interface IUiLayoutStore
{
    bool TryLoad(string key, out UiPlacement placement);
    void Save(string key, UiPlacement placement);
}
```

The core package intentionally does not ship a built-in JSON or file-backed layout store in 1.1. Samples can use in-memory stores. Durable storage should live in app code or a later out-of-core helper after the interface settles.

## Property Changes

Controls use plain CLR properties plus internal invalidation categories:

- layout changes invalidate measure and arrange;
- arrange-only changes invalidate arrange and render;
- visual changes invalidate render;
- input-region changes invalidate input resolution;
- focus/state changes update focus, capture, and visual state;
- resource changes invalidate resource-dependent render or measurement.

Tree mutations requested during protected phases are deferred. This includes measure, arrange, render, routed event dispatch, focus changes, popup dismissal, and capture release. Deferred operations run FIFO when the root returns to an idle safe point.

`OverlayUiRoot.Defer(...)` is a same-thread safe-point API. It is not a cross-thread dispatcher.

## Controls

The 1.1 MVP includes these retained controls:

- Display: `TextBlock`, `Label`, `Image`.
- Buttons: `Button`, `ToggleButton`, `CheckBox`, `RadioButton`.
- Range/numeric: `ProgressBar`, `Slider`, `NumberBox`.
- Text input: `TextBox`.
- Selection/popup: `ListBox`, `ComboBox`, `Menu`, `ContextMenu`, `ToolTip`.
- Windows/containers: `UiWindow`, `GroupBox`.
- Navigation and color: `TabControl`, `SegmentedControl`, `ColorPicker`.

Most controls expose direct properties and explicit changed events. Full data binding is out of scope for 1.1.

Compact examples:

```csharp
var text = new TextBlock { Text = "Status", TextWrapping = UiTextWrapping.Wrap };
var label = new Label { Text = "Name", Target = nameTextBox };
var image = new Image { Source = imageHandle, Width = 48, Height = 48, ImageOpacity = 0.8f };
```

```csharp
var button = new Button { Text = "Apply" };
var toggle = new ToggleButton { Text = "Pin", IsThreeState = true };
var checkBox = new CheckBox { Text = "Enabled", IsChecked = true };
var radio = new RadioButton { Text = "Option A", GroupName = "mode" };
```

```csharp
var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 35 };
var slider = new Slider { Minimum = 0, Maximum = 100, Value = 35, LargeChange = 10 };
var number = new NumberBox { Minimum = 0, Maximum = 100, Step = 5, Value = 50 };
```

```csharp
var textBox = new TextBox
{
    Placeholder = "Type here",
    MaxLength = 120,
};

var multiline = new TextBox
{
    Mode = TextBoxMode.MultiLine,
    Text = "First line\nSecond line",
    Height = 96,
    MaxLines = 4,
};
```

```csharp
var list = new ListBox { Height = 96 };
list.Items.Add("First");
list.Items.Add("Second");

var combo = new ComboBox { Placeholder = "Choose" };
combo.Items.Add("Compact");
combo.Items.Add("Detailed");
```

```csharp
var menu = new Menu();
menu.Items.Add(new UiMenuItem("Apply", UiCommand.FromAction(Apply)));

var contextMenu = new ContextMenu { Owner = button };
contextMenu.Items.Add(new UiMenuItem("Reset", UiCommand.FromAction(Reset)));

var tooltip = new ToolTip { Owner = button, Text = "Apply changes" };
```

```csharp
var group = new GroupBox { Header = "Options", Content = new StackPanel() };

var tabs = new TabControl();
tabs.Add("General", new TextBlock { Text = "General settings" });
tabs.Add("Advanced", new TextBlock { Text = "Advanced settings" });

var segmented = new SegmentedControl();
segmented.Items.Add("Inspect");
segmented.Items.Add("Edit");

var colorPicker = new ColorPicker { Label = "Indicator color" };
```

Commands use `UiCommand`:

```csharp
Button save = new()
{
    Text = "Save",
    Command = UiCommand.FromAction(() => SaveSettings()),
};
```

For commands that need state, use `CanExecute`, `CommandParameter`, and `RaiseCanExecuteChanged()` so controls can refresh disabled visuals.

## Popups

`Popup`, `ComboBox`, `ContextMenu`, `Menu`, and `ToolTip` use the popup layer. Open popups are resolved before normal content for input-region and pointer routing.

Popup policy in 1.1:

- Opening a popup preserves owner focus by default.
- Focus can move into explicitly focusable popup children.
- Pointer capture is owned by the root, not by a separate popup root.
- Owner close, hide, disable, detach, or root disposal closes owned popups.
- Outside pointer and Escape dismissal run before the next normal dispatch.
- Arbitrary nested popups are not a general-purpose 1.1 feature; menu/context-menu scenarios are the supported nested shape.

## Focus And Text Input

The UI root manages one focused element and one captured pointer element. `Tab` and `Shift+Tab` move focus through focusable controls by `TabIndex` and tree order. Controls render visible focus states.

Focused text controls receive text through the overlay text-input event path. `TextBox` supports text entry, caret movement, selection, deletion, read-only mode, placeholder text, max length, password display, horizontal scrolling for long single-line text, and multiline editing through `TextBoxMode.MultiLine`. Multiline mode defaults to wrapping and Enter-to-newline behavior, supports Up/Down and line-local Home/End navigation, and scrolls internally enough to keep the caret visible when content exceeds the arranged height.

Set `OverlayWindowOptions.NoActivate = false` for overlays that need real keyboard focus and typed text. The default remains `true` so passive overlays can be shown without stealing focus.

Text input limits in 1.1:

- `WM_CHAR` and key routing are the supported path.
- `WM_IME_*` composition is out of scope.
- Clipboard copy, cut, and paste are deferred.
- Dead keys work only when Windows delivers composed character messages.
- .NET strings can store delivered Unicode text, but caret movement and deletion are UTF-16 code-unit based rather than grapheme-cluster based.
- Locale-sensitive word navigation and rich text editing are out of scope.
- Caret blinking is owned by the UI root scheduler.

When a text input is focused, it is expected to own keyboard/text message input for the overlay editing session.

## Theme And Resources

`UiTheme` describes the built-in theme colors and font settings. `OverlayUiRoot.ApplyTheme(...)` swaps theme resources and invalidates layout/render/resource state.

Theme resources are realized through `OverlayResourceManager` outside render callbacks. Native backend realizations continue to follow the existing resource-generation and device-recreation model.

Element-level style overrides can use caller-owned brush handles. If a disposed override is encountered during layout or render, the UI logs a `UiResourceRealizationFailure` diagnostic and falls back to the root theme resource.

Use `UiTheme.CheckReadability()` to inspect built-in contrast checks for key foreground/background pairs:

```csharp
UiThemeReadabilityReport report = UiTheme.Default.CheckReadability();
if (!report.IsReadable)
{
    foreach (UiThemeContrastCheck failure in report.Failures)
    {
        Console.WriteLine($"{failure.Name}: {failure.ContrastRatio:0.00}");
    }
}
```

General subtree opacity is not implemented in 1.1 because the drawing layer has no cheap global opacity stack. `UiElement.Opacity == 0` skips render and input participation. `Image.ImageOpacity` covers image-specific alpha.

## Accessibility Baseline

The 1.1 UI layer provides a practical usability baseline, not full platform accessibility parity:

- keyboard navigation for focusable controls;
- visible focus states;
- disabled state visuals;
- default theme readability checks;
- documented text input and UI Automation limitations.

Recommended hit-target guidance for app-authored controls is at least 24 by 24 DIPs for compact overlays and closer to 32 by 32 DIPs for frequently used pointer targets. Avoid relying on color alone for important state.

UI Automation providers, screen-reader tree exposure, accessibility patterns, and high-level semantic roles are out of scope for 1.1.

## Deferred Features

The following are intentionally deferred until after the first retained UI MVP:

- `ScrollViewer`;
- general virtualization;
- full transformed coordinate handling for scrolled content;
- platform UI Automation providers;
- IME composition;
- clipboard editing;
- grapheme-aware text editing;
- production-level benchmark validation.

Before adding `ScrollViewer`, the design needs explicit transformed-coordinate handling for layout, render clipping, hit testing, wheel routing, scrollbar ownership, and popup placement from scrolled content.

## Samples

Use `samples/InteractiveUiOverlay` for the retained UI sample. It demonstrates selective click-through, explicit `ui.Render(frame)`, floating windows, popups, menus, context menus, text input, tab navigation, runtime theme changes, live metrics, bounds/DPI movement, and in-memory `IUiLayoutStore` persistence.

Run it with:

```powershell
tools\Start-ModernOverlaySample.ps1 InteractiveUiOverlay
```
