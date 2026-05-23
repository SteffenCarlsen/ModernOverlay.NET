using System.Runtime.InteropServices;
using ModernOverlay.Rendering;
using ModernOverlay.Win32;

namespace ModernOverlay.Tests;

[TestClass]
[DoNotParallelize]
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        Assert.AreEqual(Rendering.RenderBackendKind.Direct2DHwnd, overlay.RenderBackendKind);
        Assert.AreEqual("Direct2D HWND", overlay.BackendName);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task Direct2DBackendIsAutoDiscoveredWhenAssemblyIsAvailable()
    {
        using IDisposable registration = Rendering.RenderBackendRegistry.UseNullBackendForScope();

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
    public async Task BackendRecreateRequestRaisesDeviceEventsAndContinuesRendering()
    {
        var backend = new RecreateRequestBackend();
        using IDisposable registration = RenderBackendRegistry.RegisterForScope(new SingleBackendProvider(backend));
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int deviceLost = 0;
        int deviceRestored = 0;
        int renderAttempts = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });
        long initialResourceGeneration = overlay.Resources.CurrentGeneration;
        overlay.DeviceLost += (_, args) =>
        {
            deviceLost++;
            StringAssert.Contains(args.Reason, "test");
        };
        overlay.DeviceRestored += (_, args) =>
        {
            deviceRestored++;
            StringAssert.Contains(args.Reason, "recreated");
        };
        overlay.Render += _ =>
        {
            renderAttempts++;
            if (renderAttempts >= 2)
            {
                runCancellation.Cancel();
            }
        };

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(1, deviceLost);
        Assert.AreEqual(1, deviceRestored);
        Assert.AreEqual(1, overlay.FrameStats.FrameCount);
        Assert.AreEqual(initialResourceGeneration + 1, overlay.Resources.CurrentGeneration);
        Assert.AreEqual(RenderBackendGeneration.Initial.Next(), backend.Generation);
        Assert.IsTrue(renderAttempts >= 2);
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
        Assert.AreEqual(overlay.FrameStats.LastFrameDuration, overlay.FrameStats.ActualFrameInterval);
        Assert.IsTrue(double.IsFinite(overlay.FrameStats.CurrentFramesPerSecond));
        Assert.IsTrue(overlay.FrameStats.CurrentFramesPerSecond >= 0);
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
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
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            RejectResourceCreationDuringRender = true,
        });

        overlay.Render += _ => overlay.Resources.CreateSolidBrush(ColorRgba.White);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await overlay.RunAsync(runCancellation.Token));
        Assert.AreEqual(0, overlay.Resources.CreateLeakReport().LiveCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task HiddenOverlayPausesRenderingByDefault()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        int renderAttempts = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        overlay.Render += _ => renderAttempts++;

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(0, renderAttempts);
        Assert.AreEqual(0, overlay.FrameStats.FrameCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task PauseSuppressesRenderingEvenWhenHiddenRenderingContinues()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        int renderAttempts = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        overlay.Render += _ => renderAttempts++;
        overlay.Pause();

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(0, renderAttempts);
        Assert.AreEqual(0, overlay.FrameStats.FrameCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task StopAsyncStopsCurrentRunWithoutDisposingOverlayLifetime()
    {
        int renderAttempts = 0;
        int loaded = 0;
        int unloaded = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        overlay.Loaded += _ => loaded++;
        overlay.Unloaded += _ => unloaded++;
        overlay.Render += _ =>
        {
            renderAttempts++;
            ValueTask stopTask = overlay.StopAsync();
            Assert.IsTrue(stopTask.IsCompletedSuccessfully);
        };

        await overlay.RunAsync();
        await overlay.RunAsync();

        Assert.AreEqual(2, renderAttempts);
        Assert.AreEqual(2, loaded);
        Assert.AreEqual(2, unloaded);
        Assert.AreEqual(2, overlay.FrameStats.FrameCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ShowHideMoveAndResizeUpdateNativeWindowAndRaiseEvents()
    {
        var initialBounds = new WindowBounds(30, 40, 180, 100);
        var movedBounds = new WindowBounds(70, 90, 240, 140);
        int visibilityChanges = 0;
        int boundsChanges = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = initialBounds,
            IsVisible = false,
        });
        overlay.VisibilityChanged += (_, _) => visibilityChanges++;
        overlay.BoundsChanged += (_, _) => boundsChanges++;

        Assert.IsFalse(Win32WindowQuery.IsVisible(overlay.Hwnd.Value));

        await overlay.ShowAsync();
        Assert.IsTrue(Win32WindowQuery.IsVisible(overlay.Hwnd.Value));

        await overlay.HideAsync();
        Assert.IsFalse(Win32WindowQuery.IsVisible(overlay.Hwnd.Value));

        overlay.MovePixels(movedBounds.X, movedBounds.Y);
        overlay.ResizePixels(movedBounds.Width, movedBounds.Height);

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds nativeBounds));
        Assert.AreEqual(movedBounds.X, nativeBounds.X);
        Assert.AreEqual(movedBounds.Y, nativeBounds.Y);
        Assert.AreEqual(movedBounds.Width, nativeBounds.Width);
        Assert.AreEqual(movedBounds.Height, nativeBounds.Height);
        Assert.AreEqual(2, visibilityChanges);
        Assert.AreEqual(2, boundsChanges);
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

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InteractiveOverlayReceivesPointerWheelEvents()
    {
        var pointerWheel = new TaskCompletionSource<OverlayPointerEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bounds = new WindowBounds(40, 50, 160, 120);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = bounds,
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.PointerWheel += (_, args) => pointerWheel.TrySetResult(args);

        const int wheelDelta = 120;
        nint wheelWParam = MakeWheelWParam(wheelDelta);
        _ = SendMessage(overlay.Hwnd.Value, WmMouseWheel, (nuint)wheelWParam, MakeLParam((short)(bounds.X + 12), (short)(bounds.Y + 24)));

        OverlayPointerEventArgs pointer = await pointerWheel.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(OverlayPointerEventKind.Wheel, pointer.Kind);
        Assert.AreEqual(OverlayPointerButton.None, pointer.Button);
        Assert.AreEqual(12, pointer.PixelX);
        Assert.AreEqual(24, pointer.PixelY);
        Assert.AreEqual(12f, pointer.Position.X);
        Assert.AreEqual(24f, pointer.Position.Y);
        Assert.AreEqual(wheelDelta, pointer.WheelDelta);
        Assert.IsFalse(pointer.IsHorizontalWheel);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InteractiveOverlayReceivesHorizontalPointerWheelEvents()
    {
        var pointerWheel = new TaskCompletionSource<OverlayPointerEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bounds = new WindowBounds(50, 60, 160, 120);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = bounds,
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.PointerWheel += (_, args) => pointerWheel.TrySetResult(args);

        const int wheelDelta = -120;
        nint wheelWParam = MakeWheelWParam(wheelDelta);
        _ = SendMessage(overlay.Hwnd.Value, WmMouseHWheel, (nuint)wheelWParam, MakeLParam((short)(bounds.X + 14), (short)(bounds.Y + 28)));

        OverlayPointerEventArgs pointer = await pointerWheel.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(OverlayPointerEventKind.Wheel, pointer.Kind);
        Assert.AreEqual(14, pointer.PixelX);
        Assert.AreEqual(28, pointer.PixelY);
        Assert.AreEqual(wheelDelta, pointer.WheelDelta);
        Assert.IsTrue(pointer.IsHorizontalWheel);
    }

    private const uint WmLButtonDown = 0x0201;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmMouseHWheel = 0x020E;

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    private static nint MakeLParam(short low, short high)
        => new(((high & 0xFFFF) << 16) | (low & 0xFFFF));

    private static nint MakeWheelWParam(int delta)
        => new((delta & 0xFFFF) << 16);

    private sealed class SingleBackendProvider(IRenderBackend backend) : IRenderBackendProvider
    {
        public IRenderBackend CreateBackend(OverlayWindowOptions options) => backend;
    }

    private sealed class RecreateRequestBackend : IRenderBackend
    {
        private readonly NullRenderBackend inner = new();
        private bool recreateRequested;

        public RenderBackendKind Kind => inner.Kind;

        public RenderBackendGeneration Generation => inner.Generation;

        public IDrawCommandSink CommandSink => inner.CommandSink;

        public IBackendResourceFactory Resources => inner.Resources;

        public void Initialize(RenderBackendInitializeContext context) => inner.Initialize(context);

        public void Resize(PixelSize size, DpiScale dpi) => inner.Resize(size, dpi);

        public void Recreate(RenderBackendInitializeContext context) => inner.Recreate(context);

        public BeginFrameResult BeginFrame(in FrameInfo frameInfo) => inner.BeginFrame(frameInfo);

        public EndFrameResult EndFrame()
        {
            if (!recreateRequested)
            {
                recreateRequested = true;
                return EndFrameResult.RecreateTarget("test backend requested recreation");
            }

            return inner.EndFrame();
        }

        public void Clear(ColorRgba color) => inner.Clear(color);

        public void SetQuality(RenderQualityOptions quality) => inner.SetQuality(quality);

        public void SetPresentMode(PresentMode presentMode) => inner.SetPresentMode(presentMode);

        public void Dispose() => inner.Dispose();
    }
}
