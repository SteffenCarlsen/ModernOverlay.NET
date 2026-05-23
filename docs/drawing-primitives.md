# Drawing Primitives

`ModernOverlay.Drawing` contains the immediate-mode drawing surface, drawing primitives, geometry structs, and resource handles. `DrawContext` is the per-frame drawing surface. Overlay-created contexts are valid only inside the render callback.

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

Minimal shape frame:

```csharp
overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    frame.Draw.Line(new PointF(16, 16), new PointF(160, 48), brush, strokeWidth: 2);
    frame.Draw.Rectangle(new RectF(24, 64, 180, 72), brush, strokeWidth: 2);
    frame.Fill.Circle(new PointF(260, 96), 24, brush);
};
```

## Helpers

Helpers include:

- `Draw.Box`;
- `Draw.CornerBox`;
- `Draw.Crosshair`;
- `Draw.Arrow`.

`Draw.Box` maps to full rectangle drawing. `Draw.CornerBox` draws eight corner-only line segments with an explicit corner length for GameOverlay-style overlay markers.

```csharp
frame.Draw.CornerBox(new RectF(40, 40, 160, 90), brush, cornerLength: 18, strokeWidth: 2);
frame.Draw.Crosshair(new PointF(320, 180), size: 14, brush, strokeWidth: 2);
frame.Draw.Arrow(new PointF(80, 220), new PointF(180, 180), brush, strokeWidth: 2);
```

## Text

Direct text drawing is available through `Draw.Text`. Reusable `TextLayoutHandle` instances support wrapping, max bounds, alignment, trimming, drawing, and measurement.

```csharp
using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
using SolidBrushHandle brush = overlay.Resources.CreateSolidBrush(ColorRgba.White);

overlay.Render += frame =>
{
    frame.Draw.Text("status: ready", font, brush, new PointF(24, 24));
};
```

## Images

Images can be loaded from path, byte array, read-only memory, or stream. PNG, JPEG/JPG, and BMP are covered through WIC-backed paths. Draw calls support destination rectangles, optional source rectangles, opacity, frame index, and interpolation mode.

```csharp
using ImageHandle image = overlay.Resources.CreateImage("assets/status.png");

overlay.Render += frame =>
{
    frame.Draw.Image(image, new RectF(24, 24, 64, 64), opacity: 0.85f);
};
```

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
