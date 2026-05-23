using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using ModernOverlay.Direct2D;
using ModernOverlay.Rendering;
using ModernOverlay.Win32;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class Direct2DRenderBackendTests
{
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
        0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    private static readonly byte[] TinyJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDi6KKK+ZP3E//Z");

    private static readonly byte[] TinyBmp =
    [
        0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00,
        0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00,
        0x00, 0x00, 0x13, 0x0B, 0x00, 0x00, 0x13, 0x0B, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF,
    ];

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void BackendTracksLifecycleState()
    {
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();

        window.InvokeOnOwnerThread(() =>
        {
            backend.Initialize(new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.BackendDefault));

            BeginFrameResult begin = backend.BeginFrame(CreateFrameInfo());
            backend.Clear(ColorRgba.Transparent);
            EndFrameResult end = backend.EndFrame();

            Assert.IsTrue(begin.CanRender);
            Assert.IsTrue(end.Presented);
            Assert.AreEqual(RenderBackendKind.Direct2DHwnd, backend.Kind);
            Assert.AreEqual(new PixelSize(640, 360), backend.CurrentPixelSize);
            backend.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void ResizeAdvancesBackendGeneration()
    {
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();

        window.InvokeOnOwnerThread(() =>
        {
            backend.Initialize(new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.BackendDefault));
            RenderBackendGeneration initial = backend.Generation;

            backend.Resize(new PixelSize(800, 600), DpiScale.Default);

            Assert.AreEqual(initial.Next(), backend.Generation);
            Assert.AreEqual(new PixelSize(800, 600), backend.CurrentPixelSize);
            backend.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void VSyncPresentModeWarnsOnceAndFallsBackToBackendDefault()
    {
        using var listener = new RecordingOverlayEventListener();
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();

        window.InvokeOnOwnerThread(() =>
        {
            backend.Initialize(new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.VSync));

            Assert.AreEqual(PresentMode.VSync, backend.PresentMode);
            Assert.AreEqual(PresentMode.BackendDefault, backend.EffectivePresentMode);

            backend.SetPresentMode(PresentMode.VSync);
            backend.SetPresentMode(PresentMode.Immediate);
            Assert.AreEqual(PresentMode.Immediate, backend.EffectivePresentMode);
            backend.Dispose();
        });

        Assert.IsTrue(listener.WaitForBackendFallback(nameof(PresentMode)));
        Assert.AreEqual(1, listener.CountBackendFallbacks(nameof(PresentMode)));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void RecreateDisposesCachedNativeResourcesAndAdvancesGeneration()
    {
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();
        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);

        window.InvokeOnOwnerThread(() =>
        {
            var context = new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.BackendDefault);
            backend.Initialize(context);
            RenderBackendGeneration initial = backend.Generation;

            _ = backend.BeginFrame(CreateFrameInfo());
            backend.CommandSink.DrawLine(new PointF(0, 0), new PointF(20, 20), brush, 1f, null);
            _ = backend.EndFrame();
            Assert.IsGreaterThan(0, backend.CommandSink.NativeResourceCount);
            Assert.AreEqual(1, brush.NativeRealizationCount);

            backend.Recreate(context);

            Assert.AreEqual(initial.Next(), backend.Generation);
            Assert.AreEqual(0, backend.CommandSink.NativeResourceCount);
            Assert.AreEqual(0, brush.NativeRealizationCount);
            _ = backend.BeginFrame(CreateFrameInfo());
            backend.CommandSink.DrawLine(new PointF(0, 0), new PointF(20, 20), brush, 1f, null);
            _ = backend.EndFrame();
            Assert.IsGreaterThan(0, backend.CommandSink.NativeResourceCount);
            Assert.AreEqual(1, brush.NativeRealizationCount);
            backend.Dispose();
            Assert.AreEqual(0, brush.NativeRealizationCount);
        });
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void EndFrameCanReportRenderTargetRecreationRequest()
    {
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();

        window.InvokeOnOwnerThread(() =>
        {
            backend.Initialize(new RenderBackendInitializeContext(
                new WindowHandle(window.Hwnd),
                new PixelSize(640, 360),
                DpiScale.Default,
                RenderQualityOptions.Default,
                PresentMode.BackendDefault));
            backend.SimulateRecreateTargetOnNextEndFrame = true;

            _ = backend.BeginFrame(CreateFrameInfo());
            backend.Clear(ColorRgba.Transparent);
            EndFrameResult end = backend.EndFrame();

            Assert.IsFalse(end.Presented);
            Assert.IsTrue(end.RequiresRecreate);
            StringAssert.Contains(end.RecreateReason, "recreation");
            backend.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void PrimitiveCommandsRenderThroughDirect2DSink()
    {
        using Win32OverlayWindow window = CreateHiddenWindow();
        var backend = new Direct2DRenderBackend();
        var resources = new OverlayResourceManager();
        using SolidBrushHandle brush = resources.CreateSolidBrush(ColorRgba.White);
        using LinearGradientBrushHandle gradient = resources.CreateLinearGradientBrush(new LinearGradientBrushOptions(
            new PointF(0, 0),
            new PointF(100, 0),
            [
                new GradientStop(0f, ColorRgba.Black),
                new GradientStop(1f, ColorRgba.White),
            ]));
        using FontHandle font = resources.CreateFont(new FontOptions("Segoe UI", 16));
        using StrokeStyleHandle dashed = resources.CreateStrokeStyle(new StrokeStyleOptions { DashStyle = StrokeDashStyle.Dash });
        string imagePath = WriteTinyPng();
        using ImageHandle image = resources.CreateImage(imagePath);
        using ImageHandle byteImage = resources.CreateImage(TinyPng);
        using ImageHandle jpegImage = resources.CreateImage(TinyJpeg);
        using ImageHandle bmpImage = resources.CreateImage(TinyBmp);
        using var tinyPngStream = new MemoryStream(TinyPng);
        using ImageHandle streamImage = resources.CreateImage(tinyPngStream);
        using GeometryPath geometry = resources.CreateGeometry(path => path
            .MoveTo(new PointF(180, 40))
            .BezierTo(new PointF(200, 20), new PointF(230, 60), new PointF(220, 80))
            .QuadraticBezierTo(new PointF(190, 96), new PointF(160, 80))
            .ArcTo(new PointF(190, 45), new SizeF(28, 18), 20f, GeometrySweepDirection.Clockwise, GeometryArcSize.Small)
            .LineTo(new PointF(160, 80))
            .Close());
        using TextLayoutHandle textLayout = resources.CreateTextLayout("Layout", font, new TextLayoutOptions
        {
            MaxWidth = 96,
            MaxHeight = 32,
            Wrapping = TextWrapping.NoWrap,
            HorizontalAlignment = TextHorizontalAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Center,
            Trimming = TextTrimming.Character,
        });

        try
        {
            window.InvokeOnOwnerThread(() =>
            {
                backend.Initialize(new RenderBackendInitializeContext(
                    new WindowHandle(window.Hwnd),
                    new PixelSize(640, 360),
                    DpiScale.Default,
                    RenderQualityOptions.Default,
                    PresentMode.BackendDefault));

                _ = backend.BeginFrame(CreateFrameInfo());
                backend.CommandSink.DrawLine(new PointF(0, 0), new PointF(20, 20), brush, 1f, null);
                backend.CommandSink.DrawRectangle(new RectF(10, 10, 40, 30), brush, 2f, dashed);
                backend.CommandSink.FillRectangle(new RectF(20, 20, 20, 20), brush);
                backend.CommandSink.FillRectangle(new RectF(48, 20, 20, 20), gradient);
                backend.CommandSink.DrawRoundedRectangle(new RectF(30, 30, 60, 40), 4f, 4f, brush, 1f, null);
                backend.CommandSink.FillRoundedRectangle(new RectF(40, 40, 30, 30), 4f, 4f, brush);
                backend.CommandSink.DrawEllipse(new RectF(50, 50, 30, 20), brush, 1f, null);
                backend.CommandSink.FillCircle(new PointF(80, 80), 8f, brush);
                backend.CommandSink.DrawTriangle(new PointF(100, 40), new PointF(120, 80), new PointF(80, 80), brush, 1f, null);
                backend.CommandSink.FillTriangle(new PointF(140, 40), new PointF(160, 80), new PointF(120, 80), brush);
                backend.CommandSink.DrawGeometry(geometry, brush, 1f, dashed);
                backend.CommandSink.FillGeometry(geometry, brush);
                backend.CommandSink.DrawImage(image, 0, new RectF(240, 40, 24, 24), new RectF(0, 0, 1, 1), 0.5f, ImageInterpolationMode.NearestNeighbor);
                backend.CommandSink.DrawImage(byteImage, 0, new RectF(272, 40, 24, 24), null, 1f, ImageInterpolationMode.Linear);
                backend.CommandSink.DrawImage(streamImage, 0, new RectF(304, 40, 24, 24), null, 1f, ImageInterpolationMode.Linear);
                backend.CommandSink.DrawImage(jpegImage, 0, new RectF(336, 40, 24, 24), null, 1f, ImageInterpolationMode.Linear);
                backend.CommandSink.DrawImage(bmpImage, 0, new RectF(368, 40, 24, 24), null, 1f, ImageInterpolationMode.Linear);
                backend.CommandSink.DrawText("DirectWrite", font, brush, new PointF(20, 120));
                backend.CommandSink.DrawTextLayout(textLayout, brush, new PointF(20, 150));
                EndFrameResult end = backend.EndFrame();

                Assert.IsTrue(end.Presented);
                Assert.AreEqual(19, ((Direct2DDrawCommandSink)backend.CommandSink).CommandCount);
                Assert.AreEqual(19, backend.CommandSink.PrimitiveCount);
                Assert.IsGreaterThan(0, backend.CommandSink.NativeResourceCount);
                Assert.IsGreaterThan(0, brush.NativeRealizationCount);
                Assert.IsGreaterThan(0, image.NativeRealizationCount);
                Assert.AreEqual(2, geometry.NativeRealizationCount);
                int nativeResourceCountAfterFirstFrame = backend.CommandSink.NativeResourceCount;

                _ = backend.BeginFrame(CreateFrameInfo());
                backend.CommandSink.DrawGeometry(geometry, brush, 1f, dashed);
                backend.CommandSink.FillGeometry(geometry, brush);
                _ = backend.EndFrame();

                Assert.AreEqual(nativeResourceCountAfterFirstFrame, backend.CommandSink.NativeResourceCount);
                backend.Dispose();
                Assert.AreEqual(0, brush.NativeRealizationCount);
                Assert.AreEqual(0, image.NativeRealizationCount);
                Assert.AreEqual(0, geometry.NativeRealizationCount);
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string WriteTinyPng()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ModernOverlay_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, TinyPng);
        return path;
    }

    private static FrameInfo CreateFrameInfo()
        => new(
            1,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1d / 60d),
            TimeSpan.Zero,
            Environment.CurrentManagedThreadId,
            DpiScale.Default,
            new WindowBounds(0, 0, 640, 360),
            null);

    private static Win32OverlayWindow CreateHiddenWindow()
        => Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay Direct2D backend test",
            X: 0,
            Y: 0,
            Width: 640,
            Height: 360,
            ClickThrough: true,
            TopMost: false,
            ToolWindow: true));

    private sealed class RecordingOverlayEventListener : EventListener
    {
        private readonly ConcurrentQueue<string> eventNames = new();
        private readonly ConcurrentQueue<string> backendFallbackFeatures = new();

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

        public bool WaitForBackendFallback(string feature)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (backendFallbackFeatures.Contains(feature))
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return backendFallbackFeatures.Contains(feature);
        }

        public int CountBackendFallbacks(string feature)
            => backendFallbackFeatures.Count(value => string.Equals(value, feature, StringComparison.Ordinal));

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
                if (eventData.EventName == "BackendFallback" && eventData.Payload?.Count >= 2 && eventData.Payload[1] is string feature)
                {
                    backendFallbackFeatures.Enqueue(feature);
                }
            }
        }
    }
}
