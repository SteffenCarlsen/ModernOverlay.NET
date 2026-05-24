using System.Runtime.InteropServices;
using ModernOverlay.Win32;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class Win32StyleTests
{
    [TestMethod]
    public void ClickThroughTopMostToolWindowStyleIncludesRequiredFlags()
    {
        uint style = WindowStyles.BuildExtendedStyle(clickThrough: true, topMost: true, toolWindow: true);

        Assert.AreNotEqual(0u, style & WindowStyles.WsExLayered);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExNoActivate);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExTransparent);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExTopMost);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExToolWindow);
    }

    [TestMethod]
    public void InteractiveStyleDoesNotIncludeTransparentFlag()
    {
        uint style = WindowStyles.BuildExtendedStyle(clickThrough: false, topMost: false, toolWindow: false);

        Assert.AreEqual(0u, style & WindowStyles.WsExTransparent);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExLayered);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExNoActivate);
    }

    [TestMethod]
    public void ActivatableInteractiveStyleOmitsNoActivateFlag()
    {
        uint style = WindowStyles.BuildExtendedStyle(clickThrough: false, topMost: false, toolWindow: false, noActivate: false);

        Assert.AreEqual(0u, style & WindowStyles.WsExTransparent);
        Assert.AreEqual(0u, style & WindowStyles.WsExNoActivate);
        Assert.AreNotEqual(0u, style & WindowStyles.WsExLayered);
    }

    [TestMethod]
    public void NativeHResultExceptionRecordsLastNativeFailure()
    {
        _ = new NativeHResultException("UnitTestOperation", unchecked((int)0x80004005));

        Win32NativeFailureInfo? failure = Win32NativeDiagnostics.LastFailure;
        Assert.IsNotNull(failure);
        Assert.AreEqual("UnitTestOperation", failure.Operation);
        Assert.AreEqual(unchecked((int)0x80004005), failure.HResult);
        Assert.IsNull(failure.Win32Error);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void Win32QueryExposesRequiredWindowMetadata()
    {
        string title = $"ModernOverlay Win32 query test {Guid.NewGuid():N}";
        using Win32OverlayWindow window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: title,
            X: 0,
            Y: 0,
            Width: 160,
            Height: 90,
            ClickThrough: true,
            TopMost: false,
            ToolWindow: true));

        Assert.IsTrue(Win32WindowQuery.IsWindow(window.Hwnd));
        Assert.IsTrue(Win32WindowQuery.TryGetWindowProcessId(window.Hwnd, out int processId));
        Assert.AreEqual(Environment.ProcessId, processId);
        Assert.IsTrue(Win32WindowQuery.TryGetWindowStyles(window.Hwnd, out Win32WindowStyles styles));
        Assert.AreNotEqual(nint.Zero, styles.Style);
        Assert.AreNotEqual(nint.Zero, styles.ExtendedStyle & new nint(WindowStyles.WsExLayered));
        Assert.IsTrue(Win32WindowQuery.TryGetDesktopWindow(out nint desktop));
        Assert.AreNotEqual(nint.Zero, desktop);
        Assert.IsFalse(Win32WindowQuery.TryGetOwnerWindow(window.Hwnd, out _));

        WindowHandle handle = new(window.Hwnd);
        Assert.IsTrue(WindowQuery.IsWindow(handle));
        Assert.IsTrue(WindowQuery.TryGetWindowBounds(handle, out WindowBounds windowBounds));
        Assert.AreEqual(160, windowBounds.Width);
        Assert.AreEqual(90, windowBounds.Height);
        Assert.IsTrue(WindowQuery.TryGetWindowStyles(handle, out WindowStylesSnapshot publicStyles));
        Assert.AreEqual(styles.Style, publicStyles.Style);
        Assert.AreEqual(styles.ExtendedStyle, publicStyles.ExtendedStyle);

        Win32WindowZOrder.MakeTopmost(window.Hwnd);
        Win32WindowZOrder.RemoveTopmost(window.Hwnd);
        Win32WindowEffects.ExtendFrameIntoClientArea(window.Hwnd);
        Win32WindowEffects.EnableBlurBehind(window.Hwnd);
        Win32WindowEffects.EnableBlurBehind(window.Hwnd, enabled: false);
        window.SetTransparentColorKey(0x000000);
        window.Show();
        Win32WindowEffects.ExcludeFromCapture(window.Hwnd);
        Assert.AreEqual(Win32WindowDisplayAffinity.ExcludeFromCapture, Win32WindowEffects.GetDisplayAffinity(window.Hwnd));
        Win32WindowEffects.ClearDisplayAffinity(window.Hwnd);
        Assert.AreEqual(Win32WindowDisplayAffinity.None, Win32WindowEffects.GetDisplayAffinity(window.Hwnd));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task OverlayOptionAppliesCaptureExclusionBeforeUse()
    {
        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = new WindowBounds(360, 360, 160, 90),
            IsVisible = true,
            ExcludeFromCapture = true,
        });

        Assert.AreEqual(Win32WindowDisplayAffinity.ExcludeFromCapture, Win32WindowEffects.GetDisplayAffinity(overlay.Hwnd.Value));
        Assert.IsTrue(WindowEffects.TryClearDisplayAffinity(overlay.Hwnd));
        Assert.AreEqual(Win32WindowDisplayAffinity.None, Win32WindowEffects.GetDisplayAffinity(overlay.Hwnd.Value));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void Win32QueryFindsChildWindowsByTitleAndClassName()
    {
        using Win32OverlayWindow window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay child query parent",
            X: 0,
            Y: 0,
            Width: 160,
            Height: 90,
            ClickThrough: false,
            TopMost: false,
            ToolWindow: true));
        nint child = CreateWindowEx(
            0,
            "Static",
            "ModernOverlay child query target",
            WsChild | WsVisible,
            4,
            4,
            80,
            24,
            window.Hwnd,
            0,
            GetModuleHandle(null),
            0);
        Assert.AreNotEqual(0, child);

        try
        {
            Assert.IsTrue(Win32WindowQuery.TryFindChildWindowByTitle(window.Hwnd, "child query", Win32WindowTitleMatchMode.Contains, out nint byTitle));
            Assert.AreEqual(child, byTitle);
            Assert.IsTrue(Win32WindowQuery.TryFindChildWindowByClassName(window.Hwnd, "Static", out nint byClassName));
            Assert.AreEqual(child, byClassName);
            Assert.IsTrue(Win32WindowQuery.TryFindChildWindow(window.Hwnd, "Static", "target", Win32WindowTitleMatchMode.EndsWith, out nint byClassAndTitle));
            Assert.AreEqual(child, byClassAndTitle);
            Assert.IsTrue(WindowQuery.TryFindChildWindow(new WindowHandle(window.Hwnd), "Static", "target", MatchMode.EndsWith, out WindowHandle publicByClassAndTitle));
            Assert.AreEqual(new WindowHandle(child), publicByClassAndTitle);
        }
        finally
        {
            _ = DestroyWindow(child);
        }
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task VisibleOverlayDoesNotStealForegroundWindow()
    {
        bool hadForegroundBefore = Win32WindowQuery.TryGetForegroundWindow(out nint foregroundBefore);

        await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
        {
            Bounds = new WindowBounds(340, 330, 160, 80),
            IsVisible = true,
            ZOrder = OverlayZOrder.TopMost,
        });

        await Task.Delay(150);

        bool hasForegroundAfter = Win32WindowQuery.TryGetForegroundWindow(out nint foregroundAfter);
        if (hasForegroundAfter)
        {
            Assert.AreNotEqual(overlay.Hwnd.Value, foregroundAfter);
        }

        if (hadForegroundBefore && hasForegroundAfter)
        {
            Assert.AreEqual(foregroundBefore, foregroundAfter);
        }
    }

    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
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

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

}
