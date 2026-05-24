using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiThemeResourceTests
{
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
}
