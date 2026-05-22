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
}
