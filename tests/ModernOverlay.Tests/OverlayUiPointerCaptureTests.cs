using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiPointerCaptureTests
{
    private static readonly string[] ExpectedCapturedEvents = ["captured:Moved", "captured:Released", "captured:Wheel"];
    private static readonly string[] ExpectedReleasedEvents = ["other:Pressed"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CapturedElementReceivesPointerEventsOutsideItsBounds()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement captured = CreateInputElement(10f, 10f, 30f, 30f);
        ProbeElement other = CreateInputElement(100f, 10f, 30f, 30f);
        List<string> events = [];
        captured.PointerMoved += (_, args) => events.Add($"captured:{args.Kind}");
        captured.PointerReleased += (_, args) => events.Add($"captured:{args.Kind}");
        captured.PointerWheel += (_, args) => events.Add($"captured:{args.Kind}");
        other.PointerMoved += (_, args) => events.Add($"other:{args.Kind}");
        other.PointerReleased += (_, args) => events.Add($"other:{args.Kind}");
        other.PointerWheel += (_, args) => events.Add($"other:{args.Kind}");
        ui.Root.Children.Add(captured);
        ui.Root.Children.Add(other);
        ui.Render(new DrawContext());

        captured.CapturePointer();
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 115, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 115, 20);
        DispatchPointer(overlay, new Win32PointerEvent(Win32PointerEventKind.Wheel, Win32PointerButton.None, 115, 20, 120));

        CollectionAssert.AreEqual(ExpectedCapturedEvents, events);
        Assert.AreSame(captured, ui.CapturedElement);
        Assert.IsTrue(captured.IsPointerCaptured);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ExplicitReleaseRestoresNormalPointerRouting()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement captured = CreateInputElement(10f, 10f, 30f, 30f);
        ProbeElement other = CreateInputElement(100f, 10f, 30f, 30f);
        List<string> events = [];
        captured.PointerPressed += (_, args) => events.Add($"captured:{args.Kind}");
        other.PointerPressed += (_, args) => events.Add($"other:{args.Kind}");
        ui.Root.Children.Add(captured);
        ui.Root.Children.Add(other);
        ui.Render(new DrawContext());

        captured.CapturePointer();
        captured.ReleasePointerCapture();
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 115, 20);

        CollectionAssert.AreEqual(ExpectedReleasedEvents, events);
        Assert.IsNull(ui.CapturedElement);
        Assert.IsFalse(captured.IsPointerCaptured);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CaptureIsReleasedWhenOwnerIsRemovedDisabledOrHidden()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement removed = CreateInputElement(10f, 10f, 30f, 30f);
        ProbeElement disabled = CreateInputElement(50f, 10f, 30f, 30f);
        ProbeElement hidden = CreateInputElement(90f, 10f, 30f, 30f);
        ui.Root.Children.Add(removed);
        ui.Root.Children.Add(disabled);
        ui.Root.Children.Add(hidden);
        ui.Render(new DrawContext());

        removed.CapturePointer();
        ui.Root.Children.Remove(removed);
        Assert.IsNull(ui.CapturedElement);

        disabled.CapturePointer();
        disabled.IsEnabled = false;
        Assert.IsNull(ui.CapturedElement);

        hidden.CapturePointer();
        hidden.Visibility = UiVisibility.Hidden;
        Assert.IsNull(ui.CapturedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ButtonClickIsSuppressedAfterDragThreshold()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ui.DragThreshold = 4f;
        UiButton button = new()
        {
            Text = "Drag",
            Width = 80f,
            Height = 30f,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        Assert.AreSame(button, ui.CapturedElement);

        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 30, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);

        Assert.AreEqual(0, clicks);
        Assert.IsNull(ui.CapturedElement);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 240, 160),
        });

    private static ProbeElement CreateInputElement(float x, float y, float width, float height)
    {
        ProbeElement element = new()
        {
            Width = width,
            Height = height,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
            ReceivesInput = true,
        };
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
        => DispatchPointer(overlay, new Win32PointerEvent(kind, button, x, y));

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEvent pointer)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [pointer]);
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
