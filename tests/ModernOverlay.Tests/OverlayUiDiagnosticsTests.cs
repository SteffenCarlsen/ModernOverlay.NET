using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiDiagnosticsTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MetricsTrackElementsLayoutRenderInputEventsAndPopups()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton button = new()
        {
            Text = "Metrics",
            Width = 90f,
            Height = 30f,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        Popup popup = new()
        {
            IsOpen = true,
            Width = 120f,
            Height = 60f,
            Placement = new PointF(160f, 20f),
        };
        popup.Children.Add(new TextBlock { Text = "Popup" });
        ui.Root.Children.Add(button);
        ui.Root.Children.Add(popup);

        ui.Render(new DrawContext());
        OverlayUiMetrics afterRender = ui.Metrics;

        Assert.AreEqual(4, afterRender.ElementCount);
        Assert.IsTrue(afterRender.LayoutPasses >= 1);
        Assert.AreEqual(1, afterRender.RenderPasses);
        Assert.AreEqual(1, afterRender.ActivePopupCount);

        _ = ui.ResolveInputRegion(new PointF(20f, 20f));
        Assert.AreEqual(afterRender.InputRegionChecks + 1, ui.Metrics.InputRegionChecks);

        long routedEvents = ui.Metrics.RoutedEvents;
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        Assert.AreEqual(routedEvents + 1, ui.Metrics.RoutedEvents);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }
}
