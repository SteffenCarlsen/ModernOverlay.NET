using ModernOverlay.UI;
using ModernOverlay.Win32;
using System.Reflection;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiPopupPolicyTests
{
    private const int VirtualKeyEscape = 0x1B;
    private static readonly string[] ExpectedUpperPopupHit = ["upper"];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PopupPlacementResolvesOwnerAnchorsAndClampsToOverlay()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Canvas.SetLeft(owner, 20f);
        Canvas.SetTop(owner, 30f);
        Popup anchored = CreateOwnedPopup(owner);
        anchored.Width = 80f;
        anchored.Height = 40f;
        anchored.PlacementOffset = new PointF(3f, 4f);
        Popup clamped = new()
        {
            IsOpen = true,
            Width = 80f,
            Height = 40f,
            Placement = new PointF(300f, 200f),
        };
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(anchored);
        ui.Root.Children.Add(clamped);
        anchored.IsOpen = true;

        ui.Render(new DrawContext());

        AssertRect(new RectF(23f, 62f, 80f, 40f), anchored.Bounds);
        AssertRect(new RectF(240f, 180f, 80f, 40f), clamped.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TopmostPopupReceivesPointerInputByZOrder()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Popup lower = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup, placement: new PointF(10f, 10f));
        Popup upper = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup + 1, placement: new PointF(10f, 10f));
        List<string> hits = [];
        lower.PointerPressed += (_, _) => hits.Add("lower");
        upper.PointerPressed += (_, _) => hits.Add("upper");
        ui.Root.Children.Add(lower);
        ui.Root.Children.Add(upper);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 20, 20);

        CollectionAssert.AreEqual(ExpectedUpperPopupHit, hits);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OutsidePointerAndEscapeDismissOpenPopups()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Popup outsideDismissed = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup, placement: new PointF(10f, 10f));
        Popup escapeDismissed = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup + 1, placement: new PointF(80f, 10f));
        ui.Root.Children.Add(outsideDismissed);
        ui.Root.Children.Add(escapeDismissed);
        ui.Render(new DrawContext());

        DispatchPointer(overlay, Win32PointerEventKind.Pressed, Win32PointerButton.Left, 250, 150);

        Assert.IsFalse(outsideDismissed.IsOpen);
        Assert.IsFalse(escapeDismissed.IsOpen);

        escapeDismissed.IsOpen = true;
        ui.Render(new DrawContext());

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsFalse(escapeDismissed.IsOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OpeningPopupPreservesOwnerFocus()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);

        owner.Focus();
        popup.IsOpen = true;

        Assert.AreSame(owner, ui.FocusedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FocusablePopupChildCanReceiveFocus()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        UiButton child = new() { Text = "Popup action", Width = 120f, Height = 28f };
        popup.Children.Add(child);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);

        popup.IsOpen = true;
        child.Focus();

        Assert.AreSame(child, ui.FocusedElement);
        Assert.IsTrue(child.IsKeyboardFocusWithin);
        Assert.IsTrue(popup.IsKeyboardFocusWithin);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OwnerRemovalClosesOwnedPopup()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);
        popup.IsOpen = true;

        ui.Root.Children.Remove(owner);

        Assert.IsFalse(popup.IsOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PopupChildCaptureUsesOwningRootAndReleasesWhenOwnerUnavailable()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        UiButton child = new() { Text = "Drag me", Width = 120f, Height = 28f };
        popup.Children.Add(child);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);
        popup.IsOpen = true;

        child.CapturePointer();

        Assert.AreSame(child, ui.CapturedElement);

        owner.IsEnabled = false;

        Assert.IsFalse(popup.IsOpen);
        Assert.IsNull(ui.CapturedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PopupChildCaptureReleasesWhenOwnerIsHidden()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        UiButton child = new() { Text = "Drag me", Width = 120f, Height = 28f };
        popup.Children.Add(child);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);
        popup.IsOpen = true;

        child.CapturePointer();
        owner.Visibility = UiVisibility.Hidden;

        Assert.IsFalse(popup.IsOpen);
        Assert.IsNull(ui.CapturedElement);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task NestedPopupsUseRootZOrderForEscapeDismissal()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        Popup parent = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup, placement: new PointF(10f, 10f));
        Popup child = CreateAbsolutePopup(zIndex: (int)UiLayer.Popup + 1, placement: new PointF(30f, 30f));
        child.Owner = parent;
        parent.Children.Add(child);
        ui.Root.Children.Add(parent);
        ui.Render(new DrawContext());

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsTrue(parent.IsOpen);
        Assert.IsFalse(child.IsOpen);

        DispatchKey(overlay, VirtualKeyEscape);

        Assert.IsFalse(parent.IsOpen);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DeferredOwnerCleanupRunsBeforeNewCaptureAssignment()
    {
        await using OverlayWindow overlay = await CreateOverlayAsync();
        using OverlayUiRoot ui = OverlayUi.Attach(overlay, new OverlayUiOptions { RegisterInputRegions = false });
        UiButton owner = CreateOwnerButton();
        Popup popup = CreateOwnedPopup(owner);
        UiButton popupChild = new() { Text = "Popup drag", Width = 120f, Height = 28f };
        UiButton nextCapture = new() { Text = "Next drag", Width = 120f, Height = 28f };
        var trigger = new DeferredCleanupAndCaptureElement(owner, nextCapture)
        {
            Width = 1f,
            Height = 1f,
        };
        popup.Children.Add(popupChild);
        ui.Root.Children.Add(owner);
        ui.Root.Children.Add(popup);
        ui.Root.Children.Add(nextCapture);
        ui.Root.Children.Add(trigger);
        popup.IsOpen = true;
        popupChild.CapturePointer();

        ui.Render(new DrawContext());

        Assert.IsFalse(popup.IsOpen);
        Assert.AreSame(nextCapture, ui.CapturedElement);
    }

    private static async ValueTask<OverlayWindow> CreateOverlayAsync()
        => await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = new WindowBounds(10, 20, 320, 220),
        });

    private static UiButton CreateOwnerButton()
        => new()
        {
            Text = "Owner",
            Width = 100f,
            Height = 28f,
            ReceivesInput = true,
        };

    private static Popup CreateOwnedPopup(UiElement owner)
        => new()
        {
            Owner = owner,
            Width = 140f,
            Height = 80f,
            PlacementMode = UiPopupPlacementMode.OwnerAnchor,
        };

    private static Popup CreateAbsolutePopup(int zIndex, PointF placement)
        => new()
        {
            IsOpen = true,
            Width = 80f,
            Height = 50f,
            Placement = placement,
            ZIndex = zIndex,
        };

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

    private static void AssertRect(RectF expected, RectF actual)
    {
        Assert.AreEqual((double)expected.X, actual.X, 0.01d, "X");
        Assert.AreEqual((double)expected.Y, actual.Y, 0.01d, "Y");
        Assert.AreEqual((double)expected.Width, actual.Width, 0.01d, "Width");
        Assert.AreEqual((double)expected.Height, actual.Height, 0.01d, "Height");
    }

    private sealed class DeferredCleanupAndCaptureElement : UiElement
    {
        private readonly UiElement owner;
        private readonly UiElement nextCapture;
        private bool queued;

        public DeferredCleanupAndCaptureElement(UiElement owner, UiElement nextCapture)
        {
            this.owner = owner;
            this.nextCapture = nextCapture;
        }

        protected override void RenderCore(UiRenderContext context)
        {
            if (queued)
            {
                return;
            }

            queued = true;
            Root!.Defer(() => owner.IsEnabled = false);
            Root.Defer(nextCapture.CapturePointer);
        }
    }
}
