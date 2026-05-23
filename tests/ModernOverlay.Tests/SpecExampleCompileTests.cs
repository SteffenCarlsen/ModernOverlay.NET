namespace ModernOverlay.Tests;

[TestClass]
public sealed class SpecExampleCompileTests
{
    [TestMethod]
    public void MinimalSpecExampleImportsCompile()
    {
        _ = typeof(DrawingNamespace);
        _ = typeof(WindowsNamespace);

        var options = new OverlayWindowOptions
        {
            Title = "Demo Overlay",
            Bounds = WindowBounds.FromPixels(100, 100, 800, 600),
            InputMode = OverlayInputMode.ClickThrough,
            ZOrder = OverlayZOrder.TopMost,
            IsVisible = true,
            FrameRateLimit = FrameRateLimit.Fixed(144),
        };

        Assert.AreEqual("Demo Overlay", options.Title);
        Assert.AreEqual(new WindowBounds(100, 100, 800, 600), options.Bounds);
        Assert.AreEqual(OverlayInputMode.ClickThrough, options.InputMode);
        Assert.AreEqual(OverlayZOrder.TopMost, options.ZOrder);
        Assert.IsTrue(options.IsVisible);
        Assert.AreEqual(144d, options.FrameRateLimit.FramesPerSecond);
    }

    [TestMethod]
    public void SpecNamedWindowHelpersCompile()
    {
        Assert.IsFalse(WindowQuery.IsWindow(default));
        Assert.IsFalse(WindowQuery.IsVisible(default));
        Assert.IsFalse(WindowQuery.TryGetWindowBounds(default, out WindowBounds bounds));
        Assert.AreEqual(default, bounds);

        Assert.IsFalse(WindowQuery.TryGetWindowStyles(default, out WindowStylesSnapshot styles));
        Assert.AreEqual(default, styles);

        Assert.IsFalse(WindowEffects.TryExtendFrameIntoClientArea(default));
        Assert.IsFalse(WindowEffects.TryEnableBlurBehind(default));
        Assert.IsFalse(WindowEffects.TryExcludeFromCapture(default));
        Assert.IsFalse(WindowEffects.TryClearDisplayAffinity(default));
    }
}
