# DPI and Multi-Monitor Behavior

ModernOverlay requests Per Monitor V2 DPI awareness by default.

## Coordinate Model

Window bounds use physical pixels:

```csharp
WindowBounds.FromPixels(100, 100, 800, 450)
```

Drawing coordinates use DIPs. `WindowBounds`, `WindowHandle`, and `DpiScale` live in `ModernOverlay.Windows`; drawing rectangles such as `RectF` live in `ModernOverlay.Drawing`:

```csharp
frame.Draw.Rectangle(new RectF(20, 20, 200, 80), brush);
```

Use explicit conversion helpers when moving between the two spaces:

```csharp
WindowBounds pixels = WindowBounds.FromDips(new RectF(10, 10, 400, 240), dpi);
RectF dips = dpi.PixelsToDips(pixels);
```

## Runtime DPI Changes

`WM_DPICHANGED` applies the Windows-suggested bounds, resizes the backend render target, raises `BoundsChanged`, and emits an EventSource DPI diagnostic.

```csharp
overlay.BoundsChanged += (_, args) =>
{
    WindowBounds currentPixels = args.Bounds;
};
```

## Multi-Monitor Notes

Negative coordinates are valid on desktops with monitors positioned left or above the primary display.

When `FrameRateLimit.DisplayDefault` is used, the run loop re-resolves the overlay HWND's current monitor refresh rate while the overlay is running. Moving the overlay or tracking a target onto a monitor with a different refresh rate updates the target frame interval for subsequent frames. Fixed and unlimited frame-rate modes do not depend on monitor refresh.

Mixed-DPI and negative-coordinate setups require manual release validation because driver, compositor, and monitor layout behavior can vary.
