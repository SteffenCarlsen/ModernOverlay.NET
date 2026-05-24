namespace ModernOverlay.UI;

/// <summary>
/// Represents distances from the left, top, right, and bottom edges of a UI rectangle.
/// </summary>
/// <param name="Left">The left distance in DIPs.</param>
/// <param name="Top">The top distance in DIPs.</param>
/// <param name="Right">The right distance in DIPs.</param>
/// <param name="Bottom">The bottom distance in DIPs.</param>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    /// <summary>
    /// Gets a thickness with all edges set to zero.
    /// </summary>
    public static Thickness Zero { get; } = new(0f);

    /// <summary>
    /// Initializes a thickness with the same value on every edge.
    /// </summary>
    /// <param name="uniform">The value for all edges in DIPs.</param>
    public Thickness(float uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Initializes a thickness with shared horizontal and vertical values.
    /// </summary>
    /// <param name="horizontal">The left and right values in DIPs.</param>
    /// <param name="vertical">The top and bottom values in DIPs.</param>
    public Thickness(float horizontal, float vertical)
        : this(horizontal, vertical, horizontal, vertical)
    {
    }

    /// <summary>
    /// Gets the combined left and right distance.
    /// </summary>
    public float Horizontal => Left + Right;

    /// <summary>
    /// Gets the combined top and bottom distance.
    /// </summary>
    public float Vertical => Top + Bottom;
}

/// <summary>
/// Represents a UI size in device-independent pixels.
/// </summary>
/// <param name="Width">The width in DIPs.</param>
/// <param name="Height">The height in DIPs.</param>
public readonly record struct UiSize(float Width, float Height)
{
    /// <summary>
    /// Gets a zero-width, zero-height size.
    /// </summary>
    public static UiSize Zero { get; } = new(0f, 0f);
}

/// <summary>
/// Represents minimum and maximum layout constraints in device-independent pixels.
/// </summary>
public readonly record struct UiConstraints
{
    /// <summary>
    /// Gets constraints with zero minimum size and no finite maximum size.
    /// </summary>
    public static UiConstraints Unbounded { get; } = new(0f, 0f, float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>
    /// Initializes a new constraint range.
    /// </summary>
    /// <param name="minWidth">The minimum width in DIPs.</param>
    /// <param name="minHeight">The minimum height in DIPs.</param>
    /// <param name="maxWidth">The maximum width in DIPs, or positive infinity.</param>
    /// <param name="maxHeight">The maximum height in DIPs, or positive infinity.</param>
    public UiConstraints(float minWidth, float minHeight, float maxWidth, float maxHeight)
    {
        ValidateMinimum(minWidth, nameof(minWidth));
        ValidateMinimum(minHeight, nameof(minHeight));
        ValidateMaximum(maxWidth, nameof(maxWidth));
        ValidateMaximum(maxHeight, nameof(maxHeight));
        if (minWidth > maxWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(minWidth), "Minimum width cannot be greater than maximum width.");
        }

        if (minHeight > maxHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(minHeight), "Minimum height cannot be greater than maximum height.");
        }

        MinWidth = minWidth;
        MinHeight = minHeight;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
    }

    /// <summary>
    /// Gets the minimum width in DIPs.
    /// </summary>
    public float MinWidth { get; }

    /// <summary>
    /// Gets the minimum height in DIPs.
    /// </summary>
    public float MinHeight { get; }

    /// <summary>
    /// Gets the maximum width in DIPs.
    /// </summary>
    public float MaxWidth { get; }

    /// <summary>
    /// Gets the maximum height in DIPs.
    /// </summary>
    public float MaxHeight { get; }

    /// <summary>
    /// Clamps a size to this constraint range.
    /// </summary>
    /// <param name="size">The size to constrain.</param>
    /// <returns>The constrained size.</returns>
    public SizeF Constrain(SizeF size)
        => UiGeometry.Clamp(size, MinWidth, MinHeight, MaxWidth, MaxHeight);

    /// <summary>
    /// Creates a copy with a different minimum width.
    /// </summary>
    /// <param name="value">The new minimum width in DIPs.</param>
    /// <returns>The updated constraints.</returns>
    public UiConstraints WithMinWidth(float value) => new(value, MinHeight, MaxWidth, MaxHeight);

    /// <summary>
    /// Creates a copy with a different minimum height.
    /// </summary>
    /// <param name="value">The new minimum height in DIPs.</param>
    /// <returns>The updated constraints.</returns>
    public UiConstraints WithMinHeight(float value) => new(MinWidth, value, MaxWidth, MaxHeight);

    /// <summary>
    /// Creates a copy with a different maximum width.
    /// </summary>
    /// <param name="value">The new maximum width in DIPs, or positive infinity.</param>
    /// <returns>The updated constraints.</returns>
    public UiConstraints WithMaxWidth(float value) => new(MinWidth, MinHeight, value, MaxHeight);

    /// <summary>
    /// Creates a copy with a different maximum height.
    /// </summary>
    /// <param name="value">The new maximum height in DIPs, or positive infinity.</param>
    /// <returns>The updated constraints.</returns>
    public UiConstraints WithMaxHeight(float value) => new(MinWidth, MinHeight, MaxWidth, value);

    private static void ValidateMinimum(float value, string parameterName)
    {
        if (value < 0f || !float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Minimum layout constraints must be finite and non-negative.");
        }
    }

    private static void ValidateMaximum(float value, string parameterName)
    {
        if (value < 0f || (!float.IsFinite(value) && !float.IsPositiveInfinity(value)))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Maximum layout constraints must be non-negative finite values or positive infinity.");
        }
    }
}

