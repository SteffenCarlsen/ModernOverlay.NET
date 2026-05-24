using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiWindowTests
{
    private const int VirtualKeyEscape = 0x1B;

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DragResizeAndReleaseSaveManualPlacement()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var store = new MemoryLayoutStore();
        UiWindow window = CreateWindow(120f, 90f);
        window.LayoutKey = "settings";
        window.LayoutStore = store;
        ui.Root.Children.Add(window);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.Left, 50, 60);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 50, 60);
        ui.Render(new DrawContext());

        AssertPlacement(window, 40f, 50f);
        Assert.IsTrue(store.TryLoad("settings", out UiPlacement dragged));
        Assert.AreEqual(40f, dragged.Bounds.X, 0.001f);
        Assert.AreEqual(50f, dragged.Bounds.Y, 0.001f);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 152, 132);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.Left, 182, 152);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 182, 152);

        Assert.AreEqual(150f, window.Width, 0.001f);
        Assert.AreEqual(110f, window.Height, 0.001f);
        Assert.IsTrue(store.TryLoad("settings", out UiPlacement resized));
        Assert.AreEqual(150f, resized.Bounds.Width, 0.001f);
        Assert.AreEqual(110f, resized.Bounds.Height, 0.001f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerActivationBringsWindowToFrontAndFocusesIt()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow lower = CreateWindow(120f, 90f);
        UiWindow upper = CreateWindow(120f, 90f);
        Canvas.SetLeft(upper, 150f);
        lower.ZIndex = (int)UiLayer.Floating;
        upper.ZIndex = (int)UiLayer.Floating + 3;
        ui.Root.Children.Add(lower);
        ui.Root.Children.Add(upper);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        Assert.AreSame(lower, ui.FocusedElement);
        Assert.IsTrue(lower.ZIndex > upper.ZIndex);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CloseChromeAndEscapeRequestCloseAndRemoveWindow()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow clicked = CreateWindow(120f, 90f);
        int clickedCloseCount = 0;
        clicked.CloseRequested += (_, _) => clickedCloseCount++;
        ui.Root.Children.Add(clicked);
        ui.Render(new DrawContext());

        Click(overlay, 113, 25);

        Assert.AreEqual(1, clickedCloseCount);
        CollectionAssert.DoesNotContain(ui.Root.Children.ToArray(), clicked);

        UiWindow keyboard = CreateWindow(120f, 90f);
        int keyboardCloseCount = 0;
        keyboard.CloseRequested += (_, _) => keyboardCloseCount++;
        ui.Root.Children.Add(keyboard);
        ui.Render(new DrawContext());
        keyboard.Focus();

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.AreEqual(1, keyboardCloseCount);
        CollectionAssert.DoesNotContain(ui.Root.Children.ToArray(), keyboard);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task MinimizeBehaviorsCollapseHideAndDockUntilRestored()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow collapse = CreateWindow(120f, 90f);
        int minimizedChanges = 0;
        collapse.MinimizedChanged += (_, _) => minimizedChanges++;
        ui.Root.Children.Add(collapse);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);
        ui.Render(new DrawContext());

        Assert.IsTrue(collapse.IsMinimized);
        Assert.AreEqual(30f, collapse.Bounds.Height, 0.001f);
        Assert.AreEqual(1, minimizedChanges);

        collapse.Restore();
        ui.Root.Children.Remove(collapse);

        UiWindow hidden = CreateWindow(120f, 90f);
        hidden.MinimizeBehavior = MinimizeBehavior.HideUntilRestored;
        ui.Root.Children.Add(hidden);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);

        Assert.IsTrue(hidden.IsMinimized);
        Assert.AreEqual(UiVisibility.Hidden, hidden.Visibility);

        hidden.Restore();
        Assert.AreEqual(UiVisibility.Visible, hidden.Visibility);
        ui.Root.Children.Remove(hidden);

        UiWindow docked = CreateWindow(120f, 90f);
        docked.MinimizeBehavior = MinimizeBehavior.Dock;
        ui.Root.Children.Add(docked);
        ui.Render(new DrawContext());

        Click(overlay, 89, 25);

        Assert.IsTrue(docked.IsMinimized);
        Assert.AreEqual(200f, docked.Width, 0.001f);
        Assert.AreEqual(30f, docked.Height, 0.001f);
        AssertPlacement(docked, 8f, 142f);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 260, 180),
        });

    private static UiWindow CreateWindow(float width, float height)
    {
        UiWindow window = new()
        {
            Title = "Tools",
            Width = width,
            Height = height,
            MinWidth = 80f,
            MinHeight = 60f,
            Padding = Thickness.Zero,
        };
        Canvas.SetLeft(window, 10f);
        Canvas.SetTop(window, 10f);
        return window;
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

    private static void AssertPlacement(UiWindow window, float expectedLeft, float expectedTop)
    {
        Assert.AreEqual(expectedLeft, Canvas.GetLeft(window), 0.001f);
        Assert.AreEqual(expectedTop, Canvas.GetTop(window), 0.001f);
    }

    private sealed class MemoryLayoutStore : IUiLayoutStore
    {
        private readonly Dictionary<string, UiPlacement> placements = [];

        public bool TryLoad(string key, out UiPlacement placement)
            => placements.TryGetValue(key, out placement);

        public void Save(string key, UiPlacement placement)
            => placements[key] = placement;
    }
}
