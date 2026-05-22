namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayWindowOptionsTests
{
    [TestMethod]
    public void DefaultsMatchSpec()
    {
        var options = new OverlayWindowOptions();

        Assert.AreEqual(OverlayInputMode.ClickThrough, options.InputMode);
        Assert.AreEqual(OverlayZOrder.TopMost, options.ZOrder);
        Assert.AreEqual(DpiMode.PerMonitorV2, options.DpiMode);
        Assert.AreEqual(TransparencyMode.Auto, options.TransparencyMode);
        Assert.AreEqual(HiddenRenderPolicy.Pause, options.HiddenRenderPolicy);
        Assert.AreEqual(RenderExceptionPolicy.StopOverlay, options.ExceptionPolicy);
    }

    [TestMethod]
    public void FixedFrameRateRejectsZero()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FrameRateLimit.Fixed(0));
    }
}
