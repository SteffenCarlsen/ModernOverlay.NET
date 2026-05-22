# Drawing Primitives

`DrawContext` is the per-frame immediate-mode drawing surface. Overlay-created contexts are valid only inside the render callback.

## Clear

```csharp
frame.Clear(ColorRgba.Transparent);
```

## Shapes

Supported draw/fill operations include:

- lines;
- rectangles;
- rounded rectangles;
- circles;
- ellipses;
- triangles;
- geometry paths.

Stroke styles support solid, dash, dot, dash-dot, dash-dot-dot, and custom dash patterns.

## Helpers

Helpers include:

- `Draw.Box`;
- `Draw.Crosshair`;
- `Draw.Arrow`.

`Draw.Box` currently maps to rectangle drawing. If corner-only ESP-style boxes are required, that should become an explicit option or separate helper.

## Text

Direct text drawing is available through `Draw.Text`. Reusable `TextLayoutHandle` instances support wrapping, max bounds, alignment, trimming, drawing, and measurement.

## Images

Images can be loaded from path, byte array, read-only memory, or stream. PNG, JPEG/JPG, and BMP are covered through WIC-backed paths. Draw calls support destination rectangles, optional source rectangles, opacity, frame index, and interpolation mode.

## Clip and Transform

Use scoped helpers to guarantee stack cleanup:

```csharp
using (frame.Clip(new RectF(0, 0, 320, 180)))
using (frame.Transform(Matrix3x2F.CreateTranslation(16, 16)))
{
    frame.Draw.Text("Scoped", font, brush, new PointF(0, 0));
}
```

Leaving clip or transform scopes active at frame end raises a clear exception.