/// <summary>
/// Describes the primary axis for layout or range controls.
/// </summary>
public enum UiOrientation
{
    /// <summary>Arrange or interpret values vertically.</summary>
    Vertical,

    /// <summary>Arrange or interpret values horizontally.</summary>
    Horizontal,
}

/// <summary>
/// Describes whether an element is rendered and participates in layout.
/// </summary>
public enum UiVisibility
{
    /// <summary>The element is visible and participates in layout.</summary>
    Visible,

    /// <summary>The element is not rendered but still reserves layout space.</summary>
    Hidden,

    /// <summary>The element is not rendered and does not reserve layout space.</summary>
    Collapsed,
}

/// <summary>
/// Describes horizontal alignment within an assigned layout slot.
/// </summary>
public enum UiHorizontalAlignment
{
    /// <summary>Stretch to fill the available width.</summary>
    Stretch,

    /// <summary>Align to the left edge.</summary>
    Left,

    /// <summary>Center horizontally.</summary>
    Center,

    /// <summary>Align to the right edge.</summary>
    Right,
}

/// <summary>
/// Describes vertical alignment within an assigned layout slot.
/// </summary>
public enum UiVerticalAlignment
{
    /// <summary>Stretch to fill the available height.</summary>
    Stretch,

    /// <summary>Align to the top edge.</summary>
    Top,

    /// <summary>Center vertically.</summary>
    Center,

    /// <summary>Align to the bottom edge.</summary>
    Bottom,
}

/// <summary>
/// Describes how an image is scaled inside its content bounds.
/// </summary>
public enum UiImageStretch
{
    /// <summary>Do not scale the image.</summary>
    None,

    /// <summary>Scale to fill the content bounds exactly.</summary>
    Fill,

    /// <summary>Scale uniformly so the whole image fits inside the content bounds.</summary>
    Uniform,

    /// <summary>Scale uniformly so the content bounds are fully covered.</summary>
    UniformToFill,
}

/// <summary>
/// Describes text wrapping behavior.
/// </summary>
public enum UiTextWrapping
{
    /// <summary>Render text on a single line.</summary>
    NoWrap,

    /// <summary>Wrap text within the available width.</summary>
    Wrap,
}

/// <summary>
/// Describes text trimming behavior when content exceeds the available line width.
/// </summary>
public enum UiTextTrimming
{
    /// <summary>Do not trim overflowing text.</summary>
    None,

    /// <summary>Trim by character and append an ellipsis.</summary>
    CharacterEllipsis,
}

/// <summary>
/// Describes the effective visual state of an element.
/// </summary>
public enum UiVisualState
{
    /// <summary>The default state.</summary>
    Normal,

    /// <summary>The pointer is over the element.</summary>
    Hover,

    /// <summary>The element is pressed or actively captured.</summary>
    Pressed,

    /// <summary>The element or one of its ancestors is disabled.</summary>
    Disabled,

    /// <summary>The element has keyboard focus.</summary>
    Focused,
}

