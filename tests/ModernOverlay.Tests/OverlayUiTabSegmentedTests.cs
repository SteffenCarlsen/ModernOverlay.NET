using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiTabControl = ModernOverlay.UI.TabControl;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiTabSegmentedTests
{
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyLeft = 0x25;
    private const int VirtualKeyRight = 0x27;
    private const int VirtualKeyEnd = 0x23;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabPointerSelectionSkipsDisabledTabsAndArrangesActiveContent()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out ProbeElement first, out ProbeElement second, out ProbeElement third);
        tabs.Items[1].IsEnabled = false;
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());

        Click(overlay, 70, 20);
        Assert.AreEqual(0, tabs.SelectedIndex);
        CollectionAssert.Contains(tabs.Children.ToArray(), first);
        CollectionAssert.DoesNotContain(tabs.Children.ToArray(), second);

        Click(overlay, 125, 20);
        ui.Render(new DrawContext());

        Assert.AreEqual(2, tabs.SelectedIndex);
        CollectionAssert.Contains(tabs.Children.ToArray(), third);
        Assert.AreEqual(10f, third.Bounds.X, 0.001f);
        Assert.AreEqual(40f, third.Bounds.Y, 0.001f);
        Assert.AreEqual(220f, third.Bounds.Width, 0.001f);
        Assert.AreEqual(90f, third.Bounds.Height, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TabKeyboardNavigationSkipsDisabledTabs()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiTabControl tabs = CreateTabs(out _, out _, out _);
        tabs.Items[1].IsEnabled = false;
        int changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(tabs);
        ui.Render(new DrawContext());
        tabs.Focus();

        DispatchKey(overlay, VirtualKeyRight);
        Assert.AreEqual(2, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyLeft);
        Assert.AreEqual(0, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, tabs.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, tabs.SelectedIndex);
        Assert.AreEqual(4, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SegmentedControlPointerAndKeyboardSelection()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        SegmentedControl segmented = new()
        {
            Width = 180f,
        };
        Canvas.SetLeft(segmented, 10f);
        Canvas.SetTop(segmented, 10f);
        segmented.Items.Add("A");
        segmented.Items.Add("B");
        segmented.Items.Add("C");
        int changes = 0;
        segmented.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(segmented);
        ui.Render(new DrawContext());

        Click(overlay, 80, 20);
        Assert.AreEqual(1, segmented.SelectedIndex);

        segmented.Focus();
        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, segmented.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, segmented.SelectedIndex);

        DispatchKey(overlay, VirtualKeyLeft);
        Assert.AreEqual(2, segmented.SelectedIndex);
        Assert.AreEqual(4, changes);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiTabControl CreateTabs(out ProbeElement first, out ProbeElement second, out ProbeElement third)
    {
        UiTabControl tabs = new()
        {
            Width = 220f,
            Height = 120f,
            MinWidth = 0f,
            MinHeight = 0f,
        };
        Canvas.SetLeft(tabs, 10f);
        Canvas.SetTop(tabs, 10f);
        first = new ProbeElement();
        second = new ProbeElement();
        third = new ProbeElement();
        tabs.Add("One", first);
        tabs.Add("Two", second);
        tabs.Add("Three", third);
        return tabs;
    }

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
