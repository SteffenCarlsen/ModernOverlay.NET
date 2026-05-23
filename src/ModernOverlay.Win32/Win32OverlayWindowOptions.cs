namespace ModernOverlay.Win32;

public sealed record Win32OverlayWindowOptions(
    string? ClassName,
    string Title,
    int X,
    int Y,
    int Width,
    int Height,
    bool ClickThrough,
    bool TopMost,
    bool ToolWindow,
    bool PerMonitorDpiAware = true,
    bool ExcludeFromCapture = false);
