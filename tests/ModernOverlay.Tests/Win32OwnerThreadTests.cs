using ModernOverlay.Win32;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class Win32OwnerThreadTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void WindowIsCreatedOnDedicatedOwnerThread()
    {
        int testThreadId = Environment.CurrentManagedThreadId;
        using Win32OverlayWindow window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay owner thread test",
            X: 0,
            Y: 0,
            Width: 100,
            Height: 100,
            ClickThrough: true,
            TopMost: false,
            ToolWindow: true));

        Assert.AreNotEqual(0, window.Hwnd);
        Assert.AreNotEqual(testThreadId, window.OwnerThreadId);

        window.SetClickThrough(false);
        window.SetTopMost(false);
        window.SetBounds(10, 10, 120, 90);
    }
}
