using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiContextMenu = ModernOverlay.UI.ContextMenu;
using UiMenu = ModernOverlay.UI.Menu;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiMenuTests
{
    private const int VirtualKeyEnter = 0x0D;
    private const int VirtualKeyEscape = 0x1B;
    private const int VirtualKeyHome = 0x24;
    private const int VirtualKeyDown = 0x28;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MenuPointerInvokesCommandWithParameter()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? invokedParameter = null;
        UiMenu menu = CreateMenu();
        menu.Items.Add(new UiMenuItem("Apply", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "pointer" });
        ui.Root.Children.Add(menu);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);

        Assert.AreEqual("pointer", invokedParameter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MenuPointerBoundaryKeepsPreviousRenderedItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? invokedParameter = null;
        UiMenu menu = CreateMenu();
        menu.Items.Add(new UiMenuItem("A", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "first" });
        menu.Items.Add(new UiMenuItem("B", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "second" });
        ui.Root.Children.Add(menu);
        ui.Render(new DrawContext());

        float boundaryX = 10f + 8f + MenuItemWidth("A");
        ClickUi(ui, new PointF(boundaryX, 20f));

        Assert.AreEqual("first", invokedParameter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MenuKeyboardNavigationSkipsDisabledItemsAndInvokesCommand()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? invokedParameter = null;
        UiMenu menu = CreateMenu();
        menu.Items.Add(new UiMenuItem("Disabled", new UiCommand(parameter => invokedParameter = parameter)) { IsEnabled = false, CommandParameter = "disabled" });
        menu.Items.Add(new UiMenuItem("Apply", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "keyboard" });
        ui.Root.Children.Add(menu);
        ui.Render(new DrawContext());
        menu.Focus();

        DispatchKey(overlay, VirtualKeyHome);
        DispatchKey(overlay, VirtualKeyEnter);

        Assert.AreEqual("keyboard", invokedParameter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ContextMenuKeyboardInvokesCommandAndCloses()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? invokedParameter = null;
        UiContextMenu contextMenu = CreateContextMenu();
        contextMenu.Items.Add(new UiMenuItem("Disabled", new UiCommand(parameter => invokedParameter = parameter)) { IsEnabled = false, CommandParameter = "disabled" });
        contextMenu.Items.Add(new UiMenuItem("Apply", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "keyboard" });
        ui.Root.Children.Add(contextMenu);
        ui.Render(new DrawContext());
        contextMenu.Focus();

        DispatchKey(overlay, VirtualKeyDown);
        DispatchKey(overlay, VirtualKeyEnter);

        Assert.AreEqual("keyboard", invokedParameter);
        Assert.IsFalse(contextMenu.IsOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ContextMenuPointerBoundaryKeepsPreviousRenderedItem()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? invokedParameter = null;
        UiContextMenu contextMenu = CreateContextMenu();
        contextMenu.Items.Add(new UiMenuItem("A", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "first" });
        contextMenu.Items.Add(new UiMenuItem("B", new UiCommand(parameter => invokedParameter = parameter)) { CommandParameter = "second" });
        ui.Root.Children.Add(contextMenu);
        ui.Render(new DrawContext());

        Click(overlay, 20, 40);

        Assert.AreEqual("first", invokedParameter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ContextMenuOutsidePointerAndEscapeDismiss()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiContextMenu contextMenu = CreateContextMenu();
        contextMenu.Items.Add(new UiMenuItem("Apply", UiCommand.FromAction(() => { })));
        ui.Root.Children.Add(contextMenu);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 230, 130);
        Assert.IsFalse(contextMenu.IsOpen);

        contextMenu.IsOpen = true;

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsFalse(contextMenu.IsOpen);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiMenu CreateMenu()
    {
        UiMenu menu = new()
        {
            Width = 180f,
        };
        Canvas.SetLeft(menu, 10f);
        Canvas.SetTop(menu, 10f);
        return menu;
    }

    private static UiContextMenu CreateContextMenu()
        => new()
        {
            IsOpen = true,
            Placement = new PointF(10f, 10f),
        };

    private static float MenuItemWidth(string text) => text.Length * UiTheme.Default.FontSize * 0.62f + 22f;

    private static void Click(OverlayWindow overlay, int x, int y)
    {
        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, x, y);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, x, y);
    }

    private static void ClickUi(OverlayUiRoot ui, PointF position)
    {
        DispatchUiPointer(ui, OverlayPointerEventKind.Pressed, OverlayPointerButton.Left, position);
        DispatchUiPointer(ui, OverlayPointerEventKind.Released, OverlayPointerButton.Left, position);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
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
