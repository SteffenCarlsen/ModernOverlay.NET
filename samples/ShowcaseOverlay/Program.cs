using System.Globalization;
using ModernOverlay;

const int width = 1180;
const int height = 760;

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Showcase",
    Bounds = new WindowBounds(120, 120, width, height),
    InputMode = OverlayInputMode.ClickThrough,
    ZOrder = OverlayZOrder.TopMost,
    TransparencyMode = TransparencyMode.DwmGlassFrame,
    FrameRateLimit = FrameRateLimit.Unlimited,
    PresentMode = PresentMode.Immediate,
});

using SolidBrushHandle background = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(42, 45, 55, 245));
using SolidBrushHandle panel = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(18, 22, 28, 235));
using SolidBrushHandle panelBorder = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(78, 86, 102));
using SolidBrushHandle grid = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(92, 99, 116, 115));
using SolidBrushHandle metricText = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(44, 255, 74));
using SolidBrushHandle white = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(238, 244, 255));
using SolidBrushHandle dim = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(166, 178, 198));
using SolidBrushHandle blue = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(47, 110, 220));
using SolidBrushHandle cyan = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(71, 218, 218));
using SolidBrushHandle sky = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(97, 204, 244));
using SolidBrushHandle green = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(24, 216, 16));
using SolidBrushHandle lime = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(111, 248, 127));
using SolidBrushHandle mint = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(87, 213, 124));
using SolidBrushHandle purple = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(142, 35, 235));
using SolidBrushHandle magenta = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(240, 67, 232));
using SolidBrushHandle lavender = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(178, 139, 226));
using SolidBrushHandle orange = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(214, 126, 64));
using SolidBrushHandle yellow = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(246, 247, 112));
using SolidBrushHandle wine = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(101, 48, 84));
using LinearGradientBrushHandle gradient = overlay.Resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
    new PointF(42, 452),
    new PointF(382, 552),
    [
        new GradientStop(0f, ColorRgba.FromBytes(211, 76, 236)),
        new GradientStop(0.5f, ColorRgba.FromBytes(67, 110, 39)),
        new GradientStop(1f, ColorRgba.FromBytes(86, 213, 124)),
    ]));
using StrokeStyleHandle dash = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.Dash });
using StrokeStyleHandle dot = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.Dot });
using StrokeStyleHandle dashDot = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.DashDot });
using StrokeStyleHandle dashDotDot = overlay.Resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.DashDotDot });
using FontHandle metricFont = overlay.Resources.CreateFont(new FontOptions("Cascadia Mono", 12));
using FontHandle smallFont = overlay.Resources.CreateFont(new FontOptions("Cascadia Mono", 11));
using FontHandle titleFont = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 18));
using GeometryPath wavePath = overlay.Resources.CreateGeometry(builder => builder
    .MoveTo(new PointF(406, 388))
    .BezierTo(new PointF(435, 318), new PointF(492, 456), new PointF(522, 386))
    .BezierTo(new PointF(552, 318), new PointF(608, 456), new PointF(638, 386))
    .LineTo(new PointF(638, 420))
    .BezierTo(new PointF(604, 480), new PointF(438, 480), new PointF(406, 420))
    .Close());

