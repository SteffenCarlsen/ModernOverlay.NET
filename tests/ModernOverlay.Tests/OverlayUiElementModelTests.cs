using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiElementModelTests
{
    private static readonly string[] ExpectedSecondHit = ["second"];
    private static readonly string[] ExpectedTopHit = ["top"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task AddingAndRemovingChildUpdatesParentRootAndLifecycleEvents()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        var child = new LifecycleElement();

        ui.Root.Children.Add(child);

        Assert.AreSame(ui.Root, child.Parent);
        Assert.AreSame(ui, child.Root);
        Assert.AreEqual(1, child.AttachedCount);
        Assert.AreEqual(0, child.DetachedCount);
        Assert.AreEqual(2, ui.Metrics.ElementCount);

        Assert.IsTrue(ui.Root.Children.Remove(child));

        Assert.IsNull(child.Parent);
        Assert.IsNull(child.Root);
        Assert.AreEqual(1, child.AttachedCount);
        Assert.AreEqual(1, child.DetachedCount);
        Assert.AreEqual(1, ui.Metrics.ElementCount);
    }

    [TestMethod]
    public void ChildCollectionRejectsMultipleParentsAndCycles()
    {
        UiPanel firstParent = new();
        UiPanel secondParent = new();
        UiPanel childPanel = new();
        UiElement leaf = new ProbeElement();

        firstParent.Children.Add(leaf);
        childPanel.Children.Add(firstParent);

        Assert.ThrowsExactly<InvalidOperationException>(() => secondParent.Children.Add(leaf));
        Assert.ThrowsExactly<InvalidOperationException>(() => firstParent.Children.Add(childPanel));
    }

    [TestMethod]
    public void ChildCollectionClearDetachesChildrenAndMissingRemoveReturnsFalse()
    {
        UiPanel parent = new();
        UiElement first = new ProbeElement();
        UiElement second = new ProbeElement();
        UiElement missing = new ProbeElement();
        parent.Children.Add(first);
        parent.Children.Add(second);

        Assert.IsFalse(parent.Children.Remove(missing));

        parent.Children.Clear();

        Assert.AreEqual(0, parent.Children.Count);
        Assert.IsNull(first.Parent);
        Assert.IsNull(second.Parent);
    }

    [TestMethod]
    public void ChildCollectionEnumerationPreservesInsertionOrder()
    {
        UiPanel parent = new();
        UiElement first = new ProbeElement();
        UiElement second = new ProbeElement();
        UiElement third = new ProbeElement();

        parent.Children.Add(first);
        parent.Children.Add(second);
        parent.Children.Add(third);

        CollectionAssert.AreEqual(new[] { first, second, third }, parent.Children.ToArray());
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InputHitTestingUsesZIndexThenReverseInsertionOrder()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement first = CreateInputElement();
        ProbeElement second = CreateInputElement();
        ProbeElement top = CreateInputElement();
        top.ZIndex = 10;
        List<string> hits = [];
        first.PointerPressed += (_, _) => hits.Add("first");
        second.PointerPressed += (_, _) => hits.Add("second");
        top.PointerPressed += (_, _) => hits.Add("top");

        ui.Root.Children.Add(first);
        ui.Root.Children.Add(second);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 12, 14);

        CollectionAssert.AreEqual(ExpectedSecondHit, hits);

        hits.Clear();
        ui.Root.Children.Add(top);

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 12, 14);

        CollectionAssert.AreEqual(ExpectedTopHit, hits);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task LayoutInvalidationFromChildPropertyRecomputesBounds()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement child = CreateInputElement();
        ui.Root.Children.Add(child);

        ui.Render(new DrawContext());
        long initialLayoutPasses = ui.Metrics.LayoutPasses;
        Assert.AreEqual(new RectF(10f, 12f, 40f, 20f), child.Bounds);

        child.Width = 80f;
        ui.Render(new DrawContext());

        Assert.IsTrue(ui.Metrics.LayoutPasses > initialLayoutPasses);
        Assert.AreEqual(new RectF(10f, 12f, 80f, 20f), child.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task HiddenCollapsedAndDisabledElementsDoNotParticipateInInput()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        ProbeElement child = CreateInputElement();
        ui.Root.Children.Add(child);

        ui.Render(new DrawContext());

        Assert.AreEqual(OverlayInputRegionResult.Interactive, ui.ResolveInputRegion(new PointF(12f, 14f)));

        child.Visibility = UiVisibility.Hidden;
        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(new PointF(12f, 14f)));

        child.Visibility = UiVisibility.Visible;
        child.IsEnabled = false;
        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(new PointF(12f, 14f)));

        child.IsEnabled = true;
        child.Visibility = UiVisibility.Collapsed;
        ui.Render(new DrawContext());

        Assert.AreEqual(new RectF(10f, 12f, 0f, 0f), child.Bounds);
        Assert.AreEqual(OverlayInputRegionResult.PassThrough, ui.ResolveInputRegion(new PointF(12f, 14f)));
    }

    [TestMethod]
    public void EffectiveEnabledStateFollowsAncestors()
    {
        UiPanel parent = new();
        UiPanel childPanel = new();
        UiElement leaf = new ProbeElement();
        parent.Children.Add(childPanel);
        childPanel.Children.Add(leaf);

        Assert.IsTrue(leaf.IsEffectivelyEnabled);

        parent.IsEnabled = false;

        Assert.IsFalse(childPanel.IsEffectivelyEnabled);
        Assert.IsFalse(leaf.IsEffectivelyEnabled);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 200, 120),
        });

    private static void DispatchPointer(OverlayWindow overlay, Win32PointerEventKind kind, Win32PointerButton button, int x, int y)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandlePointerEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandlePointerEvent");
        method.Invoke(overlay, [new Win32PointerEvent(kind, button, x, y)]);
    }

    private static ProbeElement CreateInputElement()
    {
        ProbeElement element = new()
        {
            Width = 40f,
            Height = 20f,
            HorizontalAlignment = UiHorizontalAlignment.Left,
            VerticalAlignment = UiVerticalAlignment.Top,
            ReceivesInput = true,
        };
        Canvas.SetLeft(element, 10f);
        Canvas.SetTop(element, 12f);
        return element;
    }

    private sealed class ProbeElement : UiElement
    {
    }

    private sealed class LifecycleElement : UiElement
    {
        public int AttachedCount { get; private set; }

        public int DetachedCount { get; private set; }

        protected override void OnAttached() => AttachedCount++;

        protected override void OnDetached() => DetachedCount++;
    }
}
