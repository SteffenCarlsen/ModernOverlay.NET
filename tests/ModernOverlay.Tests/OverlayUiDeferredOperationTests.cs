using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiDeferredOperationTests
{
    private static readonly string[] ExpectedNonReentrantDeferredOrder = ["first-start", "first-end", "second", "nested"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DeferRunsImmediatelyWhenRootIsIdle()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });

        ui.Defer(() => ui.Root.Children.Add(new TextBlock { Text = "Idle add" }));

        Assert.AreEqual(1, ui.Root.Children.Count);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ChildMutationDuringRenderIsDeferredUntilProtectedPhaseExits()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var panel = new RenderMutationPanel { Width = 100f, Height = 60f };
        ui.Root.Children.Add(panel);

        ui.Render(new DrawContext());

        Assert.AreEqual(0, panel.ChildCountObservedDuringRender);
        Assert.AreEqual(1, panel.Children.Count);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DeferredOperationFlushIsNonReentrantAndPreservesFifoOrder()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var panel = new NestedDeferPanel { Width = 100f, Height = 60f };
        ui.Root.Children.Add(panel);

        ui.Render(new DrawContext());

        CollectionAssert.AreEqual(ExpectedNonReentrantDeferredOrder, panel.Order);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ClickHandlerCanRemoveClickedElementAfterRouteCompletes()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton button = new()
        {
            Text = "Remove",
            Width = 80f,
            Height = 30f,
        };
        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 10f);
        ui.Root.Children.Add(button);
        button.Click += (_, _) => ui.Root.Children.Remove(button);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);
        DispatchPointer(overlay, Win32PointerEventKind.Released, Win32PointerButton.Left, 20, 20);

        Assert.AreEqual(0, ui.Root.Children.Count);
        Assert.IsNull(button.Root);
        Assert.IsNull(ui.CapturedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PopupCanCloseDuringBubblingWithoutInvalidatingCurrentRoute()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Popup popup = new()
        {
            IsOpen = true,
            Width = 80f,
            Height = 50f,
        };
        var child = new ProbeElement
        {
            Width = 40f,
            Height = 20f,
            ReceivesInput = true,
        };
        List<string> route = [];
        child.PointerPressed += (_, _) =>
        {
            route.Add("child");
            popup.IsOpen = false;
        };
        popup.PointerPressed += (_, args) =>
        {
            route.Add(args.RoutePhase.ToString());
        };
        popup.Children.Add(child);
        ui.Root.Children.Add(popup);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 8, 8);

        CollectionAssert.AreEqual(new[] { "child", UiRoutedEventPhase.Bubble.ToString() }, route);
        Assert.IsFalse(popup.IsOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task LayoutInvalidationDuringArrangeSchedulesFollowUpLayoutPass()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var element = new ArrangeInvalidatingElement
        {
            Width = 40f,
            Height = 20f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
        };
        ui.Root.Children.Add(element);

        ui.Render(new DrawContext());

        Assert.IsTrue(ui.Metrics.LayoutPasses >= 2);
        Assert.AreEqual(80f, element.Bounds.Width);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task CaptureAndFocusAreReleasedWhenOwnerIsRemoved()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton button = new()
        {
            Text = "Owner",
            Width = 80f,
            Height = 30f,
        };
        ui.Root.Children.Add(button);

        button.Focus();
        button.CapturePointer();
        ui.Root.Children.Remove(button);

        Assert.IsNull(ui.FocusedElement);
        Assert.IsNull(ui.CapturedElement);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private sealed class RenderMutationPanel : UiPanel
    {
        private bool added;

        public int ChildCountObservedDuringRender { get; private set; }

        protected override void RenderCore(UiRenderContext context)
        {
            if (!added)
            {
                added = true;
                Children.Add(new TextBlock { Text = "Deferred add" });
                ChildCountObservedDuringRender = Children.Count;
            }

            base.RenderCore(context);
        }
    }

    private sealed class ArrangeInvalidatingElement : UiElement
    {
        private bool invalidated;

        protected override void ArrangeCore(RectF finalRect)
        {
            if (invalidated)
            {
                return;
            }

            invalidated = true;
            Width = 80f;
        }
    }

    private sealed class NestedDeferPanel : UiPanel
    {
        private bool queued;

        public List<string> Order { get; } = [];

        protected override void RenderCore(UiRenderContext context)
        {
            if (!queued)
            {
                queued = true;
                Root!.Defer(() =>
                {
                    Order.Add("first-start");
                    Root.Defer(() => Order.Add("nested"));
                    Order.Add("first-end");
                });
                Root.Defer(() => Order.Add("second"));
            }

            base.RenderCore(context);
        }
    }

    private sealed class ProbeElement : UiElement
    {
    }
}
