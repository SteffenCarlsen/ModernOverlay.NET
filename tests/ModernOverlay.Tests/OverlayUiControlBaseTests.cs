using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiControlBaseTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task VisualStateFollowsFocusHoverPressCaptureAndDisabledPriority()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement element = CreateInputElement();
        ui.Root.Children.Add(element);
        ui.Render(new DrawContext());

        Assert.AreEqual(UiVisualState.Normal, element.VisualState);

        element.Focus();
        Assert.AreEqual(UiVisualState.Focused, element.VisualState);

        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 20, 20);
        Assert.AreEqual(UiVisualState.Hover, element.VisualState);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        Assert.AreEqual(UiVisualState.Pressed, element.VisualState);

        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);
        element.CapturePointer();
        Assert.AreEqual(UiVisualState.Pressed, element.VisualState);

        element.IsEnabled = false;
        Assert.AreEqual(UiVisualState.Disabled, element.VisualState);
        Assert.IsFalse(element.IsPointerCaptured);
        Assert.IsNull(ui.FocusedElement);
    }

    [TestMethod]
    public void DisabledStateIsInheritedFromParent()
    {
        Canvas parent = new()
        {
            IsEnabled = false,
        };
        ProbeElement child = CreateInputElement();
        parent.Children.Add(child);

        Assert.IsFalse(child.IsEffectivelyEnabled);
        Assert.AreEqual(UiVisualState.Disabled, child.VisualState);

        parent.IsEnabled = true;

        Assert.IsTrue(child.IsEffectivelyEnabled);
        Assert.AreEqual(UiVisualState.Normal, child.VisualState);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CommandDisabledButtonKeepsDisabledActivationStateUntilCanExecuteChanges()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        bool canExecute = false;
        int clicks = 0;
        UiCommand command = new(_ => clicks++, _ => canExecute);
        UiButton button = new()
        {
            Text = "Apply",
            Width = 80f,
            Height = 30f,
            Command = command,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        Assert.IsFalse(button.IsPointerCaptured);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);
        Assert.AreEqual(0, clicks);

        canExecute = true;
        command.RaiseCanExecuteChanged();
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        Assert.IsTrue(button.IsPointerCaptured);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);
        Assert.AreEqual(1, clicks);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 240, 160),
        });

    private static ProbeElement CreateInputElement()
    {
        ProbeElement element = new()
        {
            Width = 60f,
            Height = 30f,
            Focusable = true,
            ReceivesInput = true,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(element, 10f);
        Canvas.SetTop(element, 10f);
        return element;
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
