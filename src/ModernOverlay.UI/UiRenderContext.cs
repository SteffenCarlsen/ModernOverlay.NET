using ModernOverlay.Diagnostics;

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

    public ColorRgba Border { get; init; } = ColorRgba.FromBytes(120, 134, 150, 230);

    public ColorRgba Accent { get; init; } = ColorRgba.FromBytes(86, 156, 214);

    public ColorRgba Disabled { get; init; } = ColorRgba.FromBytes(130, 141, 153, 200);

    public UiThemeReadabilityReport CheckReadability()
    {
        UiThemeContrastCheck[] checks =
        [
            UiThemeContrastCheck.Create("Foreground on surface", Foreground, Surface, 4.5f),
            UiThemeContrastCheck.Create("Foreground on hover surface", Foreground, SurfaceHover, 4.5f),
            UiThemeContrastCheck.Create("Foreground on pressed surface", Foreground, SurfacePressed, 4.5f),
            UiThemeContrastCheck.Create("Muted foreground on surface", MutedForeground, Surface, 4.5f),
            UiThemeContrastCheck.Create("Accent on surface", Accent, Surface, 3.0f),
            UiThemeContrastCheck.Create("Border on surface", Border, Surface, 3.0f),
            UiThemeContrastCheck.Create("Disabled on surface", Disabled, Surface, 3.0f),
        ];
        return new UiThemeReadabilityReport(checks);
    }
}

public sealed record UiThemeReadabilityReport(IReadOnlyList<UiThemeContrastCheck> Checks)
{
    public bool IsReadable => Checks.Count > 0 && Checks.All(check => check.Passes);

    public IReadOnlyList<UiThemeContrastCheck> Failures => Checks.Where(check => !check.Passes).ToArray();
}

public sealed record UiThemeContrastCheck(
    string Name,
    ColorRgba Foreground,
    ColorRgba Background,
    float ContrastRatio,
    float MinimumContrastRatio)
{
    public bool Passes => ContrastRatio >= MinimumContrastRatio;

    public static UiThemeContrastCheck Create(string name, ColorRgba foreground, ColorRgba background, float minimumContrastRatio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        float validatedMinimumContrastRatio = minimumContrastRatio > 0f && float.IsFinite(minimumContrastRatio)
            ? minimumContrastRatio
            : throw new ArgumentOutOfRangeException(nameof(minimumContrastRatio), "Minimum contrast ratio must be finite and greater than zero.");

        return new UiThemeContrastCheck(
            name,
            foreground,
            background,
            CalculateContrastRatio(foreground, background),
            validatedMinimumContrastRatio);
    }

    public static float CalculateContrastRatio(ColorRgba foreground, ColorRgba background)
    {
        float foregroundLuminance = RelativeLuminance(CompositeOverOpaqueBackground(foreground, background));
        float backgroundLuminance = RelativeLuminance(background);
        float lighter = MathF.Max(foregroundLuminance, backgroundLuminance);
        float darker = MathF.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static ColorRgba CompositeOverOpaqueBackground(ColorRgba foreground, ColorRgba background)
    {
        float alpha = Math.Clamp(foreground.A, 0f, 1f);
        float inverseAlpha = 1f - alpha;
        return new ColorRgba(
            foreground.R * alpha + background.R * inverseAlpha,
            foreground.G * alpha + background.G * inverseAlpha,
            foreground.B * alpha + background.B * inverseAlpha,
            1f);
    }

    private static float RelativeLuminance(ColorRgba color)
        => 0.2126f * Linearize(color.R)
            + 0.7152f * Linearize(color.G)
            + 0.0722f * Linearize(color.B);

    private static float Linearize(float channel)
    {
        float value = Math.Clamp(channel, 0f, 1f);
        return value <= 0.03928f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
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
        ReplaceHandles(theme, disposeExisting: false);
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
        ReplaceHandles(nextTheme, disposeExisting: true);
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

    private void ReplaceHandles(UiTheme nextTheme, bool disposeExisting)
    {
        ThemeHandleSet handles = Realize(nextTheme);
        if (disposeExisting)
        {
            DisposeHandles();
        }

        theme = nextTheme;
        Foreground = handles.Foreground;
        MutedForeground = handles.MutedForeground;
        Surface = handles.Surface;
        SurfaceHover = handles.SurfaceHover;
        SurfacePressed = handles.SurfacePressed;
        Border = handles.Border;
        Accent = handles.Accent;
        Disabled = handles.Disabled;
        Font = handles.Font;
    }

    private ThemeHandleSet Realize(UiTheme nextTheme)
    {
        SolidBrushHandle? foreground = null;
        SolidBrushHandle? mutedForeground = null;
        SolidBrushHandle? surface = null;
        SolidBrushHandle? surfaceHover = null;
        SolidBrushHandle? surfacePressed = null;
        SolidBrushHandle? border = null;
        SolidBrushHandle? accent = null;
        SolidBrushHandle? disabled = null;
        FontHandle? font = null;
        try
        {
            foreground = CreateTrackedResource("Theme.ForegroundBrush", () => resources.CreateSolidBrush(nextTheme.Foreground));
            mutedForeground = CreateTrackedResource("Theme.MutedForegroundBrush", () => resources.CreateSolidBrush(nextTheme.MutedForeground));
            surface = CreateTrackedResource("Theme.SurfaceBrush", () => resources.CreateSolidBrush(nextTheme.Surface));
            surfaceHover = CreateTrackedResource("Theme.SurfaceHoverBrush", () => resources.CreateSolidBrush(nextTheme.SurfaceHover));
            surfacePressed = CreateTrackedResource("Theme.SurfacePressedBrush", () => resources.CreateSolidBrush(nextTheme.SurfacePressed));
            border = CreateTrackedResource("Theme.BorderBrush", () => resources.CreateSolidBrush(nextTheme.Border));
            accent = CreateTrackedResource("Theme.AccentBrush", () => resources.CreateSolidBrush(nextTheme.Accent));
            disabled = CreateTrackedResource("Theme.DisabledBrush", () => resources.CreateSolidBrush(nextTheme.Disabled));
            font = CreateTrackedResource("Theme.Font", () => resources.CreateFont(new FontOptions(nextTheme.FontFamily, nextTheme.FontSize)));
            return new ThemeHandleSet(foreground, mutedForeground, surface, surfaceHover, surfacePressed, border, accent, disabled, font);
        }
        catch
        {
            foreground?.Dispose();
            mutedForeground?.Dispose();
            surface?.Dispose();
            surfaceHover?.Dispose();
            surfacePressed?.Dispose();
            border?.Dispose();
            accent?.Dispose();
            disabled?.Dispose();
            font?.Dispose();
            throw;
        }
    }

    private static T CreateTrackedResource<T>(string resourceKind, Func<T> create)
        where T : OverlayResourceHandle
    {
        try
        {
            return create();
        }
        catch (Exception ex)
        {
            OverlayEventSource.Log.UiResourceRealizationFailure(
                resourceKind,
                ex.GetType().FullName ?? ex.GetType().Name,
                ex.Message);
            throw;
        }
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

    private sealed record ThemeHandleSet(
        SolidBrushHandle Foreground,
        SolidBrushHandle MutedForeground,
        SolidBrushHandle Surface,
        SolidBrushHandle SurfaceHover,
        SolidBrushHandle SurfacePressed,
        SolidBrushHandle Border,
        SolidBrushHandle Accent,
        SolidBrushHandle Disabled,
        FontHandle Font);
}