/// <summary>
/// Describes the state of a toggle control.
/// </summary>
public enum UiToggleState
{
    /// <summary>The control is unchecked.</summary>
    Unchecked,

    /// <summary>The control is checked.</summary>
    Checked,

    /// <summary>The control is in an indeterminate third state.</summary>
    Indeterminate,
}

/// <summary>
/// Provides conventional z-index bands for retained UI elements.
/// </summary>
public enum UiLayer
{
    /// <summary>Normal content.</summary>
    Content = 0,

    /// <summary>Floating windows and panels.</summary>
    Floating = 100,

    /// <summary>Popup content such as menus and combo boxes.</summary>
    Popup = 200,

    /// <summary>Adornment content drawn above popups.</summary>
    Adorner = 300,
}

/// <summary>
/// Describes the edge used by <see cref="DockPanel"/>.
/// </summary>
public enum Dock
{
    /// <summary>Dock to the left edge.</summary>
    Left,

    /// <summary>Dock to the top edge.</summary>
    Top,

    /// <summary>Dock to the right edge.</summary>
    Right,

    /// <summary>Dock to the bottom edge.</summary>
    Bottom,
}

/// <summary>
/// Describes how a grid row or column length is resolved.
/// </summary>
public enum GridUnitType
{
    /// <summary>Use a fixed pixel length.</summary>
    Pixel,

    /// <summary>Use the desired size of the content.</summary>
    Auto,

    /// <summary>Use a weighted share of remaining space.</summary>
    Star,
}

/// <summary>
/// Represents a grid row or column length.
/// </summary>
/// <param name="Value">The unit value.</param>
/// <param name="UnitType">The kind of grid unit.</param>
public readonly record struct GridLength(float Value, GridUnitType UnitType)
{
    /// <summary>
    /// Gets an automatic grid length.
    /// </summary>
    public static GridLength Auto { get; } = new(1f, GridUnitType.Auto);

    /// <summary>
    /// Creates a weighted star grid length.
    /// </summary>
    /// <param name="value">The star weight.</param>
    /// <returns>A star grid length.</returns>
    public static GridLength Star(float value = 1f) => new(value, GridUnitType.Star);

    /// <summary>
    /// Creates a fixed pixel grid length.
    /// </summary>
    /// <param name="value">The length in DIPs.</param>
    /// <returns>A fixed grid length.</returns>
    public static GridLength Pixel(float value) => new(value, GridUnitType.Pixel);
}

/// <summary>
/// Defines a grid row or column.
/// </summary>
public sealed class GridDefinition
{
    /// <summary>
    /// Initializes a grid definition.
    /// </summary>
    /// <param name="length">The row or column length.</param>
    public GridDefinition(GridLength length)
    {
        Length = length;
    }

    /// <summary>
    /// Gets or sets the row or column length.
    /// </summary>
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
    private const float InputBoundaryEpsilon = 0.001f;

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

    public static bool ContainsInputBand(RectF rect, PointF point)
        => point.X >= rect.X - InputBoundaryEpsilon
            && point.Y >= rect.Y - InputBoundaryEpsilon
            && point.X < rect.X + rect.Width + InputBoundaryEpsilon
            && point.Y < rect.Y + rect.Height + InputBoundaryEpsilon;

    public static int VisibleUniformBandCount(float extent, float bandExtent, int count)
        => count <= 0 || extent <= 0f || bandExtent <= 0f || !float.IsFinite(extent) || !float.IsFinite(bandExtent)
            ? 0
            : Math.Min(count, (int)MathF.Floor((extent + InputBoundaryEpsilon) / bandExtent));

    public static int UniformBandIndex(float coordinate, float origin, float bandExtent, int count)
    {
        if (count <= 0 || bandExtent <= 0f || !float.IsFinite(coordinate) || !float.IsFinite(origin) || !float.IsFinite(bandExtent))
        {
            return -1;
        }

        float relative = coordinate - origin;
        if (relative < -InputBoundaryEpsilon)
        {
            return -1;
        }

        float totalExtent = count * bandExtent;
        if (relative >= totalExtent + InputBoundaryEpsilon)
        {
            return -1;
        }

        if (relative <= InputBoundaryEpsilon)
        {
            return 0;
        }

        int index = (int)MathF.Floor((relative - InputBoundaryEpsilon) / bandExtent);
        return index >= 0 && index < count ? index : -1;
    }

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
