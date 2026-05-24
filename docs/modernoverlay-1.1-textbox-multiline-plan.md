# ModernOverlay 1.1 TextBox Multiline Plan

Date: 2026-05-24

This document analyzes what it takes to add multiline editing support to `ModernOverlay.UI.TextBox` and breaks the work into implementation tasks. The target is one `TextBox` control with explicit multiline options, not a separate `MultiLineTextBox` control.

Implementation status: completed in the 1.1 branch with `TextBoxMode.MultiLine`, multiline line layout, Enter/newline handling, vertical navigation, caret-visible viewport scrolling, focused tests, documentation, and a `UiAbTestOverlay` showcase.

## Verdict

Use one `TextBox` control with multiline behavior enabled by properties.

A separate `MultiLineTextBox` would duplicate the same text state, focus behavior, editing commands, selection model, validation hooks, theme behavior, and future clipboard/IME work. The underlying difference is not a separate control identity; it is the text layout, wrapping, return-key handling, caret navigation, and vertical viewport model.

The sample can still present this as "Multiline TextBox" so the feature is discoverable without creating a second public control type.

Resolved decision: multiline support stays on `TextBox`; no separate `MultiLineTextBox` control is planned for 1.1.

## Current TextBox Baseline

The current `TextBox` implementation is single-line by design:

1. It stores one `horizontalOffset` for horizontal scrolling.
2. It measures one advance table for the full `Text` value.
3. Caret hit testing maps pointer X to a single text index.
4. Selection rendering draws one rectangle on one line.
5. Caret rendering draws one vertical line at one Y coordinate.
6. `OnTextInput` filters all control characters, so newline input is currently dropped.
7. Keyboard navigation supports left, right, home, end, backspace, delete, and Ctrl+A, but not up, down, line-local home/end, or return insertion.

This means multiline support needs a text layout model rather than only an `AcceptsReturn` flag.

## Proposed Public API

Resolved public shape:

```csharp
public enum TextBoxMode
{
    SingleLine,
    MultiLine,
}

public TextBoxMode Mode { get; set; }
public bool AcceptsReturn { get; set; }
public UiTextWrapping TextWrapping { get; set; }
public int MaxLines { get; set; }
public float LineSpacing { get; set; }
```

Resolved defaults and behavior:

1. `Mode = TextBoxMode.SingleLine`.
2. `AcceptsReturn = false` in single-line mode.
3. Multiline mode inserts new lines on Enter by default.
4. Multiline mode defaults to wrapping.
5. Explicit-height multiline boxes scroll internally enough to keep the caret visible.
6. Home/End move to visual line start/end in multiline mode.
7. Ctrl+Home/Ctrl+End move to document start/end in multiline mode.
8. `MaxLines` is a layout/render limit, not an editing limit.
4. `MaxLines = int.MaxValue`.
5. `LineSpacing = 1.35f`.

`Mode = MultiLine` enables vertical layout and vertical viewport behavior. `AcceptsReturn` remains public so callers can override Enter behavior when they want form-level activation instead of newline insertion.

## Internal Architecture

Add an internal line model used by render, caret placement, hit testing, selection, and scrolling:

```csharp
private readonly record struct TextBoxLine(
    int Start,
    int Length,
    int End,
    float Width,
    float Y);
```

The line model should be rebuilt when text, font, wrapping width, line spacing, or mode changes. It should preserve UTF-16 boundary rules already used by the single-line editor so surrogate pairs are not split by caret movement or deletion.

The line model must support:

1. Explicit line breaks from `\n`, normalized from `\r\n` and `\r`.
2. Optional wrapping when multiline wrapping is enabled.
3. Empty visual lines, including trailing newline behavior.
4. Mapping text index to `(line, x, y)`.
5. Mapping pointer position to text index.
6. Selection rectangles across one or more lines.
7. Vertical scrolling or viewport offset when content exceeds the arranged height.

## Editing Behavior

Single-line behavior should remain unchanged.

Multiline behavior should add:

1. Enter inserts `\n` only when `AcceptsReturn` is true and the control is not read-only.
2. Text input permits newline characters only when multiline return insertion is enabled.
3. Up and Down move the caret between visual lines while preserving a preferred X position.
4. Home and End move to the start/end of the current visual line by default in multiline mode.
5. Ctrl+Home and Ctrl+End move to the start/end of the full text.
6. Shift with navigation extends selection using the existing selection anchor behavior.
7. Backspace and Delete continue to honor safe UTF-16 text boundaries and work across line breaks.

Word navigation, clipboard, IME composition, and grapheme-cluster editing remain out of scope for this slice unless the existing 1.1 text scope changes.

## Rendering And Layout

Measure should account for multiline height:

1. Single-line measurement keeps the current compact behavior.
2. Multiline measurement uses line count, line height, padding, `MinHeight`, `MaxHeight`, explicit `Height`, and available width.
3. If `Height` is explicit and content exceeds the viewport, the control clips content and scrolls internally.
4. If `Height` is not explicit, the control may size to content up to available height and `MaxHeight`.

Render should:

1. Draw the same border/background/focus visuals as the current `TextBox`.
2. Clip text, selection, and caret to `ContentBounds`.
3. Draw placeholder text on the first line only when text is empty.
4. Draw selection rectangles per visual line.
5. Draw the caret at the resolved line and x position.
6. Keep horizontal scrolling only for single-line or non-wrapped multiline content.

## Tests

Add focused tests in `OverlayUiTextBoxTests`:

1. Multiline text input preserves line breaks when `Mode = MultiLine` and `AcceptsReturn = true`.
2. Single-line text input still filters line breaks.
3. Enter does not mutate text when multiline return insertion is disabled.
4. Up and Down move between lines and preserve the preferred visual X position.
5. Home and End move within the current line in multiline mode.
6. Ctrl+Home and Ctrl+End move to document boundaries.
7. Drag selection and Shift navigation can select across lines.
8. Backspace/Delete can remove line breaks.
9. Rendering produces multiple text runs, per-line selection rectangles, and a caret on the expected line.
10. Explicit height clips/scrolls the caret into view when editing beyond the visible viewport.

## UiAbTestOverlay Showcase

Update `samples/UiAbTestOverlay` with a visible multiline showcase:

1. Add a labeled multiline text box with an explicit height.
2. Preload it with two or three lines of neutral sample text.
3. Show that Enter can add new lines.
4. Show selection, caret movement, and text changes through existing status text.
5. Keep it in the main controls window only if the window remains usable at common viewport sizes; otherwise put it in a dedicated text-input or editing panel.

## Resolved Design Decisions

1. Use `TextBoxMode Mode`, not `IsMultiline`.
2. Multiline mode inserts new lines on Enter by default.
3. Multiline mode defaults to wrapping.
4. Explicit-height multiline boxes internally scroll enough to keep the caret visible.
5. Home/End follows WinForms-style line navigation, with Ctrl+Home/Ctrl+End for document boundaries.
6. `MaxLines` is a layout/render limit only.
7. The AbTest showcase should live in a separate small text-input/editing window.
