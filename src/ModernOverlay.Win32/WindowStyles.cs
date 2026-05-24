namespace ModernOverlay.Win32;

public static class WindowStyles
{
    public const int GwlStyle = -16;

    public const int GwlExStyle = -20;

    public const uint WsPopup = 0x80000000;

    public const uint WsExTopMost = 0x00000008;

    public const uint WsExTransparent = 0x00000020;

    public const uint WsExToolWindow = 0x00000080;

    public const uint WsExLayered = 0x00080000;

    public const uint WsExNoActivate = 0x08000000;

    public static uint BuildExtendedStyle(bool clickThrough, bool topMost, bool toolWindow, bool noActivate = true)
    {
        uint style = WsExLayered;

        if (noActivate)
        {
            style |= WsExNoActivate;
        }

        if (clickThrough)
        {
            style |= WsExTransparent;
        }

        if (topMost)
        {
            style |= WsExTopMost;
        }

        if (toolWindow)
        {
            style |= WsExToolWindow;
        }

        return style;
    }

    public static nint WithFlag(nint value, uint flag, bool enabled)
    {
        long current = value.ToInt64();
        long next = enabled ? current | flag : current & ~flag;
        return new nint(next);
    }
}
