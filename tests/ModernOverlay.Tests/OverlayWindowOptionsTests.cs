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
        Assert.AreEqual(TargetMinimizedPolicy.HideOverlay, options.TargetMinimizedPolicy);
        Assert.AreEqual(TimeSpan.FromMilliseconds(33), options.TargetTrackingInterval);
        Assert.AreEqual(RenderExceptionPolicy.StopOverlay, options.ExceptionPolicy);
        Assert.IsFalse(options.ExcludeFromCapture);
    }

    [TestMethod]
    public void FixedFrameRateRejectsZero()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FrameRateLimit.Fixed(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FrameRateLimit.Fixed(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FrameRateLimit.Fixed(double.PositiveInfinity));
    }

    [TestMethod]
    public void PublicFacadeAvoidsCompatibilityAliases()
    {
        Assert.AreEqual("ModernOverlay.Windows", typeof(OverlayWindowOptions).Namespace);
        Assert.AreEqual("ModernOverlay.Windows", typeof(OverlayInputMode).Namespace);
        Assert.AreEqual("ModernOverlay.Windows", typeof(WindowTarget).Namespace);
        CollectionAssert.DoesNotContain(Enum.GetNames<RenderExceptionPolicy>(), "IgnoreAndContinue");
        Assert.IsNull(typeof(WindowTarget).GetMethod("ByWindowTitle"));
        Assert.IsNull(typeof(WindowTarget).GetMethod("Foreground", Type.EmptyTypes));
    }

    [TestMethod]
    public void DisplayDefaultFrameRateUsesResolvedRefreshRate()
    {
        TimeSpan interval = FrameRateLimit.DisplayDefault.ToFrameInterval(144);

        Assert.AreEqual(1d / 144d, interval.TotalSeconds, 0.000_001d);
    }

    [TestMethod]
    public void FixedAndUnlimitedFrameRatesDoNotDependOnDisplayDefault()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1d / 119.88d), FrameRateLimit.Fixed(119.88).ToFrameInterval());
        Assert.AreEqual(TimeSpan.Zero, FrameRateLimit.Unlimited.ToFrameInterval());
    }

    [TestMethod]
    public void WindowBoundsExposeExplicitPixelAndDipFactories()
    {
        var dpi = new DpiScale(1.5f, 2f);

        WindowBounds pixels = WindowBounds.FromPixels(-20, 30, 800, 450);
        WindowBounds fromDips = WindowBounds.FromDips(new RectF(-10, 15, 100, 50), dpi);
        RectF backToDips = dpi.PixelsToDips(fromDips);

        Assert.AreEqual(new WindowBounds(-20, 30, 800, 450), pixels);
        Assert.AreEqual(new WindowBounds(-15, 30, 150, 100), fromDips);
        Assert.AreEqual(new RectF(-10, 15, 100, 50), backToDips);
    }

    [TestMethod]
    public void KeyGestureHelpersCreateVirtualKeyGestures()
    {
        Assert.AreEqual(new KeyGesture('O', HotkeyModifiers.Control | HotkeyModifiers.Alt), KeyGesture.CtrlAltO);
        Assert.AreEqual(new KeyGesture(0x7B, HotkeyModifiers.Shift), KeyGesture.FunctionKey(12, HotkeyModifiers.Shift));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KeyGesture.FromKey('1', HotkeyModifiers.Control));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KeyGesture.FunctionKey(25, HotkeyModifiers.Control));
    }

    [TestMethod]
    public void TargetTrackingIntervalCanBeConfiguredOnTarget()
    {
        TimeSpan interval = TimeSpan.FromMilliseconds(42);
        OverlayTarget target = WindowTarget.ByTitle("demo").WithTrackingInterval(interval);

        Assert.AreEqual(interval, target.TrackingInterval);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => target.WithTrackingInterval(TimeSpan.FromMilliseconds(-1)));
    }

    [TestMethod]
    public async Task TargetTrackingIntervalRejectsNegative()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            TargetTrackingInterval = TimeSpan.FromMilliseconds(-1),
        }));
    }
}
