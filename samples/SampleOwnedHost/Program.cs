using ModernOverlay.Integration;

const string pipeName = "ModernOverlay.IpcOverlayDemo";
const string commandToken = "modern-overlay-local-demo";
byte[] markerPng =
[
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
    0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D, 0x00, 0x00,
    0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
];

var client = new NamedPipeOverlayCommandClient(pipeName, commandToken: commandToken);
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

OverlayGeometryCommand[] curve =
[
    OverlayGeometryCommand.MoveTo(new PointF(650, 196)),
    OverlayGeometryCommand.BezierTo(new PointF(680, 156), new PointF(720, 236), new PointF(748, 196)),
    OverlayGeometryCommand.LineTo(new PointF(650, 196)),
    OverlayGeometryCommand.Close(),
];

OverlayCommandMessage update = OverlayCommandMessage.Update(
    [
        OverlayDrawCommand.Clear(ColorRgba.Transparent),
        OverlayDrawCommand.TextRun("Command update from owned sample host", new PointF(24, 24), ColorRgba.White)
            .WithBrushResource("text-brush")
            .WithFontResource("text-font"),
        OverlayDrawCommand.FilledRectangle(new RectF(24, 72, 260, 80), ColorRgba.White)
            .WithBrushResource("panel-gradient"),
        OverlayDrawCommand.Rectangle(new RectF(24, 72, 260, 80), ColorRgba.White, 2)
            .WithBrushResource("outline-brush"),
        OverlayDrawCommand.Line(new PointF(24, 180), new PointF(340, 180), ColorRgba.White, 3)
            .WithBrushResource("warning-brush"),
        OverlayDrawCommand.CornerBox(new RectF(380, 72, 180, 92), 28, ColorRgba.White, 3)
            .WithBrushResource("outline-brush"),
        OverlayDrawCommand.Crosshair(new PointF(470, 118), 24, ColorRgba.White, 2)
            .WithBrushResource("warning-brush"),
        OverlayDrawCommand.Circle(new PointF(610, 118), 34, ColorRgba.White, 3)
            .WithBrushResource("accent-brush"),
        OverlayDrawCommand.Arrow(new PointF(380, 196), new PointF(620, 196), ColorRgba.White, 3)
            .WithBrushResource("arrow-brush"),
        OverlayDrawCommand.ImageResource("marker-image", new RectF(650, 88, 48, 48), opacity: 0.85f, interpolationMode: ImageInterpolationMode.NearestNeighbor),
        OverlayDrawCommand.GeometryResource("curve-geometry", ColorRgba.White, 3)
            .WithBrushResource("outline-brush"),
    ])
    with
{
    Sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    ResourceDefinitions =
    [
        OverlayResourceDefinition.SolidBrush("text-brush", ColorRgba.White),
        OverlayResourceDefinition.Font("text-font", "Segoe UI", 18),
        OverlayResourceDefinition.LinearGradientBrush(
            "panel-gradient",
            new LinearGradientBrushOptions(
                new PointF(24, 72),
                new PointF(284, 152),
                [
                    new GradientStop(0, ColorRgba.FromBytes(20, 120, 210, 180)),
                    new GradientStop(1, ColorRgba.FromBytes(120, 240, 255, 180)),
                ])),
        OverlayResourceDefinition.SolidBrush("outline-brush", ColorRgba.FromBytes(180, 230, 255)),
        OverlayResourceDefinition.SolidBrush("warning-brush", ColorRgba.FromBytes(255, 220, 120)),
        OverlayResourceDefinition.SolidBrush("accent-brush", ColorRgba.FromBytes(120, 240, 255)),
        OverlayResourceDefinition.SolidBrush("arrow-brush", ColorRgba.FromBytes(255, 150, 120)),
        OverlayResourceDefinition.ImageFromBytes("marker-image", markerPng),
        OverlayResourceDefinition.Geometry("curve-geometry", curve),
    ],
};

try
{
    OverlayCommandResult result = await client.SendAsync(update, cts.Token);
    Console.WriteLine(result.Accepted
        ? $"Overlay command accepted: sequence {result.Sequence}"
        : $"Overlay command rejected: {result.Error}");
}
catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
{
    Console.WriteLine($"Could not reach IPC overlay demo on pipe '{pipeName}': {ex.Message}");
}
