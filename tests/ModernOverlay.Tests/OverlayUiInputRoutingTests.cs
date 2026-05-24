using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiInputRoutingTests
{
    private static readonly string[] ExpectedDirectThenBubble = ["child:Direct", "parent:Bubble"];
    private static readonly string[] ExpectedPopupHit = ["popup"];
    private static readonly int[] ExpectedClickCounts = [1, 2];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerEventsRouteDirectThenBubble()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Canvas parent = new()
        {
            Width = 100f,
            Height = 100f,
        };
        ProbeElement child = CreateInputElement(20f, 20f, 30f, 30f);
        List<string> route = [];
        child.PointerPressed += (_, args) => route.Add($"child:{args.RoutePhase}");
        parent.PointerPressed += (_, args) =>
        {
            Assert.AreSame(child, args.OriginalSource);
            Assert.AreSame(parent, args.Source);
            route.Add($"parent:{args.RoutePhase}");
        };
        parent.Children.Add(child);
        ui.Root.Children.Add(parent);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 25, 25);

        CollectionAssert.AreEqual(ExpectedDirectThenBubble, route);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task HandledPointerEventStopsBubbling()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Canvas parent = new()
        {
            Width = 100f,
            Height = 100f,
        };
        ProbeElement child = CreateInputElement(20f, 20f, 30f, 30f);
        bool parentCalled = false;
        child.PointerPressed += (_, args) => args.Handled = true;
        parent.PointerPressed += (_, _) => parentCalled = true;
        parent.Children.Add(child);
        ui.Root.Children.Add(parent);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 25, 25);

        Assert.IsFalse(parentCalled);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PointerEnterLeaveClickDoubleClickAndWheelAreTranslated()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton button = new()
        {
            Text = "Input",
            Width = 80f,
            Height = 30f,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        int enterCount = 0;
        int exitCount = 0;
        int wheelDelta = 0;
        bool horizontalWheel = false;
        List<int> clickCounts = [];
        button.PointerEntered += (_, _) => enterCount++;
        button.PointerExited += (_, _) => exitCount++;
        button.PointerWheel += (_, args) =>
        {
            wheelDelta = args.WheelDelta;
            horizontalWheel = args.IsHorizontalWheel;
        };
        button.Click += (_, args) => clickCounts.Add(args.ClickCount);
        ui.Root.Children.Add(button);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Moved, Win32PointerButton.None, 150, 100);
        Click(overlay, 20, 20);
        Click(overlay, 20, 20);
        DispatchPointer(overlay, new Win32PointerEvent(Win32PointerEventKind.Wheel, Win32PointerButton.None, 20, 20, 120, IsHorizontalWheel: true));

        Assert.AreEqual(1, enterCount);
        Assert.AreEqual(1, exitCount);
        CollectionAssert.AreEqual(ExpectedClickCounts, clickCounts);
        Assert.AreEqual(120, wheelDelta);
        Assert.IsTrue(horizontalWheel);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InputRegionsRespectNestedClippingAndCustomPredicates()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Canvas clippedParent = new()
        {
            Width = 50f,
            Height = 50f,
        };
        ProbeElement clippedChild = CreateInputElement(40f, 40f, 30f, 30f);
        ProbeElement custom = CreateInputElement(70f, 10f, 30f, 30f);
        custom.InputRegion = (_, point) => point.X < 85f;
        clippedParent.Children.Add(clippedChild);
        ui.Root.Children.Add(clippedParent);
        ui.Root.Children.Add(custom);
        ui.Render(new DrawContext());

        Assert.AreEqual(OverlayInputRegionResult.Interactive, ui.ResolveInputRegion(new PointF(45f, 45f)));
        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(new PointF(60f, 60f)));
        Assert.AreEqual(OverlayInputRegionResult.Interactive, ui.ResolveInputRegion(new PointF(80f, 20f)));
        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(new PointF(90f, 20f)));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PopupInputResolvesBeforeNormalContent()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement normal = CreateInputElement(10f, 10f, 60f, 40f);
        Popup popup = new()
        {
            IsOpen = true,
            Width = 80f,
            Height = 50f,
            Placement = new PointF(0f, 0f),
        };
        ProbeElement popupChild = CreateInputElement(10f, 10f, 60f, 30f);
        List<string> hits = [];
        normal.PointerPressed += (_, _) => hits.Add("normal");
        popupChild.PointerPressed += (_, _) => hits.Add("popup");
        popup.Children.Add(popupChild);
        ui.Root.Children.Add(normal);
        ui.Root.Children.Add(popup);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        CollectionAssert.AreEqual(ExpectedPopupHit, hits);
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

    private sealed class ProbeElement : UiElement
    {
    }
}