overlay.Render += frame =>
{
    FrameStats stats = overlay.FrameStats;

    frame.Clear(ColorRgba.Transparent);
    frame.Fill.Rectangle(new RectF(0, 0, width, height), background);

    DrawHeader(frame, overlay, stats, titleFont, metricFont, panel, panelBorder, metricText, white, dim);
    DrawGrid(frame, grid, panelBorder);
    DrawShowcase(frame, stats, smallFont, white, dim, blue, cyan, sky, green, lime, mint, purple, magenta, lavender, orange, yellow, wine, gradient, dash, dot, dashDot, dashDotDot, wavePath);
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
await overlay.RunAsync(cts.Token);

static void DrawHeader(
    DrawContext frame,
    OverlayWindow overlay,
    FrameStats stats,
    FontHandle titleFont,
    FontHandle metricFont,
    BrushHandle panel,
    BrushHandle border,
    BrushHandle metric,
    BrushHandle white,
    BrushHandle dim)
{
    frame.Fill.RoundedRectangle(new RectF(20, 14, 1140, 112), 8, 8, panel);
    frame.Draw.RoundedRectangle(new RectF(20, 14, 1140, 112), 8, 8, border, 1.5f);
    frame.Draw.Text("ModernOverlay Showcase", titleFont, white, new PointF(36, 24));
    frame.Draw.Text("Direct2D HWND / Vortice / net11.0-windows preview", metricFont, dim, new PointF(36, 56));
    frame.Draw.Text("Unbounded FPS / immediate present", metricFont, dim, new PointF(36, 88));

    DrawMetric(frame, metricFont, metric, white, "FPS", $"{stats.CurrentFramesPerSecond:0.0} / avg {stats.AverageFramesPerSecond:0.0}", 388, 24);
    DrawMetric(frame, metricFont, metric, white, "Frame", $"{stats.LastFrameDuration.TotalMilliseconds:0.00} ms", 590, 24);
    DrawMetric(frame, metricFont, metric, white, "Render", $"{stats.RenderDuration.TotalMilliseconds:0.00} ms", 780, 24);
    DrawMetric(frame, metricFont, metric, white, "Present", $"{stats.PresentDuration.TotalMilliseconds:0.00} ms", 970, 24);

    DrawMetric(frame, metricFont, metric, white, "Frames", stats.FrameCount.ToString(CultureInfo.InvariantCulture), 388, 56);
    DrawMetric(frame, metricFont, metric, white, "Cmd/Prim", $"{stats.CommandCount}/{stats.PrimitiveCount}", 590, 56);
    DrawMetric(frame, metricFont, metric, white, "Native", stats.NativeResourceCount.ToString(CultureInfo.InvariantCulture), 780, 56);
    DrawMetric(frame, metricFont, metric, white, "DPI", $"{stats.DpiScale.X:0.##}x", 970, 56);

    DrawMetric(frame, metricFont, metric, white, "Target", $"{stats.TargetFrameInterval.TotalMilliseconds:0.00} ms", 388, 88);
    DrawMetric(frame, metricFont, metric, white, "Actual", $"{stats.ActualFrameInterval.TotalMilliseconds:0.00} ms", 590, 88);
    DrawMetric(frame, metricFont, metric, white, "Worst", $"{stats.WorstFrameDuration.TotalMilliseconds:0.00} ms", 780, 88);
    DrawMetric(frame, metricFont, metric, white, "Drop/Skip", $"{stats.DroppedFrameCount}/{stats.SkippedFrameCount}", 970, 88);

    frame.Draw.Text($"Backend {overlay.BackendName} gen {stats.BackendGeneration}", metricFont, dim, new PointF(36, 136));
    frame.Draw.Text($"HWND 0x{overlay.Hwnd.Value:X}", metricFont, dim, new PointF(312, 136));
}

static void DrawMetric(DrawContext frame, FontHandle font, BrushHandle labelBrush, BrushHandle valueBrush, string label, string value, float x, float y)
{
    frame.Draw.Text($"{label}: ", font, labelBrush, new PointF(x, y));
    frame.Draw.Text(value, font, valueBrush, new PointF(x + 82, y));
}

static void DrawGrid(DrawContext frame, BrushHandle grid, BrushHandle border)
{
    var bounds = new RectF(20, 154, 1140, 586);
    frame.Draw.Rectangle(bounds, border, 1.5f);

    for (float x = bounds.X + 20; x < bounds.X + bounds.Width; x += 20)
    {
        frame.Draw.Line(new PointF(x, bounds.Y), new PointF(x, bounds.Y + bounds.Height), grid, 1f);
    }

    for (float y = bounds.Y + 20; y < bounds.Y + bounds.Height; y += 20)
    {
        frame.Draw.Line(new PointF(bounds.X, y), new PointF(bounds.X + bounds.Width, y), grid, 1f);
    }
}

static void DrawShowcase(
    DrawContext frame,
    FrameStats stats,
    FontHandle smallFont,
    BrushHandle white,
    BrushHandle dim,
    BrushHandle blue,
    BrushHandle cyan,
    BrushHandle sky,
    BrushHandle green,
    BrushHandle lime,
    BrushHandle mint,
    BrushHandle purple,
    BrushHandle magenta,
    BrushHandle lavender,
    BrushHandle orange,
    BrushHandle yellow,
    BrushHandle wine,
    BrushHandle gradient,
    StrokeStyleHandle dash,
    StrokeStyleHandle dot,
    StrokeStyleHandle dashDot,
    StrokeStyleHandle dashDotDot,
    GeometryPath wavePath)
{
    frame.Fill.Triangle(new PointF(64, 252), new PointF(116, 150), new PointF(168, 252), blue);
    frame.Draw.Rectangle(new RectF(202, 152, 118, 100), blue, 2f);
    frame.Draw.Circle(new PointF(405, 202), 49, green, dash, 2f);
    frame.Fill.Circle(new PointF(548, 202), 52, green);
    frame.Draw.Rectangle(new RectF(654, 152, 118, 100), sky, dot, 2.5f);
    frame.Draw.Ellipse(new RectF(812, 154, 112, 96), sky, 2.5f);

    frame.Fill.RoundedRectangle(new RectF(42, 276, 124, 96), 9, 9, gradient);
    frame.Fill.Rectangle(new RectF(202, 276, 118, 96), cyan);
    frame.Draw.Triangle(new PointF(404, 276), new PointF(344, 372), new PointF(464, 372), sky, 2.5f);
    frame.Draw.Circle(new PointF(548, 324), 51, magenta, 2.5f);
    frame.Draw.Triangle(new PointF(696, 276), new PointF(636, 372), new PointF(756, 372), cyan, dashDot, 2f);
    frame.Draw.RoundedRectangle(new RectF(804, 276, 122, 96), 9, 9, orange, dash, 2f);

    frame.Draw.Rectangle(new RectF(42, 398, 124, 96), lime, dot, 2f);
    frame.Draw.Rectangle(new RectF(202, 398, 118, 96), purple, dashDot, 2f);
    frame.Draw.Triangle(new PointF(404, 398), new PointF(344, 494), new PointF(464, 494), purple, 2.5f);
    frame.Draw.Geometry(wavePath, orange, dashDotDot, 2f);
    frame.Draw.CornerBox(new RectF(652, 398, 120, 96), lime, cornerLength: 26f, strokeWidth: 2.5f);
    frame.Draw.Triangle(new PointF(850, 398), new PointF(790, 494), new PointF(912, 494), yellow, 2.5f);

    frame.Fill.RoundedRectangle(new RectF(42, 520, 124, 62), 10, 10, magenta);
    frame.Fill.RoundedRectangle(new RectF(202, 520, 118, 62), 10, 10, green);
    frame.Fill.Rectangle(new RectF(344, 520, 120, 62), mint);
    frame.Draw.Ellipse(new RectF(506, 510, 112, 82), lavender, dash, 2f);
    frame.Draw.Crosshair(new PointF(706, 550), 34, sky, 2f);
    frame.Fill.RoundedRectangle(new RectF(804, 520, 122, 62), 8, 8, wine);

    float pulse = 12f + MathF.Abs(MathF.Sin(stats.FrameCount / 18f)) * 12f;
    frame.Draw.Arrow(new PointF(62, 608), new PointF(172, 608), white, 2.5f, headLength: 18f);
    frame.Draw.Text("fills / strokes / dashed / geometry / helpers", smallFont, white, new PointF(204, 600));
    frame.Draw.Text($"live pulse {pulse:0.0}px", smallFont, dim, new PointF(594, 600));
    frame.Draw.Circle(new PointF(748, 608), pulse, yellow, 2f);
}
