namespace ModernOverlay.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TransparencyVisualTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TransparentClearShowsUnderlyingDesktopContent()
    {
        System.Drawing.Color backgroundColor = System.Drawing.Color.FromArgb(64, 160, 32);
        var backgroundBounds = new System.Drawing.Rectangle(80, 80, 360, 220);
        var overlayBounds = new WindowBounds(120, 120, 220, 120);
        var samplePoint = new System.Drawing.Point(overlayBounds.X + 40, overlayBounds.Y + 40);
        using TestBackgroundWindow background = TestBackgroundWindow.Create(backgroundBounds, backgroundColor);
        await WaitForScreenColorAsync(samplePoint, backgroundColor, tolerance: 18, TimeSpan.FromSeconds(2));
        using var renderCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int frames = 0;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = overlayBounds,
            IsVisible = true,
            InputMode = OverlayInputMode.ClickThrough,
            ZOrder = OverlayZOrder.TopMost,
            TransparencyMode = TransparencyMode.DwmGlassFrame,
            FrameRateLimit = FrameRateLimit.Fixed(60),
        });

        using SolidBrushHandle brush = overlay.Resources.CreateSolidBrush(ColorRgba.White);
        overlay.Render += frame =>
        {
            frame.Clear(ColorRgba.Transparent);
            frame.Draw.Crosshair(new PointF(180, 60), 18, brush, 2f);
            if (Interlocked.Increment(ref frames) >= 3)
            {
                rendered.TrySetResult();
            }
        };

        Task runTask = overlay.RunAsync(renderCancellation.Token).AsTask();
        try
        {
            await rendered.Task.WaitAsync(renderCancellation.Token);
            await Task.Delay(150, renderCancellation.Token);

            System.Drawing.Color sampled = CaptureScreenPixel(samplePoint.X, samplePoint.Y);

            AssertColorNear(backgroundColor, sampled, tolerance: 18);
        }
        finally
        {
            await overlay.StopAsync();
            await runTask;
        }
    }

    private static System.Drawing.Color CaptureScreenPixel(int x, int y)
    {
        using var bitmap = new System.Drawing.Bitmap(1, 1);
        using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(1, 1));
        return bitmap.GetPixel(0, 0);
    }

    private static async Task WaitForScreenColorAsync(
        System.Drawing.Point point,
        System.Drawing.Color expected,
        int tolerance,
        TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        System.Drawing.Color sampled;
        do
        {
            sampled = CaptureScreenPixel(point.X, point.Y);
            if (IsColorNear(expected, sampled, tolerance))
            {
                return;
            }

            await Task.Delay(50);
        }
        while (DateTime.UtcNow < deadline);

        AssertColorNear(expected, sampled, tolerance);
    }

    private static bool IsColorNear(System.Drawing.Color expected, System.Drawing.Color actual, int tolerance)
    {
        return Math.Abs(expected.R - actual.R) <= tolerance
            && Math.Abs(expected.G - actual.G) <= tolerance
            && Math.Abs(expected.B - actual.B) <= tolerance;
    }

    private static void AssertColorNear(System.Drawing.Color expected, System.Drawing.Color actual, int tolerance)
    {
        Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(expected.R - actual.R), $"Expected {expected}, sampled {actual}.");
        Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(expected.G - actual.G), $"Expected {expected}, sampled {actual}.");
        Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(expected.B - actual.B), $"Expected {expected}, sampled {actual}.");
    }

    private sealed class TestBackgroundWindow : IDisposable
    {
        private readonly nint hwnd;
        private bool disposed;

        private TestBackgroundWindow(nint hwnd)
        {
            this.hwnd = hwnd;
        }

        public static TestBackgroundWindow Create(System.Drawing.Rectangle bounds, System.Drawing.Color backgroundColor)
        {
            nint hwnd = CreateWindowEx(
                WsExTopMost | WsExToolWindow,
                "Static",
                "ModernOverlay transparency visual test background",
                WsPopup | WsVisible,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                0,
                0,
                0,
                0);
            Assert.AreNotEqual(0, hwnd);

            _ = ShowWindow(hwnd, SwShowNoActivate);
            _ = SetWindowPos(hwnd, HwndTopMost, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
            Paint(hwnd, bounds.Size, backgroundColor);

            return new TestBackgroundWindow(hwnd);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _ = DestroyWindow(hwnd);
        }

        private static void Paint(nint hwnd, System.Drawing.Size size, System.Drawing.Color color)
        {
            nint deviceContext = GetDC(hwnd);
            Assert.AreNotEqual(0, deviceContext);
            nint brush = CreateSolidBrush(ToColorRef(color));
            Assert.AreNotEqual(0, brush);
            try
            {
                var rect = new Rect(0, 0, size.Width, size.Height);
                Assert.AreNotEqual(0, FillRect(deviceContext, ref rect, brush));
            }
            finally
            {
                _ = DeleteObject(brush);
                _ = ReleaseDC(hwnd, deviceContext);
            }
        }

        private static uint ToColorRef(System.Drawing.Color color)
            => (uint)(color.R | (color.G << 8) | (color.B << 16));

        private const uint WsPopup = 0x80000000;
        private const uint WsVisible = 0x10000000;
        private const uint WsExTopMost = 0x00000008;
        private const uint WsExToolWindow = 0x00000080;
        private const int SwShowNoActivate = 4;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private static readonly nint HwndTopMost = new(-1);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "CreateWindowExW", ExactSpelling = true, SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern nint CreateWindowEx(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            nint parent,
            nint menu,
            nint instance,
            nint param);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "DestroyWindow", ExactSpelling = true, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ShowWindow", ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool ShowWindow(nint hwnd, int commandShow);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowPos", ExactSpelling = true, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetWindowPos(nint hwnd, nint hwndInsertAfter, int x, int y, int width, int height, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetDC", ExactSpelling = true)]
        private static extern nint GetDC(nint hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ReleaseDC", ExactSpelling = true)]
        private static extern int ReleaseDC(nint hwnd, nint hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "CreateSolidBrush", ExactSpelling = true)]
        private static extern nint CreateSolidBrush(uint color);

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "DeleteObject", ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DeleteObject(nint value);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "FillRect", ExactSpelling = true)]
        private static extern int FillRect(nint hdc, ref Rect rect, nint brush);

        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }
    }
}
