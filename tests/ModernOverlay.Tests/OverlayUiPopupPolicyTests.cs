using ModernOverlay.UI;
using UiButton = ModernOverlay.UI.Button;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayUiPopupPolicyTests
{
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
