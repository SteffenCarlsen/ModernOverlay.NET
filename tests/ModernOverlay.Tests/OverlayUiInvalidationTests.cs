using ModernOverlay.UI;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiInvalidationTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task LayoutAffectingPropertiesTriggerLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement element = CreateElement();
        ui.Root.Children.Add(element);
        ui.Render(new DrawContext());

        AssertLayoutPassAdded(ui, () => element.Width = 80f);
        AssertLayoutPassAdded(ui, () => element.Height = 40f);
        AssertLayoutPassAdded(ui, () => element.Margin = new Thickness(2f));
        AssertLayoutPassAdded(ui, () => element.Padding = new Thickness(3f));
        AssertLayoutPassAdded(ui, () => element.MinWidth = 20f);
        AssertLayoutPassAdded(ui, () => element.MaxHeight = 90f);
        AssertLayoutPassAdded(ui, () => element.HorizontalAlignment = UiHorizontalAlignment.Center);
        AssertLayoutPassAdded(ui, () => element.Visibility = UiVisibility.Hidden);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RenderInputAndStatePropertiesDoNotTriggerLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement element = CreateElement();
        ui.Root.Children.Add(element);
        ui.Render(new DrawContext());

        AssertNoLayoutPassAdded(ui, () => element.Name = "probe");
        AssertNoLayoutPassAdded(ui, () => element.Tag = new object());
        AssertNoLayoutPassAdded(ui, () => element.Opacity = 0.5f);
        AssertNoLayoutPassAdded(ui, () => element.ZIndex = 10);
        AssertNoLayoutPassAdded(ui, () => element.ReceivesInput = false);
        AssertNoLayoutPassAdded(ui, () => element.Focusable = true);
        AssertNoLayoutPassAdded(ui, () => element.TabIndex = 5);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SettingSamePropertyValueDoesNotTriggerLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement element = CreateElement();
        ui.Root.Children.Add(element);
        ui.Render(new DrawContext());

        AssertNoLayoutPassAdded(ui, () => element.Width = element.Width);
        AssertNoLayoutPassAdded(ui, () => element.Height = element.Height);
        AssertNoLayoutPassAdded(ui, () => element.Margin = element.Margin);
        AssertNoLayoutPassAdded(ui, () => element.Visibility = element.Visibility);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InputRegionPropertyChangesAffectHitTestingWithoutLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement element = CreateElement();
        ui.Root.Children.Add(element);
        ui.Render(new DrawContext());
        PointF inside = new(12f, 14f);

        Assert.AreEqual(OverlayInputRegionResult.Interactive, ui.ResolveInputRegion(inside));
        long layoutPasses = ui.Metrics.LayoutPasses;

        element.InputRegion = (_, _) => false;

        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(inside));
        Assert.AreEqual(layoutPasses, ui.Metrics.LayoutPasses);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 200, 120),
        });

    private static ProbeElement CreateElement()
    {
        ProbeElement element = new()
        {
            Width = 40f,
            Height = 20f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
            ReceivesInput = true,
        };
        Canvas.SetLeft(element, 10f);
        Canvas.SetTop(element, 12f);
        return element;
    }

    private static void AssertLayoutPassAdded(OverlayUiRoot ui, Action mutation)
    {
        long before = ui.Metrics.LayoutPasses;

        mutation();
        ui.Render(new DrawContext());

        Assert.IsTrue(ui.Metrics.LayoutPasses > before);
    }

    private static void AssertNoLayoutPassAdded(OverlayUiRoot ui, Action mutation)
    {
        long before = ui.Metrics.LayoutPasses;

        mutation();
        ui.Render(new DrawContext());

        Assert.AreEqual(before, ui.Metrics.LayoutPasses);
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
