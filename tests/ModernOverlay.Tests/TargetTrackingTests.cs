using System.Diagnostics;
using ModernOverlay.Win32;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class TargetTrackingTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayUsesTargetWindowBoundsAtCreation()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(25, 35, 320, 180);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)),
        });

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(25, bounds.X);
        Assert.AreEqual(35, bounds.Y);
        Assert.AreEqual(320, bounds.Width);
        Assert.AreEqual(180, bounds.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlaySyncsTargetWindowBoundsBeforeRendering()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(10, 20, 200, 120);
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)),
        });

        target.SetBounds(40, 50, 240, 160);
        overlay.Render += _ => runCancellation.Cancel();

        await overlay.RunAsync(runCancellation.Token);

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(40, bounds.X);
        Assert.AreEqual(50, bounds.Y);
        Assert.AreEqual(240, bounds.Width);
        Assert.AreEqual(160, bounds.Height);
        Assert.AreEqual(new WindowHandle(target.Hwnd), overlay.FrameStats.TargetHwnd);
        Assert.AreEqual(new WindowBounds(40, 50, 240, 160), overlay.FrameStats.TargetBounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayUsesCustomTargetBoundsResolver()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(10, 20, 200, 100);
        var customBounds = new WindowBounds(25, 35, 90, 45);
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)).WithCustomBounds(_ => customBounds),
        });

        Assert.AreEqual(customBounds, overlay.Options.Bounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task FollowTargetZOrderAppliesDuringTargetTracking()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(15, 25, 220, 140);
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)),
            ZOrder = OverlayZOrder.FollowTarget,
        });

        overlay.Render += _ => runCancellation.Cancel();

        await overlay.RunAsync(runCancellation.Token);

        Assert.AreEqual(OverlayZOrder.FollowTarget, overlay.ZOrder);
        Assert.AreEqual(new WindowBounds(15, 25, 220, 140), overlay.FrameStats.TargetBounds);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayCanDiscoverTargetByWindowTitle()
    {
        string title = $"ModernOverlay target title {Guid.NewGuid():N}";
        using Win32OverlayWindow target = CreateHiddenTarget(45, 55, 300, 140, title: title);
        Assert.AreNotEqual(0, target.Hwnd);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.ByWindowTitle(title),
        });

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(45, bounds.X);
        Assert.AreEqual(55, bounds.Y);
        Assert.AreEqual(300, bounds.Width);
        Assert.AreEqual(140, bounds.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayCanDiscoverTargetByTitleContainsByDefault()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string title = $"ModernOverlay target contains {suffix}";
        using Win32OverlayWindow target = CreateHiddenTarget(48, 58, 304, 144, title: title);
        Assert.AreNotEqual(0, target.Hwnd);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.ByTitle($"contains {suffix}"),
        });

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(48, bounds.X);
        Assert.AreEqual(58, bounds.Y);
        Assert.AreEqual(304, bounds.Width);
        Assert.AreEqual(144, bounds.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayCanDiscoverTargetByProcessId()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(52, 62, 308, 148);
        Assert.AreNotEqual(0, target.Hwnd);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.ByProcessId(Environment.ProcessId),
        });

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.IsFalse(bounds.IsEmpty);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayCanDiscoverTargetFromProvider()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(56, 66, 312, 152);
        var provider = new FixedTargetProvider(new WindowHandle(target.Hwnd));

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.FromProvider(provider),
        });

        Assert.AreEqual(1, provider.ResolveCount);
        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(56, bounds.X);
        Assert.AreEqual(66, bounds.Y);
        Assert.AreEqual(312, bounds.Width);
        Assert.AreEqual(152, bounds.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayCanDiscoverTargetByClassName()
    {
        string className = $"ModernOverlayTarget_{Guid.NewGuid():N}";
        using Win32OverlayWindow target = CreateHiddenTarget(65, 75, 280, 130, className: className);
        Assert.AreNotEqual(0, target.Hwnd);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            Target = WindowTarget.ByClassName(className),
        });

        Assert.IsTrue(Win32WindowQuery.TryGetWindowBounds(overlay.Hwnd.Value, clientArea: false, out Win32WindowBounds bounds));
        Assert.AreEqual(65, bounds.X);
        Assert.AreEqual(75, bounds.Y);
        Assert.AreEqual(280, bounds.Width);
        Assert.AreEqual(130, bounds.Height);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TargetMinimizedPausePolicySkipsRenderingUntilRestore()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(85, 95, 260, 150);
        target.Show();
        target.Minimize();
        Assert.IsTrue(Win32WindowQuery.IsWindowMinimized(target.Hwnd));
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool restored = false;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Target = WindowTarget.FromHwnd(new WindowHandle(target.Hwnd)),
            TargetMinimizedPolicy = TargetMinimizedPolicy.PauseRendering,
        });

        overlay.Render += _ =>
        {
            Assert.IsTrue(restored);
            runCancellation.Cancel();
        };
        Task restoreTask = RestoreAfterDelayAsync(target, () => restored = true, TimeSpan.FromMilliseconds(200), runCancellation.Token);

        await overlay.RunAsync(runCancellation.Token);
        await restoreTask;

        Assert.IsTrue(restored);
        Assert.IsGreaterThan(0, overlay.FrameStats.FrameCount);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task TargetLossAndReacquireEventsFire()
    {
        string title = $"ModernOverlay reacquire target {Guid.NewGuid():N}";
        Win32OverlayWindow? target = CreateHiddenTarget(105, 115, 240, 120, title: title);
        using var runCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool disposedOriginal = false;
        bool lost = false;
        bool reacquired = false;

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            IsVisible = false,
            FrameRateLimit = FrameRateLimit.Fixed(120),
            Target = WindowTarget.ByWindowTitle(title),
        });

        try
        {
            overlay.TargetLost += (_, args) =>
            {
                lost = true;
                Assert.IsNotNull(args.TargetHwnd);
                target = CreateHiddenTarget(125, 135, 250, 130, title: title);
            };
            overlay.TargetReacquired += (_, args) =>
            {
                reacquired = true;
                Assert.IsTrue(lost);
                Assert.AreEqual(new WindowBounds(125, 135, 250, 130), args.Bounds);
                runCancellation.Cancel();
            };
            overlay.Render += _ =>
            {
                if (!disposedOriginal)
                {
                    target?.Dispose();
                    target = null;
                    disposedOriginal = true;
                }
            };

            await overlay.RunAsync(runCancellation.Token);
        }
        finally
        {
            target?.Dispose();
        }

        Assert.IsTrue(lost);
        Assert.IsTrue(reacquired);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void Win32QueryCanDiscoverCurrentProcessWindow()
    {
        using Win32OverlayWindow target = CreateHiddenTarget(0, 0, 100, 100);
        Assert.AreNotEqual(0, target.Hwnd);

        Assert.IsTrue(Win32WindowQuery.TryFindWindowByProcessName(Process.GetCurrentProcess().ProcessName, out nint hwnd));
        Assert.AreNotEqual(0, hwnd);
    }

    private static Win32OverlayWindow CreateHiddenTarget(int x, int y, int width, int height, string? title = null, string? className = null)
        => Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: className,
            Title: title ?? "ModernOverlay target tracking test",
            X: x,
            Y: y,
            Width: width,
            Height: height,
            ClickThrough: false,
            TopMost: false,
            ToolWindow: true));

    private static async Task RestoreAfterDelayAsync(Win32OverlayWindow target, Action beforeRestore, TimeSpan delay, CancellationToken ct)
    {
        await Task.Delay(delay, ct);
        beforeRestore();
        target.Restore();
    }

    private sealed class FixedTargetProvider : IWindowTargetProvider
    {
        private readonly WindowHandle hwnd;

        public FixedTargetProvider(WindowHandle hwnd)
        {
            this.hwnd = hwnd;
        }

        public int ResolveCount { get; private set; }

        public bool TryResolve(out WindowHandle hwnd)
        {
            ResolveCount++;
            hwnd = this.hwnd;
            return true;
        }
    }
}
