using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiCommandTests
{
    private const int VirtualKeyEnter = 0x0D;

    [TestMethod]
    public void UiCommandPassesParametersAndSkipsExecutionWhenUnavailable()
    {
        object? executedParameter = null;
        bool canExecute = false;
        UiCommand command = new(parameter => executedParameter = parameter, _ => canExecute);

        command.Execute("blocked");
        Assert.IsNull(executedParameter);

        canExecute = true;
        command.Execute("allowed");

        Assert.AreEqual("allowed", executedParameter);
        Assert.IsTrue(command.CanExecute("allowed"));
    }

    [TestMethod]
    public void UiCommandFromActionInvokesParameterlessAction()
    {
        int count = 0;
        UiCommand command = UiCommand.FromAction(() => count++);

        command.Execute("ignored");

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ButtonClickInvokesClickCallbackAndCommandWithParameter()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        object? commandParameter = null;
        int clickCount = 0;
        UiButton button = CreateButton();
        button.CommandParameter = "profile";
        button.Command = new UiCommand(parameter => commandParameter = parameter);
        button.Click += (_, args) => clickCount = args.ClickCount;
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);

        Assert.AreEqual("profile", commandParameter);
        Assert.AreEqual(1, clickCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DisabledCommandStateSuppressesPointerAndKeyboardActivationUntilCanExecuteChanges()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        bool canExecute = false;
        int commandCount = 0;
        int clickCount = 0;
        UiCommand command = new(_ => commandCount++, _ => canExecute);
        UiButton button = CreateButton();
        button.Command = command;
        button.Click += (_, _) => clickCount++;
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        Click(overlay, 20, 20);
        button.Focus();
        DispatchKey(overlay, VirtualKeyEnter, pressed: true);

        Assert.AreEqual(0, commandCount);
        Assert.AreEqual(0, clickCount);

        canExecute = true;
        command.RaiseCanExecuteChanged();
        Click(overlay, 20, 20);
        DispatchKey(overlay, VirtualKeyEnter, pressed: true);

        Assert.AreEqual(2, commandCount);
        Assert.AreEqual(2, clickCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ReplacingCommandUnsubscribesOldCommandState()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        bool oldCanExecute = false;
        bool newCanExecute = true;
        int oldCount = 0;
        int newCount = 0;
        UiCommand oldCommand = new(_ => oldCount++, _ => oldCanExecute);
        UiCommand newCommand = new(_ => newCount++, _ => newCanExecute);
        UiButton button = CreateButton();
        button.Command = oldCommand;
        button.Command = newCommand;
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        oldCanExecute = true;
        oldCommand.RaiseCanExecuteChanged();
        newCanExecute = false;
        newCommand.RaiseCanExecuteChanged();
        Click(overlay, 20, 20);
        Assert.AreEqual(0, oldCount);
        Assert.AreEqual(0, newCount);

        newCanExecute = true;
        newCommand.RaiseCanExecuteChanged();
        Click(overlay, 20, 20);

        Assert.AreEqual(0, oldCount);
        Assert.AreEqual(1, newCount);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 240, 160),
        });

    private static UiButton CreateButton()
    {
        UiButton button = new()
        {
            Text = "Apply",
            Width = 80f,
            Height = 30f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        return button;
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

    private static void DispatchKey(OverlayWindow overlay, int virtualKey, bool pressed)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [new Win32KeyboardEvent(virtualKey, pressed, false, 1, 0, false, false, !pressed, Win32ModifierKeys.None)]);
    }
}
