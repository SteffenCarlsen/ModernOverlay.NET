using System.Runtime.InteropServices;

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

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RegisteredDirect2DBackendIsUsedByOverlayWindow()
    {
        using IDisposable registration = ModernOverlay.Direct2D.Direct2DOverlayBackend.RegisterForScope();

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        Assert.AreEqual(Rendering.RenderBackendKind.Direct2DHwnd, overlay.RenderBackendKind);
        Assert.AreEqual("Direct2D HWND", overlay.BackendName);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RecreateAsyncRecreatesRegisteredBackendAndRaisesDeviceEvents()
    {
        using IDisposable registration = ModernOverlay.Direct2D.Direct2DOverlayBackend.RegisterForScope();
        bool deviceLost = false;
        bool deviceRestored = false;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });
        Rendering.RenderBackendGeneration initialGeneration = overlay.RenderBackendGeneration;
        long initialResourceGeneration = overlay.Resources.CurrentGeneration;
        overlay.DeviceLost += (_, args) =>
        {
            deviceLost = true;
            Assert.IsTrue(args.Reason.Contains("recreation", StringComparison.OrdinalIgnoreCase));
        };
        overlay.DeviceRestored += (_, args) =>
        {
            deviceRestored = true;
            Assert.IsTrue(args.Reason.Contains("recreated", StringComparison.OrdinalIgnoreCase));
        };

        await overlay.RecreateAsync();

        Assert.IsTrue(deviceLost);
        Assert.IsTrue(deviceRestored);
        Assert.AreEqual(initialGeneration.Next(), overlay.RenderBackendGeneration);
        Assert.AreEqual(initialResourceGeneration + 1, overlay.Resources.CurrentGeneration);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FrameStatsCaptureTimingCommandsAndBounds()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bounds = new WindowBounds(12, 24, 320, 180);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Bounds = bounds,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        overlay.Render += frame =>
        {
            frame.Clear(ColorRgba.Transparent);
            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(1, overlay.FrameStats.FrameCount);
        Assert.AreEqual(1, overlay.FrameStats.CommandCount);
        Assert.AreEqual(0, overlay.FrameStats.PrimitiveCount);
        Assert.AreEqual(0, overlay.FrameStats.TransientTextLayoutCount);
        Assert.AreEqual(0, overlay.FrameStats.NativeResourceCount);
        Assert.IsTrue(overlay.FrameStats.DpiScale.X > 0);
        Assert.IsTrue(overlay.FrameStats.DpiScale.Y > 0);
        Assert.AreEqual(bounds, overlay.FrameStats.WindowBounds);
        Assert.IsNull(overlay.FrameStats.TargetBounds);
        Assert.IsTrue(overlay.FrameStats.TargetFrameInterval > TimeSpan.Zero);
        Assert.IsTrue(overlay.FrameStats.LastFrameDuration >= TimeSpan.Zero);
        Assert.AreEqual(overlay.FrameStats.LastFrameDuration, overlay.FrameStats.MovingAverageFrameDuration);
        Assert.AreEqual(overlay.FrameStats.LastFrameDuration, overlay.FrameStats.WorstFrameDuration);
        Assert.AreEqual(0, overlay.FrameStats.CurrentFramesPerSecond);
        Assert.IsTrue(overlay.FrameStats.AverageFramesPerSecond >= 0);
        Assert.IsTrue(overlay.FrameStats.RenderDuration >= TimeSpan.Zero);
        Assert.IsTrue(overlay.FrameStats.PresentDuration >= TimeSpan.Zero);
        Assert.AreEqual(0, overlay.FrameStats.SkippedFrameCount);
        Assert.AreEqual(0, overlay.FrameStats.DroppedFrameCount);
        Assert.AreEqual(0, overlay.FrameStats.TargetTrackingUpdateCount);
        Assert.AreEqual(1, overlay.FrameStats.BackendGeneration);
        Assert.IsTrue(overlay.FrameStats.TargetHwnd.IsNull);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FrameStatsCaptureTransientTextLayoutCount()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            ExcessiveTextLayoutCreationThreshold = 1,
        });

        using FontHandle font = overlay.Resources.CreateFont(new FontOptions("Segoe UI", 12));
        using SolidBrushHandle brush = overlay.Resources.CreateSolidBrush(ColorRgba.White);
        overlay.Render += frame =>
        {
            frame.Draw.Text("first", font, brush, new PointF(0, 0));
            frame.Draw.Text("second", font, brush, new PointF(0, 20));
            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(2, overlay.FrameStats.TransientTextLayoutCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ContinueExceptionPolicyKeepsRenderingAfterCallbackFailure()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int renderAttempts = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            ExceptionPolicy = RenderExceptionPolicy.Continue,
        });

        overlay.Render += _ =>
        {
            renderAttempts++;
            if (renderAttempts == 1)
            {
                throw new InvalidOperationException("Expected test exception.");
            }

            runCancellation.Cancel();
        };

        await overlay.RunAsync(runCancellation.Token);

        Assert.IsGreaterThanOrEqualTo(renderAttempts, 2);
        Assert.AreEqual(1, overlay.FrameStats.FrameCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ResourceCreationDuringRenderCanBeRejected()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            RejectResourceCreationDuringRender = true,
        });

        overlay.Render += _ => overlay.Resources.CreateSolidBrush(ColorRgba.White);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await overlay.RunAsync(runCancellation.Token));
        Assert.AreEqual(0, overlay.Resources.CreateLeakReport().LiveCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InteractiveOverlayReceivesPointerPressEvents()
    {
        var pointerPressed = new TaskCompletionSource<OverlayPointerEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.PointerPressed += (_, args) => pointerPressed.TrySetResult(args);

        _ = SendMessage(overlay.Hwnd.Value, WmLButtonDown, 0, MakeLParam(12, 24));

        OverlayPointerEventArgs pointer = await pointerPressed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(OverlayPointerEventKind.Pressed, pointer.Kind);
        Assert.AreEqual(OverlayPointerButton.Left, pointer.Button);
        Assert.AreEqual(12, pointer.PixelX);
        Assert.AreEqual(24, pointer.PixelY);
        Assert.AreEqual(12f, pointer.Position.X);
        Assert.AreEqual(24f, pointer.Position.Y);
    }

    private const uint WmLButtonDown = 0x0201;

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    private static nint MakeLParam(short low, short high)
        => new(((high & 0xFFFF) << 16) | (low & 0xFFFF));
}
