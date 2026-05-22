namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayWindowThreadingTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RenderCallbackRunsOnOwnerThread()
    {
        int testThreadId = Environment.CurrentManagedThreadId;
        var renderedThread = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        overlay.Render += frame =>
        {
            Assert.IsNotNull(frame);
            renderedThread.TrySetResult(Environment.CurrentManagedThreadId);
            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);
        int renderThreadId = await renderedThread.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreNotEqual(testThreadId, renderThreadId);
        Assert.AreEqual(renderThreadId, overlay.FrameStats.RenderThreadId);
    }
}
