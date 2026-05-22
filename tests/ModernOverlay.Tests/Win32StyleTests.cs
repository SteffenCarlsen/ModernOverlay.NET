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
        using Win32OverlayWindow window = Win32OverlayWindow.Create(new Win32OverlayWindowOptions(
            ClassName: null,
            Title: "ModernOverlay Win32 query test",
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

        Win32WindowZOrder.MakeTopmost(window.Hwnd);
        Win32WindowZOrder.RemoveTopmost(window.Hwnd);
        Win32WindowEffects.ExtendFrameIntoClientArea(window.Hwnd);
        Win32WindowEffects.EnableBlurBehind(window.Hwnd);
        Win32WindowEffects.EnableBlurBehind(window.Hwnd, enabled: false);
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
        }
        finally
        {
            _ = DestroyWindow(child);
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
