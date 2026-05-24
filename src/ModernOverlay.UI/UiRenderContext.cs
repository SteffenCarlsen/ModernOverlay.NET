using ModernOverlay.Diagnostics;

namespace ModernOverlay.UI;

/// <summary>
/// Provides drawing and theme resources to an element during UI rendering.
/// </summary>
public sealed class UiRenderContext
{
    internal UiRenderContext(DrawContext draw, UiThemeResources theme)
    {
        Draw = draw;
        Theme = theme;
    }

    /// <summary>
    /// Gets the underlying frame draw context.
    /// </summary>
    public DrawContext Draw { get; }

    /// <summary>
    /// Gets the realized theme resources for the current UI root.
    /// </summary>
    public UiThemeResources Theme { get; }
}

/// <summary>
/// Defines colors and font settings used by retained UI controls.
/// </summary>
public sealed record UiTheme
{
    /// <summary>
    /// Gets the default built-in UI theme.
    /// </summary>
    public static UiTheme Default { get; } = new();

    /// <summary>
    /// Gets the font family used by built-in controls.
    /// </summary>
    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>
    /// Gets the font size used by built-in controls.
    /// </summary>
    public float FontSize { get; init; } = 14f;

    /// <summary>
    /// Gets the primary foreground color.
    /// </summary>
    public ColorRgba Foreground { get; init; } = ColorRgba.FromBytes(236, 240, 244);

    /// <summary>
    /// Gets the muted foreground color used for secondary text and placeholders.
    /// </summary>
    public ColorRgba MutedForeground { get; init; } = ColorRgba.FromBytes(145, 153, 161);

    /// <summary>
    /// Gets the base surface color.
    /// </summary>
    public ColorRgba Surface { get; init; } = ColorRgba.FromBytes(30, 34, 40, 236);

    /// <summary>
    /// Gets the hover surface color.
    /// </summary>
    public ColorRgba SurfaceHover { get; init; } = ColorRgba.FromBytes(42, 48, 56, 242);

    /// <summary>
    /// Gets the pressed surface color.
    /// </summary>
    public ColorRgba SurfacePressed { get; init; } = ColorRgba.FromBytes(54, 62, 72, 248);

    /// <summary>
    /// Gets the default border color.
    /// </summary>
    public ColorRgba Border { get; init; } = ColorRgba.FromBytes(120, 134, 150, 230);

    /// <summary>
    /// Gets the accent color used for focus, selection, and active affordances.
    /// </summary>
    public ColorRgba Accent { get; init; } = ColorRgba.FromBytes(86, 156, 214);

    /// <summary>
    /// Gets the disabled-state color.
    /// </summary>
    public ColorRgba Disabled { get; init; } = ColorRgba.FromBytes(130, 141, 153, 200);

    /// <summary>
    /// Checks key foreground/background pairs for readable contrast.
    /// </summary>
    /// <returns>A readability report for the theme.</returns>
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

/// <summary>
/// Contains readability checks for a UI theme.
/// </summary>
/// <param name="Checks">The contrast checks that were evaluated.</param>
public sealed record UiThemeReadabilityReport(IReadOnlyList<UiThemeContrastCheck> Checks)
{
    /// <summary>
    /// Gets whether every contrast check passed.
    /// </summary>
    public bool IsReadable => Checks.Count > 0 && Checks.All(check => check.Passes);

    /// <summary>
    /// Gets the failed contrast checks.
    /// </summary>
    public IReadOnlyList<UiThemeContrastCheck> Failures => Checks.Where(check => !check.Passes).ToArray();
}

/// <summary>
/// Represents one foreground/background contrast check.
/// </summary>
/// <param name="Name">The display name of the check.</param>
/// <param name="Foreground">The foreground color.</param>
/// <param name="Background">The background color.</param>
/// <param name="ContrastRatio">The calculated contrast ratio.</param>
/// <param name="MinimumContrastRatio">The minimum acceptable contrast ratio.</param>
public sealed record UiThemeContrastCheck(
    string Name,
    ColorRgba Foreground,
    ColorRgba Background,
    float ContrastRatio,
    float MinimumContrastRatio)
{
    /// <summary>
    /// Gets whether the calculated contrast ratio meets the minimum.
    /// </summary>
    public bool Passes => ContrastRatio >= MinimumContrastRatio;

    /// <summary>
    /// Creates a contrast check from two colors and a minimum ratio.
    /// </summary>
    /// <param name="name">The display name of the check.</param>
    /// <param name="foreground">The foreground color.</param>
    /// <param name="background">The background color.</param>
    /// <param name="minimumContrastRatio">The minimum acceptable contrast ratio.</param>
    /// <returns>The calculated contrast check.</returns>
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

    /// <summary>
    /// Calculates the contrast ratio between a foreground color and a background color.
    /// </summary>
    /// <param name="foreground">The foreground color.</param>
    /// <param name="background">The background color.</param>
    /// <returns>The calculated contrast ratio.</returns>
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

/// <summary>
/// Owns realized drawing resources for a <see cref="UiTheme"/>.
/// </summary>
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

    /// <summary>
    /// Gets the theme used to create the current resource handles.
    /// </summary>
    public UiTheme Theme => theme;

    /// <summary>
    /// Gets the primary foreground brush.
    /// </summary>
    public SolidBrushHandle Foreground { get; private set; } = null!;

    /// <summary>
    /// Gets the muted foreground brush.
    /// </summary>
    public SolidBrushHandle MutedForeground { get; private set; } = null!;

    /// <summary>
    /// Gets the base surface brush.
    /// </summary>
    public SolidBrushHandle Surface { get; private set; } = null!;

    /// <summary>
    /// Gets the hover surface brush.
    /// </summary>
    public SolidBrushHandle SurfaceHover { get; private set; } = null!;

    /// <summary>
    /// Gets the pressed surface brush.
    /// </summary>
    public SolidBrushHandle SurfacePressed { get; private set; } = null!;

    /// <summary>
    /// Gets the default border brush.
    /// </summary>
    public SolidBrushHandle Border { get; private set; } = null!;

    /// <summary>
    /// Gets the accent brush.
    /// </summary>
    public SolidBrushHandle Accent { get; private set; } = null!;

    /// <summary>
    /// Gets the disabled-state brush.
    /// </summary>
    public SolidBrushHandle Disabled { get; private set; } = null!;

    /// <summary>
    /// Gets the default UI font.
    /// </summary>
    public FontHandle Font { get; private set; } = null!;

    internal void ApplyTheme(UiTheme nextTheme)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ReplaceHandles(nextTheme, disposeExisting: true);
    }

    /// <summary>
    /// Creates a caller-owned solid brush using the root resource manager.
    /// </summary>
    /// <param name="color">The brush color.</param>
    /// <returns>A solid brush handle that must be disposed by the caller.</returns>
    public SolidBrushHandle CreateSolidBrush(ColorRgba color)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return resources.CreateSolidBrush(color);
    }

    /// <summary>
    /// Disposes the realized theme resource handles.
    /// </summary>
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
