namespace ModernOverlay.Tests;

[TestClass]
public sealed class DrawContextTests
{
    [TestMethod]
    public void PopClipWithoutPushThrows()
    {
        var context = new DrawContext();

        Assert.ThrowsExactly<InvalidOperationException>(context.PopClip);
    }

    [TestMethod]
    public void ScopedClipPopsOnDispose()
    {
        var context = new DrawContext();

        using (context.Clip(new RectF(0, 0, 10, 10)))
        {
        }

        Assert.ThrowsExactly<InvalidOperationException>(context.PopClip);
    }

    [TestMethod]
    public void DisposedBrushCannotBeUsed()
    {
        var resources = new OverlayResourceManager();
        SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);
        brush.Dispose();
        var context = new DrawContext();

        Assert.ThrowsExactly<ObjectDisposedException>(() => context.Draw.Line(new PointF(0, 0), new PointF(1, 1), brush));
    }
}
