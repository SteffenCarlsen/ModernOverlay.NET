namespace ModernOverlay.UI;

public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public static Thickness Zero { get; } = new(0f);

    public Thickness(float uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    public Thickness(float horizontal, float vertical)
        : this(horizontal, vertical, horizontal, vertical)
    {
    }

    public float Horizontal => Left + Right;

    public float Vertical => Top + Bottom;
}

public readonly record struct UiSize(float Width, float Height)
{
    public static UiSize Zero { get; } = new(0f, 0f);
}

public enum UiOrientation
{
    Vertical,
    Horizontal,
}

public enum UiVisibility
{
    Visible,
    Hidden,
    Collapsed,
}

public enum UiHorizontalAlignment
{
    Stretch,
    Left,
    Center,
    Right,
}

public enum UiVerticalAlignment
{
    Stretch,
    Top,
    Center,
    Bottom,
}

public enum UiLayer
{
    Content = 0,
    Floating = 100,
    Popup = 200,
    Adorner = 300,
}

public enum Dock
{
    Left,
    Top,
    Right,
    Bottom,
}

public enum GridUnitType
{
    Pixel,
    Auto,
    Star,
}

public readonly record struct GridLength(float Value, GridUnitType UnitType)
{
    public static GridLength Auto { get; } = new(1f, GridUnitType.Auto);

    public static GridLength Star(float value = 1f) => new(value, GridUnitType.Star);

    public static GridLength Pixel(float value) => new(value, GridUnitType.Pixel);
}

public sealed class GridDefinition
{
    public GridDefinition(GridLength length)
    {
        Length = length;
    }

    public GridLength Length { get; set; }
}

[Flags]
internal enum UiInvalidation
{
    None = 0,
    Measure = 1 << 0,
    Arrange = 1 << 1,
    Render = 1 << 2,
    InputRegion = 1 << 3,
    FocusState = 1 << 4,
    Resource = 1 << 5,
}

internal enum UiRootPhase
{
    Idle,
    Measure,
    Arrange,
    Render,
    EventDispatch,
    FocusChange,
    PopupDismissal,
    CaptureRelease,
}

internal static class UiGeometry
{
    public static RectF Deflate(RectF rect, Thickness thickness)
    {
        float width = MathF.Max(0f, rect.Width - thickness.Horizontal);
        float height = MathF.Max(0f, rect.Height - thickness.Vertical);
        return new RectF(rect.X + thickness.Left, rect.Y + thickness.Top, width, height);
    }

    public static bool Contains(RectF rect, PointF point)
        => point.X >= rect.X
            && point.Y >= rect.Y
            && point.X < rect.X + rect.Width
            && point.Y < rect.Y + rect.Height;

    public static SizeF Clamp(SizeF size, float minWidth, float minHeight, float maxWidth, float maxHeight)
        => new(
            Math.Clamp(size.Width, minWidth, maxWidth),
            Math.Clamp(size.Height, minHeight, maxHeight));

    public static SizeF Deflate(SizeF size, Thickness thickness)
        => new(
            MathF.Max(0f, size.Width - thickness.Horizontal),
            MathF.Max(0f, size.Height - thickness.Vertical));

    public static SizeF Inflate(SizeF size, Thickness thickness)
        => new(size.Width + thickness.Horizontal, size.Height + thickness.Vertical);
}
