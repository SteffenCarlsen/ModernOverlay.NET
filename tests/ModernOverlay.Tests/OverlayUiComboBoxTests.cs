using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiComboBox = ModernOverlay.UI.ComboBox;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiComboBoxTests
{
    private const int VirtualKeyEscape = 0x1B;
    private const int VirtualKeyEnd = 0x23;
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyUp = 0x26;
    private const int VirtualKeyDown = 0x28;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerTogglesDropDownAndSelectsItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        int changes = 0;
        comboBox.SelectionChanged += (_, _) => changes++;
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(comboBox.IsDropDownOpen);

        Click(overlay, 20, 71);

        Assert.IsFalse(comboBox.IsDropDownOpen);
        Assert.AreEqual(1, comboBox.SelectedIndex);
        Assert.AreEqual("Auto", comboBox.SelectedItem);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerInUnrenderedDropDownPartialRowDoesNotSelectHiddenItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid", "Hidden");
        comboBox.MaxDropDownHeight = 92f;
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        Assert.IsTrue(comboBox.IsDropDownOpen);

        Click(overlay, 20, 120);

        Assert.IsFalse(comboBox.IsDropDownOpen);
        Assert.AreEqual(-1, comboBox.SelectedIndex);
        Assert.IsNull(comboBox.SelectedItem);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OutsidePointerAndEscapeCloseDropDown()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Auto", "Hybrid");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());
        comboBox.IsDropDownOpen = true;

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 220, 130);
        Assert.IsFalse(comboBox.IsDropDownOpen);

        comboBox.IsDropDownOpen = true;

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsFalse(comboBox.IsDropDownOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardNavigationSkipsDisabledItems()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox comboBox = CreateComboBox("Manual", "Disabled", "Hybrid");
        comboBox.IsItemEnabledSelector = item => !Equals(item, "Disabled");
        ui.Root.Children.Add(comboBox);
        ui.Render(new DrawContext());
        comboBox.Focus();

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(0, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyDown);
        Assert.AreEqual(2, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyUp);
        Assert.AreEqual(0, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyEnd);
        Assert.AreEqual(2, comboBox.SelectedIndex);

        DispatchKey(overlay, VirtualKeyHome);
        Assert.AreEqual(0, comboBox.SelectedIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OpenDropDownUsesPopupZOrderForSelection()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiComboBox lower = CreateComboBox("Lower A", "Lower B");
        UiComboBox upper = CreateComboBox("Upper A", "Upper B");
        lower.ZIndex = (int)UiLayer.Popup;
        upper.ZIndex = (int)UiLayer.Popup + 1;
        lower.IsDropDownOpen = true;
        upper.IsDropDownOpen = true;
        ui.Root.Children.Add(lower);
        ui.Root.Children.Add(upper);
        ui.Render(new DrawContext());

        Click(overlay, 20, 71);

        Assert.AreEqual(-1, lower.SelectedIndex);
        Assert.AreEqual(1, upper.SelectedIndex);
        Assert.IsTrue(lower.IsDropDownOpen);
        Assert.IsFalse(upper.IsDropDownOpen);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiComboBox CreateComboBox(params object?[] items)
    {
        UiComboBox comboBox = new()
        {
            Width = 140f,
        };
        Canvas.SetLeft(comboBox, 10f);
        Canvas.SetTop(comboBox, 10f);
        foreach (object? item in items)
        {
            comboBox.Items.Add(item);
        }

        return comboBox;
    }

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
        => DispatchPointer(overlay, new Win32PointerEvent(kind, button, x, y));

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEvent pointer)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [pointer]);
    }

    private static void DispatchKey(OverlayWindow overlay, int virtualKey)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, true, false, 1, 0, false, false, false, Win32ModifierKeys.None)]);
    }
}
