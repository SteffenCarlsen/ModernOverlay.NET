using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiListBox = ModernOverlay.UI.ListBox;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiListBoxTests
{
    private const int VirtualKeyEnd = 0x23;
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyDown = 0x28;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerSelectsItemAndRaisesSelectionChanged()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiListBox listBox = CreateListBox("Alpha", "Bravo", "Charlie");
        int changes = 0;
        listBox.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(listBox);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 42);

        Assert.AreEqual(1, listBox.SelectedIndex);
        Assert.AreEqual("Bravo", listBox.SelectedItem);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerBoundaryKeepsPreviousRenderedRow()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiListBox listBox = CreateListBox("Alpha", "Bravo", "Charlie");
        ui.Root.Children.Add(listBox);
        ui.Render(new DrawContext());

        DispatchUiPointer(ui, OverlayPointerEventKind.Pressed, OverlayPointerButton.Left, new PointF(20f, 58f));

        Assert.AreEqual(1, listBox.SelectedIndex);
        Assert.AreEqual("Bravo", listBox.SelectedItem);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerInUnrenderedPartialRowDoesNotSelectHiddenItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiListBox listBox = CreateListBox("Alpha", "Bravo", "Charlie", "Delta");
        listBox.Height = 92f;
        listBox.SelectedIndex = 0;
        ui.Root.Children.Add(listBox);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 90);

        Assert.AreEqual(0, listBox.SelectedIndex);
        Assert.AreEqual("Alpha", listBox.SelectedItem);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardNavigationSelectsExpectedItems()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiListBox listBox = CreateListBox("Alpha", "Bravo", "Charlie", "Delta");
        ui.Root.Children.Add(listBox);
        ui.Render(new DrawContext());
        listBox.Focus();

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(0, listBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(1, listBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(3, listBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, listBox.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DisabledItemsAreSkippedByPointerKeyboardAndWheel()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiListBox listBox = CreateListBox("Alpha", "Disabled", "Charlie");
        listBox.IsItemEnabledSelector = item => !Equals(item, "Disabled");
        listBox.SelectedIndex = 0;
        ui.Root.Children.Add(listBox);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 42);
        Assert.AreEqual(0, listBox.SelectedIndex);

        listBox.Focus();
        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(2, listBox.SelectedIndex);

        DispatchPointer(overlay, new Win32PointerEvent(Win32PointerEventKind.Wheel, Win32PointerButton.None, 20, 66, 120, IsHorizontalWheel: false));
        Assert.AreEqual(0, listBox.SelectedIndex);
    }

    [TestMethod]
    public void DynamicItemsCoerceSelectionAndRaiseChange()
    {
        UiListBox listBox = CreateListBox("Alpha", "Bravo", "Charlie");
        int changes = 0;
        listBox.SelectionChanged += (_, _) => changes++;

        listBox.SelectedIndex = 2;
        listBox.Items.RemoveAt(2);
        listBox.Items.Clear();

        Assert.AreEqual(-1, listBox.SelectedIndex);
        Assert.IsNull(listBox.SelectedItem);
        Assert.AreEqual(3, changes);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 240, 160),
        });

    private static UiListBox CreateListBox(params object?[] items)
    {
        UiListBox listBox = new()
        {
            Width = 140f,
            Height = 96f,
            ItemHeight = 24f,
        };
        Canvas.SetLeft(listBox, 10f);
        Canvas.SetTop(listBox, 10f);
        foreach (object? item in items)
        {
            listBox.Items.Add(item);
        }

        return listBox;
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
        => DispatchPointer(overlay, new Win32PointerEvent(kind, button, x, y));

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEvent pointer)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [pointer]);
    }

    private static void DispatchUiPointer(OverlayUiRoot ui, OverlayPointerEventKind kind, OverlayPointerButton button, PointF position)
    {
        MethodInfo method = typeof(OverlayUiRoot).GetMethod("DispatchPointer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayUiRoot), "DispatchPointer");
        method.Invoke(ui, [new OverlayPointerEventArgs(kind, button, position, (int)MathF.Round(position.X), (int)MathF.Round(position.Y)), kind]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }
}
