using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiFocusKeyboardTests
{
    private const int VirtualKeyA = 0x41;
    private const int VirtualKeyTab = 0x09;

    private static readonly string[] ExpectedKeyboardRoute =
    [
        "child-down:Direct:True:Control",
        "parent-down:Bubble:True:Control",
        "child-up:Direct",
    ];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FocusSetAndClearUpdateFocusedElementAndAncestorState()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Canvas parent = new()
        {
            Width = 120f,
            Height = 80f,
        };
        ProbeElement child = CreateFocusableElement(10f, 10f, 40f, 20f);
        parent.Children.Add(child);
        ui.Root.Children.Add(parent);

        child.Focus();

        Assert.AreSame(child, ui.FocusedElement);
        Assert.IsTrue(child.IsFocused);
        Assert.IsTrue(child.IsKeyboardFocusWithin);
        Assert.IsTrue(parent.IsKeyboardFocusWithin);

        child.Blur();

        Assert.IsNull(ui.FocusedElement);
        Assert.IsFalse(child.IsFocused);
        Assert.IsFalse(parent.IsKeyboardFocusWithin);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabNavigationUsesTabIndexTreeOrderAndSkipsDisabledElements()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton first = CreateButton("First", tabIndex: 20);
        UiButton second = CreateButton("Second", tabIndex: 10);
        UiButton disabled = CreateButton("Disabled", tabIndex: 15);
        disabled.IsEnabled = false;
        ui.Root.Children.Add(first);
        ui.Root.Children.Add(disabled);
        ui.Root.Children.Add(second);

        DispatchKey(overlay, VirtualKeyTab, pressed: true);
        Assert.AreSame(second, ui.FocusedElement);

        DispatchKey(overlay, VirtualKeyTab, pressed: true);
        Assert.AreSame(first, ui.FocusedElement);

        DispatchKey(overlay, VirtualKeyTab, pressed: true);
        Assert.AreSame(second, ui.FocusedElement);

        DispatchKey(overlay, VirtualKeyTab, pressed: true, modifiers: Win32ModifierKeys.Shift);
        Assert.AreSame(first, ui.FocusedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FocusClearsWhenFocusedElementIsDisabledHiddenOrRemoved()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement disabled = CreateFocusableElement(10f, 10f, 40f, 20f);
        ProbeElement hidden = CreateFocusableElement(60f, 10f, 40f, 20f);
        ProbeElement removed = CreateFocusableElement(110f, 10f, 40f, 20f);
        ui.Root.Children.Add(disabled);
        ui.Root.Children.Add(hidden);
        ui.Root.Children.Add(removed);

        disabled.Focus();
        disabled.IsEnabled = false;
        Assert.IsNull(ui.FocusedElement);

        hidden.Focus();
        hidden.Visibility = UiVisibility.Hidden;
        Assert.IsNull(ui.FocusedElement);

        removed.Focus();
        ui.Root.Children.Remove(removed);
        Assert.IsNull(ui.FocusedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardEventsRouteFromFocusedElementAndRespectHandled()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Canvas parent = new()
        {
            Width = 120f,
            Height = 80f,
        };
        ProbeElement child = CreateFocusableElement(10f, 10f, 40f, 20f);
        List<string> route = [];
        child.KeyPressed += (_, args) =>
        {
            Assert.AreSame(child, args.OriginalSource);
            route.Add($"child-down:{args.RoutePhase}:{args.IsRepeat}:{args.Modifiers}");
        };
        parent.KeyPressed += (_, args) =>
        {
            Assert.AreSame(child, args.OriginalSource);
            Assert.AreSame(parent, args.Source);
            route.Add($"parent-down:{args.RoutePhase}:{args.IsRepeat}:{args.Modifiers}");
        };
        child.KeyReleased += (_, args) =>
        {
            route.Add($"child-up:{args.RoutePhase}");
            args.Handled = true;
        };
        parent.KeyReleased += (_, _) => route.Add("parent-up");
        parent.Children.Add(child);
        ui.Root.Children.Add(parent);

        child.Focus();
        DispatchKey(overlay, VirtualKeyA, pressed: true, repeatCount: 2, wasDown: true, modifiers: Win32ModifierKeys.Control);
        DispatchKey(overlay, VirtualKeyA, pressed: false);

        CollectionAssert.AreEqual(ExpectedKeyboardRoute, route);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 240, 160),
        });

    private static ProbeElement CreateFocusableElement(float x, float y, float width, float height)
    {
        ProbeElement element = new()
        {
            Width = width,
            Height = height,
            Focusable = true,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }

    private static UiButton CreateButton(string text, int tabIndex)
        => new()
        {
            Text = text,
            Width = 80f,
            Height = 30f,
            TabIndex = tabIndex,
        };

    private static void DispatchKey(
        OverlayWindow overlay,
        int virtualKey,
        bool pressed,
        int repeatCount = 1,
        bool wasDown = false,
        Win32ModifierKeys modifiers = Win32ModifierKeys.None)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, pressed, false, repeatCount, 0, false, wasDown, !pressed, modifiers)]);
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
