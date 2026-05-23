using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using ModernOverlay.Diagnostics;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class OverlayEventSourceTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayLifecycleAndFramesEmitEventSourceEvents()
    {
        using var listener = new RecordingOverlayEventListener();
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            HiddenRenderPolicy = HiddenRenderPolicy.Continue,
            FrameRateLimit = FrameRateLimit.Fixed(120),
        });

        try
        {
            overlay.Render += frame =>
            {
                frame.Clear(ColorRgba.Transparent);
                runCancellation.Cancel();
            };

            await overlay.RunAsync(runCancellation.Token);
        }
        finally
        {
            await overlay.DisposeAsync();
        }

        Assert.IsTrue(listener.WaitForEvent("OverlayCreated"));
        Assert.IsTrue(listener.WaitForEvent("BackendInitialized"));
        Assert.IsTrue(listener.WaitForEvent("FrameRendered"));
        Assert.IsTrue(listener.WaitForEvent("BackendDisposed"));
        Assert.IsTrue(listener.WaitForEvent("OverlayDestroyed"));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RecreateEmitsDeviceEventSourceEvents()
    {
        using var listener = new RecordingOverlayEventListener();
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
        });

        await overlay.RecreateAsync();

        Assert.IsTrue(listener.WaitForEvent("DeviceLost"));
        Assert.IsTrue(listener.WaitForEvent("DeviceRestored"));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task UnsupportedTransparencyModesFallbackAndEmitDiagnostics()
    {
        using var listener = new RecordingOverlayEventListener();
        await using OverlayWindow updateLayeredOverlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            TransparencyMode = TransparencyMode.UpdateLayeredWindow,
        });
        await using OverlayWindow directCompositionOverlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            TransparencyMode = TransparencyMode.DirectComposition,
        });

        Assert.IsFalse(updateLayeredOverlay.Hwnd.IsNull);
        Assert.IsFalse(directCompositionOverlay.Hwnd.IsNull);
        Assert.IsTrue(listener.WaitForEvent("BackendFallback"));
    }

    [TestMethod]
    public void ResourceLeakReportsEmitEventSourceEvent()
    {
        using var listener = new RecordingOverlayEventListener();
        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);

        _ = resources.CreateLeakReport();

        Assert.IsTrue(listener.WaitForEvent("ResourceLeakDetected"));
    }

    [TestMethod]
    public void OverlayEventSourceLoggerForwardsEventsToLogger()
    {
        var logger = new RecordingLogger();
        using var adapter = new OverlayEventSourceLogger(logger, EventLevel.Verbose);

        OverlayEventSource.Log.RenderException("UnitTestException", "Expected message.");

        Assert.IsTrue(logger.WaitForMessage("RenderException"));
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("UnitTestException", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ExcessiveTextLayoutCreationEmitsEventSourceEvent()
    {
        using var listener = new RecordingOverlayEventListener();

        OverlayEventSource.Log.ExcessiveTextLayoutCreation(1, 4, 2);

        Assert.IsTrue(listener.WaitForEvent("ExcessiveTextLayoutCreation"));
    }

    [TestMethod]
    public void UiDiagnosticsEmitEventSourceEvents()
    {
        using var listener = new RecordingOverlayEventListener();

        OverlayEventSource.Log.UiLayoutLoop(8, 3);
        OverlayEventSource.Log.UiInvalidPlacement("Window", "Cursor", "Missing cursor position.");
        OverlayEventSource.Log.UiUnhandledException("Render", "UnitTestException", "Expected message.");
        OverlayEventSource.Log.UiResourceRealizationFailure("Theme.Font", "UnitTestException", "Expected message.");

        Assert.IsTrue(listener.WaitForEvent("UiLayoutLoop"));
        Assert.IsTrue(listener.WaitForEvent("UiInvalidPlacement"));
        Assert.IsTrue(listener.WaitForEvent("UiUnhandledException"));
        Assert.IsTrue(listener.WaitForEvent("UiResourceRealizationFailure"));
    }

    private sealed class RecordingOverlayEventListener : EventListener
    {
        private readonly ConcurrentQueue<string> eventNames = new();

        public bool WaitForEvent(string eventName)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (eventNames.Contains(eventName))
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return eventNames.Contains(eventName);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "ModernOverlay")
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == "ModernOverlay" && eventData.EventName is not null)
            {
                eventNames.Enqueue(eventData.EventName);
            }
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyCollection<string> Messages => messages.ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
        }

        public bool WaitForMessage(string value)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (messages.Any(message => message.Contains(value, StringComparison.Ordinal)))
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return messages.Any(message => message.Contains(value, StringComparison.Ordinal));
        }
    }
}
