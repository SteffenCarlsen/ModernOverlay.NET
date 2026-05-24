using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiThemeResourceTests
{
    [TestMethod]
    public void DefaultThemeReadabilityChecksPass()
    {
        UiThemeReadabilityReport report = UiTheme.Default.CheckReadability();

        Assert.IsTrue(report.IsReadable, string.Join(Environment.NewLine, report.Failures.Select(failure => $"{failure.Name}: {failure.ContrastRatio:0.00}")));
    }

    [TestMethod]
    public void ThemeReadabilityChecksReportLowContrastFailures()
    {
        UiTheme theme = UiTheme.Default with
        {
            Foreground = ColorRgba.FromBytes(32, 32, 32),
            Surface = ColorRgba.FromBytes(30, 30, 30),
        };

        UiThemeReadabilityReport report = theme.CheckReadability();

        Assert.IsFalse(report.IsReadable);
        Assert.IsTrue(report.Failures.Any(failure => failure.Name == "Foreground on surface"));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DisposedElementStyleOverridesFallBackToThemeResources()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        SolidBrushHandle foreground = overlay.Resources.CreateSolidBrush(ColorRgba.FromBytes(255, 0, 0));
        FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 16f));
        foreground.Dispose();
        font.Dispose();

        ui.Root.Children.Add(new TextBlock
        {
            Text = "Fallback style",
            Width = 160f,
            Height = 32f,
            Foreground = foreground,
            Font = font,
        });

        ui.Render(new DrawContext());

        Assert.AreEqual(1, ui.Metrics.RenderPasses);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ThemeResourcesAreCreatedSwappedAndDisposedWithRootLifetime()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });
        OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        OverlayResourceHandle[] initialHandles = GetThemeHandles(ui);

        Assert.AreEqual(9, overlay.Resources.CreateLeakReport().LiveCount);
        CollectionAssert.AllItemsAreNotNull(initialHandles);
        Assert.IsTrue(initialHandles.All(handle => !handle.IsDisposed));

        ui.ApplyTheme(UiTheme.Default with
        {
            Accent = ColorRgba.FromBytes(220, 80, 40),
        });

        Assert.IsTrue(initialHandles.All(handle => handle.IsDisposed));
        OverlayResourceHandle[] swappedHandles = GetThemeHandles(ui);
        Assert.AreEqual(9, overlay.Resources.CreateLeakReport().LiveCount);
        Assert.IsTrue(swappedHandles.All(handle => !handle.IsDisposed));

        ui.Dispose();

        Assert.IsTrue(swappedHandles.All(handle => handle.IsDisposed));
        Assert.AreEqual(0, overlay.Resources.CreateLeakReport().LiveCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ThemeSwapAfterBackendRecreationUsesCurrentResourceGeneration()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Bounds = new WindowBounds(10, 20, 320, 220),
        });
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        long initialGeneration = overlay.Resources.CurrentGeneration;

        await overlay.RecreateAsync();

        Assert.AreEqual(initialGeneration + 1, overlay.Resources.CurrentGeneration);

        OverlayResourceHandle[]? handles = null;
        long renderPasses = 0;
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        overlay.Render += frame =>
        {
            ui.ApplyTheme(UiTheme.Default with
            {
                Surface = ColorRgba.FromBytes(18, 24, 28),
            });
            ui.Render(frame);
            handles = GetThemeHandles(ui);
            renderPasses = ui.Metrics.RenderPasses;
            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);

        Assert.IsNotNull(handles);
        Assert.IsTrue(handles.All(handle => handle.Generation == overlay.Resources.CurrentGeneration));
        Assert.AreEqual(1, renderPasses);
    }

    private static OverlayResourceHandle[] GetThemeHandles(OverlayUiRoot ui)
        =>
        [
            ui.ThemeResources.Foreground,
            ui.ThemeResources.MutedForeground,
            ui.ThemeResources.Surface,
            ui.ThemeResources.SurfaceHover,
            ui.ThemeResources.SurfacePressed,
            ui.ThemeResources.Border,
            ui.ThemeResources.Accent,
            ui.ThemeResources.Disabled,
            ui.ThemeResources.Font,
        ];
}
