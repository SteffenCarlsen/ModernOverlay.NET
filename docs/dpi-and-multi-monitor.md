# DPI and Multi-Monitor Behavior

ModernOverlay requests Per Monitor V2 DPI awareness by default.

## Coordinate Model

Window bounds use physical pixels:

```csharp
WindowBounds.FromPixels(100, 100, 800, 450)
```

Drawing coordinates use DIPs:

```csharp
frame.Draw.Rectangle(brush, new RectF(20, 20, 200, 80));
```

Use explicit conversion helpers when moving between the two spaces:

```csharp
WindowBounds pixels = WindowBounds.FromDips(new RectF(10, 10, 400, 240), dpi);
RectF dips = dpi.PixelsToDips(pixels);
```

## Runtime DPI Changes

`WM_DPICHANGED` applies the Windows-suggested bounds, resizes the backend render target, raises `BoundsChanged`, and emits an EventSource DPI diagnostic.

## Multi-Monitor Notes

Negative coordinates are valid on desktops with monitors positioned left or above the primary display.

Mixed-DPI and negative-coordinate setups require manual release validation because driver, compositor, and monitor layout behavior can vary.

