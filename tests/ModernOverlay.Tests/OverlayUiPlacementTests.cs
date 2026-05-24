using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiPlacementTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ManualPlacementAppliesBounds()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(80f, 50f);
        window.Placement = UiPlacement.Manual(24f, 36f, 80f, 50f);

        ui.Root.Children.Add(window);
        ui.Render(new DrawContext());

        AssertPlacement(window, 24f, 36f);
        Assert.AreEqual(80f, window.Width);
        Assert.AreEqual(50f, window.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task AnchorPlacementRecomputesWhenOverlayBoundsChange()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(60f, 40f);
        window.Placement = UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(4f, 6f, 8f, 10f));
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        AssertPlacement(window, 132f, 70f);

        overlay.ResizePixels(260, 160);
        ui.Render(new DrawContext());

        AssertPlacement(window, 192f, 110f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task AnchorPlacementRecomputesWhenDpiChanges()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(60f, 40f);
        window.Placement = UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(0f, 0f, 10f, 10f));
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        AssertPlacement(window, 230f, 150f);

        DispatchDpiChanged(overlay, new Win32DpiScale(2f, 2f), new Win32WindowBounds(10, 20, 300, 200));
        ui.Render(new DrawContext());

        AssertPlacement(window, 80f, 50f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CursorPlacementUsesLastInputRegionPosition()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay);
        UiWindow window = CreateWindow(60f, 40f);
        window.Placement = UiPlacement.Cursor(new Thickness(3f, 4f, 1f, 2f));
        ui.Root.Children.Add(window);

        _ = ui.ResolveInputRegion(new PointF(20f, 30f));
        ui.Render(new DrawContext());

        AssertPlacement(window, 22f, 32f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ManualPlacementClampsToOverlayAndPreservesHeader()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(200, 120);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(80f, 50f);
        window.Placement = UiPlacement.Manual(500f, 500f, 80f, 50f);
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        AssertPlacement(window, 120f, 90f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PersistedPlacementUsesStoredManualBounds()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var store = new MemoryLayoutStore();
        store.Save("window", UiPlacement.Manual(42f, 54f, 90f, 70f));
        UiWindow window = CreateWindow(60f, 40f);
        window.LayoutStore = store;
        window.Placement = UiPlacement.Persisted("window", UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(8f)));
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        AssertPlacement(window, 42f, 54f);
        Assert.AreEqual(90f, window.Width);
        Assert.AreEqual(70f, window.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PersistedPlacementFallsBackToAnchorWhenNoStoredBoundsExist()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(60f, 40f);
        window.LayoutStore = new MemoryLayoutStore();
        window.Placement = UiPlacement.Persisted("missing", UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(2f, 3f, 4f, 5f)));
        ui.Root.Children.Add(window);

        ui.Render(new DrawContext());

        AssertPlacement(window, 236f, 155f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TargetAnchorPlacementRecomputesWhenTargetBoundsChange()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(30, 40, 200, 120);
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)).WithTrackingInterval(TimeSpan.Zero),
        });
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(60f, 40f);
        window.Placement = UiPlacement.TargetAnchor(OverlayAnchor.BottomRight, new Thickness(0f, 0f, 6f, 8f));
        ui.Root.Children.Add(window);
        int renderCount = 0;

        overlay.Render += frame =>
        {
            ui.Render(frame);
            renderCount++;
            if (renderCount == 1)
            {
                target.SetBounds(30, 40, 260, 160);
                return;
            }

            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);

        AssertPlacement(window, 194f, 112f);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DraggingAnchoredWindowConvertsPlacementToManual()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync(300, 200);
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiWindow window = CreateWindow(80f, 50f);
        window.Placement = UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(10f));
        ui.Root.Children.Add(window);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 15, 15);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 40, 50);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 40, 50);

        Assert.AreEqual(UiPlacementKind.Manual, window.Placement?.Kind);
        AssertPlacement(window, 35f, 45f);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync(int width, int height)
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, width, height),
        });

    private static UiWindow CreateWindow(float width, float height)
        => new()
        {
            Width = width,
            Height = height,
            MinWidth = 0f,
            MinHeight = 0f,
            Padding = Thickness.Zero,
        };

    private static Win32OverlayWindow CreateHiddenTarget(int x, int y, int width, int height)
        => Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay UI placement target",
            X: x,
            Y: y,
            Width: width,
            Height: height,
            ClickThrough: false,
            TopMost: false,
            ToolWindow: true));

    private static void DispatchDpiChanged(OverlayWindow overlay, Win32DpiScale dpiScale, Win32WindowBounds bounds)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleDpiChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleDpiChanged");
        method.Invoke(overlay, [dpiScale, bounds]);
    }

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
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
