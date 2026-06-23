using System.Reflection;
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
    public async Task HiddenUnlimitedOverlayPausesWithoutRendering()
    {
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        int renderAttempts = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Unlimited,
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
        List<WindowBounds> visibilityBounds = [];
        List<WindowBounds> boundsEvents = [];

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = initialBounds,
            IsVisible = false,
        });
        overlay.VisibilityChanged += (_, args) => visibilityBounds.Add(args.Bounds);
        overlay.BoundsChanged += (_, args) => boundsEvents.Add(args.Bounds);

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
        CollectionAssert.AreEqual(new[] { initialBounds, initialBounds }, visibilityBounds);
        CollectionAssert.AreEqual(new[] { initialBounds with { X = movedBounds.X, Y = movedBounds.Y }, movedBounds }, boundsEvents);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task DisposeAsyncRaisesDisposedEventOnce()
    {
        OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
        });
        int disposed = 0;
        overlay.Disposed += sender =>
        {
            Assert.AreSame(overlay, sender);
            disposed++;
        };

        try
        {
            await overlay.DisposeAsync();
            await overlay.DisposeAsync();

            Assert.AreEqual(1, disposed);
        }
        finally
        {
            await overlay.DisposeAsync();
        }
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
    public async Task InteractiveOverlayReceivesPointerMoveAndReleaseEvents()
    {
        var pointerMoved = new TaskCompletionSource<OverlayPointerEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pointerReleased = new TaskCompletionSource<OverlayPointerEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.PointerMoved += (_, args) => pointerMoved.TrySetResult(args);
        overlay.PointerReleased += (_, args) => pointerReleased.TrySetResult(args);

        _ = SendMessage(overlay.Hwnd.Value, WmMouseMove, 0, MakeLParam(15, 25));
        _ = SendMessage(overlay.Hwnd.Value, WmLButtonUp, 0, MakeLParam(16, 26));

        OverlayPointerEventArgs moved = await pointerMoved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OverlayPointerEventArgs released = await pointerReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        DpiScale dpi = overlay.DpiScale;

        Assert.AreEqual(OverlayPointerEventKind.Moved, moved.Kind);
        Assert.AreEqual(OverlayPointerButton.None, moved.Button);
        Assert.AreEqual(15, moved.PixelX);
        Assert.AreEqual(25, moved.PixelY);
        Assert.AreEqual(15f / dpi.X, moved.Position.X, 0.001f);
        Assert.AreEqual(25f / dpi.Y, moved.Position.Y, 0.001f);

        Assert.AreEqual(OverlayPointerEventKind.Released, released.Kind);
        Assert.AreEqual(OverlayPointerButton.Left, released.Button);
        Assert.AreEqual(16, released.PixelX);
        Assert.AreEqual(26, released.PixelY);
        Assert.AreEqual(16f / dpi.X, released.Position.X, 0.001f);
        Assert.AreEqual(26f / dpi.Y, released.Position.Y, 0.001f);
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
        Assert.AreEqual(OverlayPointerButton.None, pointer.Button);
        Assert.AreEqual(14, pointer.PixelX);
        Assert.AreEqual(28, pointer.PixelY);
        Assert.AreEqual(14f, pointer.Position.X);
        Assert.AreEqual(28f, pointer.Position.Y);
        Assert.AreEqual(wheelDelta, pointer.WheelDelta);
        Assert.IsTrue(pointer.IsHorizontalWheel);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayKeyboardAndTextEventsCopyNativePayloadValues()
    {
        List<OverlayKeyboardEventArgs> keyPressed = [];
        List<OverlayKeyboardEventArgs> keyReleased = [];
        List<OverlayTextInputEventArgs> textInput = [];

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
        });
        overlay.KeyPressed += (_, args) => keyPressed.Add(args);
        overlay.KeyReleased += (_, args) => keyReleased.Add(args);
        overlay.TextInput += (_, args) => textInput.Add(args);

        DispatchKeyboard(overlay, new Win32KeyboardEvent(
            VirtualKey: 0x70,
            IsPressed: true,
            IsSystemKey: true,
            RepeatCount: 3,
            ScanCode: 0x3B,
            IsExtendedKey: true,
            WasDown: true,
            IsTransitionState: false,
            Modifiers: Win32ModifierKeys.Control | Win32ModifierKeys.Shift));
        DispatchKeyboard(overlay, new Win32KeyboardEvent(
            VirtualKey: 0x70,
            IsPressed: false,
            IsSystemKey: true,
            RepeatCount: 1,
            ScanCode: 0x3B,
            IsExtendedKey: true,
            WasDown: true,
            IsTransitionState: true,
            Modifiers: Win32ModifierKeys.Alt));
        DispatchTextInput(overlay, new Win32TextInputEvent("ø", true));

        Assert.AreEqual(1, keyPressed.Count);
        OverlayKeyboardEventArgs pressed = keyPressed[0];
        Assert.AreEqual(0x70, pressed.VirtualKey);
        Assert.IsTrue(pressed.IsSystemKey);
        Assert.AreEqual(3, pressed.RepeatCount);
        Assert.AreEqual(0x3B, pressed.ScanCode);
        Assert.IsTrue(pressed.IsExtendedKey);
        Assert.IsTrue(pressed.WasDown);
        Assert.IsFalse(pressed.IsTransitionState);
        Assert.IsTrue(pressed.IsRepeat);
        Assert.AreEqual(OverlayModifierKeys.Control | OverlayModifierKeys.Shift, pressed.Modifiers);

        Assert.AreEqual(1, keyReleased.Count);
        OverlayKeyboardEventArgs released = keyReleased[0];
        Assert.AreEqual(0x70, released.VirtualKey);
        Assert.IsTrue(released.IsSystemKey);
        Assert.AreEqual(1, released.RepeatCount);
        Assert.AreEqual(0x3B, released.ScanCode);
        Assert.IsTrue(released.IsExtendedKey);
        Assert.IsTrue(released.WasDown);
        Assert.IsTrue(released.IsTransitionState);
        Assert.IsFalse(released.IsRepeat);
        Assert.AreEqual(OverlayModifierKeys.Alt, released.Modifiers);

        Assert.AreEqual(1, textInput.Count);
        Assert.AreEqual("ø", textInput[0].Text);
        Assert.IsTrue(textInput[0].IsSystemCharacter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task SelectiveClickThroughNcHitTestUsesInputRegionResolver()
    {
        var resolver = new RecordingInputRegionResolver(point => point.X < 50f);
        var bounds = new WindowBounds(100, 120, 160, 90);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = bounds,
            IsVisible = false,
            InputMode = OverlayInputMode.SelectiveClickThrough,
        });
        overlay.SetInputRegionResolver(resolver);

        nint interactive = SendMessage(overlay.Hwnd.Value, WmNcHitTest, 0, MakeLParam(125, 150));
        nint passThrough = SendMessage(overlay.Hwnd.Value, WmNcHitTest, 0, MakeLParam(175, 150));

        Assert.AreEqual(new nint(HtClient), interactive);
        Assert.AreEqual(new nint(HtTransparent), passThrough);
        CollectionAssert.AreEqual(new[] { new PointF(25f, 30f), new PointF(75f, 30f) }, resolver.Points);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task ClickThroughNcHitTestReturnsTransparent()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = new WindowBounds(100, 120, 160, 90),
            IsVisible = false,
            InputMode = OverlayInputMode.ClickThrough,
        });

        nint passThrough = SendMessage(overlay.Hwnd.Value, WmNcHitTest, 0, MakeLParam(125, 150));

        Assert.AreEqual(new nint(HtTransparent), passThrough);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task InteractiveOverlayReceivesKeyboardAndTextInputEvents()
    {
        var keyPressed = new TaskCompletionSource<OverlayKeyboardEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var keyReleased = new TaskCompletionSource<OverlayKeyboardEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var textInput = new TaskCompletionSource<OverlayTextInputEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.KeyPressed += (_, args) => keyPressed.TrySetResult(args);
        overlay.KeyReleased += (_, args) => keyReleased.TrySetResult(args);
        overlay.TextInput += (_, args) => textInput.TrySetResult(args);

        _ = SendMessage(overlay.Hwnd.Value, WmKeyDown, (nuint)'A', MakeKeyLParam(repeatCount: 2, scanCode: 0x1E, wasDown: true, transition: false));
        _ = SendMessage(overlay.Hwnd.Value, WmKeyUp, (nuint)'A', MakeKeyLParam(repeatCount: 1, scanCode: 0x1E, wasDown: true, transition: true));
        _ = SendMessage(overlay.Hwnd.Value, WmChar, (nuint)'Å', 1);

        OverlayKeyboardEventArgs pressed = await keyPressed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OverlayKeyboardEventArgs released = await keyReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OverlayTextInputEventArgs text = await textInput.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual('A', pressed.VirtualKey);
        Assert.AreEqual(2, pressed.RepeatCount);
        Assert.AreEqual(0x1E, pressed.ScanCode);
        Assert.IsTrue(pressed.WasDown);
        Assert.IsTrue(pressed.IsRepeat);

        Assert.AreEqual('A', released.VirtualKey);
        Assert.AreEqual(1, released.RepeatCount);
        Assert.IsTrue(released.WasDown);
        Assert.IsTrue(released.IsTransitionState);

        Assert.AreEqual("Å", text.Text);
        Assert.IsFalse(text.IsSystemCharacter);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task KeyboardLParamHighBitDoesNotOverflowOn64Bit()
    {
        var keyReleased = new TaskCompletionSource<OverlayKeyboardEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            InputMode = OverlayInputMode.Interactive,
        });
        overlay.KeyReleased += (_, args) => keyReleased.TrySetResult(args);

        _ = SendMessage(overlay.Hwnd.Value, WmKeyUp, (nuint)'A', new nint(0xC01E0001L));

        OverlayKeyboardEventArgs released = await keyReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual('A', released.VirtualKey);
        Assert.AreEqual(1, released.RepeatCount);
        Assert.AreEqual(0x1E, released.ScanCode);
        Assert.IsTrue(released.WasDown);
        Assert.IsTrue(released.IsTransitionState);
    }

    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmMouseMove = 0x0200;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmChar = 0x0102;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmMouseHWheel = 0x020E;
    private const int HtTransparent = -1;
    private const int HtClient = 1;

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    private static nint MakeLParam(short low, short high)
        => new(((high & 0xFFFF) << 16) | (low & 0xFFFF));

    private static nint MakeWheelWParam(int delta)
        => new((delta & 0xFFFF) << 16);

    private static nint MakeKeyLParam(int repeatCount, int scanCode, bool wasDown, bool transition)
    {
        int value = repeatCount & 0xFFFF;
        value |= (scanCode & 0xFF) << 16;
        if (wasDown)
        {
            value |= 1 << 30;
        }

        if (transition)
        {
            value |= 1 << 31;
        }

        return new(value);
    }

    private static void DispatchKeyboard(OverlayWindow overlay, Win32KeyboardEvent keyboard)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleKeyboardEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleKeyboardEvent");
        method.Invoke(overlay, [keyboard]);
    }

    private static void DispatchTextInput(OverlayWindow overlay, Win32TextInputEvent textInput)
    {
        MethodInfo method = typeof(OverlayWindow).GetMethod("HandleTextInputEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(OverlayWindow), "HandleTextInputEvent");
        method.Invoke(overlay, [textInput]);
    }

    private sealed class SingleBackendProvider(IRenderBackend backend) : IRenderBackendProvider
    {
        public IRenderBackend CreateBackend(OverlayWindowOptions options) => backend;
    }

    private sealed class RecordingInputRegionResolver(Func<PointF, bool> predicate) : IOverlayInputRegionResolver
    {
        public List<PointF> Points { get; } = [];

        public OverlayInputRegionResult ResolveInputRegion(PointF position)
        {
            Points.Add(position);
            return predicate(position)
                ? OverlayInputRegionResult.Interactive
                : OverlayInputRegionResult.PassThrough;
        }
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
