using ModernOverlay.UI;

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

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

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
}
