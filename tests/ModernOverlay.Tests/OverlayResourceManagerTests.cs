namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayResourceManagerTests
{
    private static readonly string[] ExpectedInitialResourceKinds = ["SolidBrush", "Font"];

    [TestMethod]
    public void ResourceManagerTracksLiveResourcesUntilDisposed()
    {
        var resources = new OverlayResourceManager();

        SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);
        FontHandle font = resources.CreateFont(new FontOptions("Segoe UI", 16));

        OverlayResourceLeakReport report = resources.CreateLeakReport();
        Assert.AreEqual(2, report.LiveCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(report.LiveResources[0].AllocationSite));
        Assert.IsTrue(report.LiveResources.All(resource => resource.NativeRealizationCount == 0));
        CollectionAssert.AreEquivalent(
            ExpectedInitialResourceKinds,
            report.LiveResources.Select(resource => resource.Kind).ToArray());

        brush.Dispose();

        report = resources.CreateLeakReport();
        Assert.AreEqual(1, report.LiveCount);
        Assert.AreEqual(font.Id, report.LiveResources[0].Id);

        font.Dispose();
        Assert.AreEqual(0, resources.CreateLeakReport().LiveCount);
    }

    [TestMethod]
    public void ResourceGenerationsReflectCreationGeneration()
    {
        var resources = new OverlayResourceManager();

        SolidBrushHandle first = resources.CreateSolidBrush(ColorRgba.White);
        resources.AdvanceGeneration();
        SolidBrushHandle second = resources.CreateSolidBrush(ColorRgba.Black);

        Assert.AreEqual(1, first.Generation);
        Assert.AreEqual(2, second.Generation);
        Assert.AreEqual(2, resources.CurrentGeneration);
    }

    [TestMethod]
    public void TextLayoutRejectsDisposedFont()
    {
        var resources = new OverlayResourceManager();
        FontHandle font = resources.CreateFont(new FontOptions("Segoe UI", 16));
        font.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => resources.CreateTextLayout("demo", font));
    }

    [TestMethod]
    public void TextLayoutStoresLayoutOptions()
    {
        var resources = new OverlayResourceManager();
        using FontHandle font = resources.CreateFont(new FontOptions("Segoe UI", 16));
        var options = new TextLayoutOptions
        {
            MaxWidth = 120,
            MaxHeight = 40,
            Wrapping = TextWrapping.NoWrap,
            HorizontalAlignment = TextHorizontalAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Center,
            Trimming = TextTrimming.Character,
        };

        using TextLayoutHandle layout = resources.CreateTextLayout("demo", font, options);

        Assert.AreEqual(options, layout.Options);
    }

    [TestMethod]
    public void TextLayoutRejectsInvalidLayoutBounds()
    {
        var resources = new OverlayResourceManager();
        using FontHandle font = resources.CreateFont(new FontOptions("Segoe UI", 16));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => resources.CreateTextLayout("demo", font, new TextLayoutOptions { MaxWidth = 0 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => resources.CreateTextLayout("demo", font, new TextLayoutOptions { MaxHeight = float.PositiveInfinity }));
    }

    [TestMethod]
    public void StrokeStyleRejectsEmptyCustomDashes()
    {
        var resources = new OverlayResourceManager();

        Assert.ThrowsExactly<ArgumentException>(() => resources.CreateStrokeStyle(new StrokeStyleOptions
        {
            DashStyle = StrokeDashStyle.Custom,
        }));
    }

    [TestMethod]
    public void ResourceManagerTracksLinearGradientBrushes()
    {
        var resources = new OverlayResourceManager();

        using LinearGradientBrushHandle brush = resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
            new PointF(0, 0),
            new PointF(100, 0),
            [
                new GradientStop(0f, ColorRgba.Black),
                new GradientStop(1f, ColorRgba.White),
            ]));

        OverlayResourceLeakReport report = resources.CreateLeakReport();
        Assert.AreEqual(1, report.LiveCount);
        Assert.AreEqual("LinearGradientBrush", report.LiveResources[0].Kind);
    }

    [TestMethod]
    public void LinearGradientRejectsInvalidStops()
    {
        var resources = new OverlayResourceManager();

        Assert.ThrowsExactly<ArgumentException>(() => resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
            new PointF(0, 0),
            new PointF(100, 0),
            [new GradientStop(0f, ColorRgba.Black)])));
        Assert.ThrowsExactly<ArgumentException>(() => resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
            new PointF(0, 0),
            new PointF(100, 0),
            [
                new GradientStop(1f, ColorRgba.Black),
                new GradientStop(0f, ColorRgba.White),
            ])));
    }

    [TestMethod]
    public void GeometryBuilderRequiresMoveBeforeLine()
    {
        var builder = new GeometryPathBuilder();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.LineTo(new PointF(1, 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => builder.BezierTo(new PointF(1, 1), new PointF(2, 2), new PointF(3, 3)));
        Assert.ThrowsExactly<InvalidOperationException>(() => builder.QuadraticBezierTo(new PointF(1, 1), new PointF(2, 2)));
        Assert.ThrowsExactly<InvalidOperationException>(() => builder.ArcTo(new PointF(2, 2), new SizeF(4, 4)));
    }

    [TestMethod]
    public void GeometryBuilderRejectsInvalidArcRadius()
    {
        var builder = new GeometryPathBuilder();

        builder.MoveTo(new PointF(0, 0));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ArcTo(new PointF(2, 2), new SizeF(0, 4)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ArcTo(new PointF(2, 2), new SizeF(4, -1)));
    }

    [TestMethod]
    public void ResourceManagerCreatesTrackedGeometry()
    {
        var resources = new OverlayResourceManager();

        using GeometryPath geometry = resources.CreateGeometry(path => path
            .MoveTo(new PointF(0, 0))
            .BezierTo(new PointF(4, 0), new PointF(6, 10), new PointF(10, 0))
            .QuadraticBezierTo(new PointF(12, 12), new PointF(10, 10))
            .ArcTo(new PointF(8, 8), new SizeF(3, 4), 15f, GeometrySweepDirection.CounterClockwise, GeometryArcSize.Large)
            .LineTo(new PointF(10, 10))
            .Close());

        OverlayResourceLeakReport report = resources.CreateLeakReport();
        Assert.AreEqual(1, report.LiveCount);
        Assert.AreEqual("GeometryPath", report.LiveResources[0].Kind);
    }

    [TestMethod]
    public void ResourceManagerTracksByteArrayMemoryAndStreamImages()
    {
        var resources = new OverlayResourceManager();
        byte[] bytes = [1, 2, 3];

        using ImageHandle fromBytes = resources.CreateImage(bytes);
        bytes[0] = 42;
        using ImageHandle fromMemory = resources.CreateImage(new ReadOnlyMemory<byte>(bytes));
        using var stream = new MemoryStream(bytes);
        using ImageHandle fromStream = resources.CreateImage(stream);

        OverlayResourceLeakReport report = resources.CreateLeakReport();
        Assert.AreEqual(3, report.LiveCount);
        Assert.IsTrue(report.LiveResources.All(resource => resource.Kind == "Image"));
    }

    [TestMethod]
    public void ResourceManagerRejectsEmptyImageBytes()
    {
        var resources = new OverlayResourceManager();
        byte[] emptyBytes = [];

        Assert.ThrowsExactly<ArgumentException>(() => resources.CreateImage(emptyBytes));
        Assert.ThrowsExactly<ArgumentException>(() => resources.CreateImage(ReadOnlyMemory<byte>.Empty));
    }
}
