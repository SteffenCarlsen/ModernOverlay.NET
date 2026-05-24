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
        using var window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
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

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void FrameLoopRechecksDynamicIntervalWhileRunning()
    {
        using var window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay dynamic frame interval test",
            X: 0,
            Y: 0,
            Width: 100,
            Height: 100,
            ClickThrough: true,
            TopMost: false,
            ToolWindow: true));
        using var stopper = new FrameLoopStopper(stopAfterRenderAttempts: 3, timeout: TimeSpan.FromSeconds(5));
        int resolveAttempts = 0;

        window.RunFrameLoop(
            () =>
            {
                resolveAttempts++;
                return resolveAttempts == 1
                    ? TimeSpan.FromMilliseconds(1)
                    : TimeSpan.FromMilliseconds(2);
            },
            stopper.RenderFrame,
            stopper.Token);

        Assert.IsTrue(stopper.RenderAttempts >= 3);
        Assert.IsTrue(resolveAttempts > 1);
    }

    private sealed class FrameLoopStopper : IDisposable
    {
        private readonly CancellationTokenSource cancellation;
        private readonly int stopAfterRenderAttempts;

        public FrameLoopStopper(int stopAfterRenderAttempts, TimeSpan timeout)
        {
            this.stopAfterRenderAttempts = stopAfterRenderAttempts;
            cancellation = new CancellationTokenSource(timeout);
        }

        public CancellationToken Token => cancellation.Token;

        public int RenderAttempts { get; private set; }

        public void RenderFrame()
        {
            RenderAttempts++;
            if (RenderAttempts == stopAfterRenderAttempts)
            {
                cancellation.Cancel();
            }
        }

        public void Dispose()
        {
            cancellation.Dispose();
        }
    }
}
