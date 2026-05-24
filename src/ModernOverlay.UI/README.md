# ModernOverlay.UI

`ModernOverlay.UI` is the retained interactive UI layer for ModernOverlay 1.1. It builds on the core overlay window, drawing, input, target tracking, resource, and diagnostics packages instead of replacing the immediate-mode drawing API.

## Current Scope

- Retained UI root attached with `OverlayUi.Attach(...)`.
- Explicit rendering through `ui.Render(frame)`.
- Selective click-through input regions through `OverlayInputMode.SelectiveClickThrough`.
- DIPs for layout and placement.
- `Canvas`, `StackPanel`, `DockPanel`, `Grid`, and `WrapPanel`.
- `UiWindow` with drag, resize, close, minimize, placement, and `IUiLayoutStore` persistence hooks.
- Text, image, button, toggle, checkbox, radio, progress, slider, number, text box, list, combo, menu, context menu, tooltip, tabs, segmented control, group box, and color picker controls.
- Built-in theme resources with direct style overrides.

## Quick Shape

```csharp
await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    InputMode = OverlayInputMode.SelectiveClickThrough,
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay);

ui.Root.Children.Add(new UiWindow
{
    Title = "Overlay Controls",
    Width = 320,
    Height = 220,
    Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(16)),
    Content = new StackPanel
    {
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = "Status" },
            new Button { Text = "Apply" },
            new TextBox { Placeholder = "Type here" },
        },
    },
});

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    ui.Render(frame);
};
```

## Known Preview Limits

`TextBox` uses the Windows character-message path for text input. IME composition, clipboard editing, full grapheme-aware editing, `ScrollViewer`, virtualization, and UI Automation provider support are not part of the first 1.1 UI package.
