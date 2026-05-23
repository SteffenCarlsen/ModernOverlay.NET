namespace ModernOverlay.UI;

public sealed class UiRenderContext
{
    internal UiRenderContext(DrawContext draw, UiThemeResources theme)
    {
        Draw = draw;
        Theme = theme;
    }

    public DrawContext Draw { get; }

    public UiThemeResources Theme { get; }
}

public sealed record UiTheme
{
    public static UiTheme Default { get; } = new();

    public string FontFamily { get; init; } = "Segoe UI";

    public float FontSize { get; init; } = 14f;

    public ColorRgba Foreground { get; init; } = ColorRgba.FromBytes(236, 240, 244);

    public ColorRgba MutedForeground { get; init; } = ColorRgba.FromBytes(145, 153, 161);

    public ColorRgba Surface { get; init; } = ColorRgba.FromBytes(30, 34, 40, 236);

    public ColorRgba SurfaceHover { get; init; } = ColorRgba.FromBytes(42, 48, 56, 242);

    public ColorRgba SurfacePressed { get; init; } = ColorRgba.FromBytes(54, 62, 72, 248);

    public ColorRgba Border { get; init; } = ColorRgba.FromBytes(82, 94, 108, 230);

    public ColorRgba Accent { get; init; } = ColorRgba.FromBytes(86, 156, 214);

    public ColorRgba Disabled { get; init; } = ColorRgba.FromBytes(94, 101, 110, 180);
}

public sealed class UiThemeResources : IDisposable
{
    private readonly OverlayResourceManager resources;
    private UiTheme theme;
    private bool disposed;

    internal UiThemeResources(OverlayResourceManager resources, UiTheme theme)
    {
        this.resources = resources;
        this.theme = theme;
        Realize(theme);
    }

    public UiTheme Theme => theme;

    public SolidBrushHandle Foreground { get; private set; } = null!;

    public SolidBrushHandle MutedForeground { get; private set; } = null!;

    public SolidBrushHandle Surface { get; private set; } = null!;

    public SolidBrushHandle SurfaceHover { get; private set; } = null!;

    public SolidBrushHandle SurfacePressed { get; private set; } = null!;

    public SolidBrushHandle Border { get; private set; } = null!;

    public SolidBrushHandle Accent { get; private set; } = null!;

    public SolidBrushHandle Disabled { get; private set; } = null!;

    public FontHandle Font { get; private set; } = null!;

    internal void ApplyTheme(UiTheme nextTheme)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        DisposeHandles();
        theme = nextTheme;
        Realize(nextTheme);
    }

    public SolidBrushHandle CreateSolidBrush(ColorRgba color)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return resources.CreateSolidBrush(color);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeHandles();
    }

    private void Realize(UiTheme nextTheme)
    {
        Foreground = resources.CreateSolidBrush(nextTheme.Foreground);
        MutedForeground = resources.CreateSolidBrush(nextTheme.MutedForeground);
        Surface = resources.CreateSolidBrush(nextTheme.Surface);
        SurfaceHover = resources.CreateSolidBrush(nextTheme.SurfaceHover);
        SurfacePressed = resources.CreateSolidBrush(nextTheme.SurfacePressed);
        Border = resources.CreateSolidBrush(nextTheme.Border);
        Accent = resources.CreateSolidBrush(nextTheme.Accent);
        Disabled = resources.CreateSolidBrush(nextTheme.Disabled);
        Font = resources.CreateFont(new FontOptions(nextTheme.FontFamily, nextTheme.FontSize));
    }

    private void DisposeHandles()
    {
        Foreground?.Dispose();
        MutedForeground?.Dispose();
        Surface?.Dispose();
        SurfaceHover?.Dispose();
        SurfacePressed?.Dispose();
        Border?.Dispose();
        Accent?.Dispose();
        Disabled?.Dispose();
        Font?.Dispose();
    }
}
