namespace ModernOverlay.UI;

/// <summary>
/// Identifies an anchor point on an overlay, target rectangle, owner element, or popup.
/// </summary>
public enum OverlayAnchor
{
    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The center point of the top edge.</summary>
    Top,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The center point of the left edge.</summary>
    Left,

    /// <summary>The center point.</summary>
    Center,

    /// <summary>The center point of the right edge.</summary>
    Right,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,

    /// <summary>The center point of the bottom edge.</summary>
    Bottom,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,
}

/// <summary>
/// Describes how a <see cref="UiPlacement"/> resolves to an overlay-local position.
/// </summary>
public enum UiPlacementKind
{
    /// <summary>Use explicit overlay-local bounds.</summary>
    Manual,

    /// <summary>Anchor the element to the overlay bounds.</summary>
    Anchor,

    /// <summary>Anchor the element to the tracked target bounds.</summary>
    TargetAnchor,

    /// <summary>Anchor the element to the last known pointer position.</summary>
    Cursor,

    /// <summary>Restore placement from an <see cref="IUiLayoutStore"/> with a fallback placement.</summary>
    Persisted,
}

/// <summary>
/// Describes dynamic or persisted placement for floating UI elements.
/// </summary>
/// <param name="Kind">The placement resolution mode.</param>
/// <param name="Bounds">Manual overlay-local bounds used by manual and restored persisted placements.</param>
/// <param name="Anchor">The anchor point used by anchor-based placements.</param>
/// <param name="Margin">Anchor margin or cursor offset in DIPs.</param>
/// <param name="PersistenceKey">The optional layout-store key used by persisted placements.</param>
public readonly record struct UiPlacement(
    UiPlacementKind Kind,
    RectF Bounds,
    OverlayAnchor Anchor,
    Thickness Margin,
    string? PersistenceKey)
{
    /// <summary>
    /// Creates a placement with explicit overlay-local bounds in DIPs.
    /// </summary>
    /// <param name="x">The overlay-local X coordinate.</param>
    /// <param name="y">The overlay-local Y coordinate.</param>
    /// <param name="width">The desired width in DIPs.</param>
    /// <param name="height">The desired height in DIPs.</param>
    /// <returns>A manual placement descriptor.</returns>
    public static UiPlacement Manual(float x, float y, float width, float height)
        => new(UiPlacementKind.Manual, new RectF(x, y, width, height), OverlayAnchor.TopLeft, Thickness.Zero, null);

    /// <summary>
    /// Creates a placement anchored to the overlay bounds.
    /// </summary>
    /// <param name="anchor">The overlay anchor to resolve against.</param>
    /// <param name="margin">The margin from the selected anchor.</param>
    /// <returns>An overlay-anchored placement descriptor.</returns>
    public static UiPlacement AnchorTo(OverlayAnchor anchor, Thickness margin)
        => new(UiPlacementKind.Anchor, default, anchor, margin, null);

    /// <summary>
    /// Creates a placement anchored to the currently tracked target bounds.
    /// </summary>
    /// <param name="anchor">The target-bounds anchor to resolve against.</param>
    /// <param name="margin">The margin from the selected anchor.</param>
    /// <returns>A target-anchored placement descriptor.</returns>
    public static UiPlacement TargetAnchor(OverlayAnchor anchor, Thickness margin)
        => new(UiPlacementKind.TargetAnchor, default, anchor, margin, null);

    /// <summary>
    /// Creates a placement relative to the last known overlay-local pointer position.
    /// </summary>
    /// <param name="offset">The pointer offset in DIPs.</param>
    /// <returns>A cursor-relative placement descriptor.</returns>
    public static UiPlacement Cursor(Thickness offset)
        => new(UiPlacementKind.Cursor, default, OverlayAnchor.TopLeft, offset, null);

    /// <summary>
    /// Creates a placement that restores from a layout store before falling back to another placement.
    /// </summary>
    /// <param name="key">The non-empty persistence key.</param>
    /// <param name="fallback">The fallback placement used when no stored placement exists.</param>
    /// <returns>A persisted placement descriptor.</returns>
    public static UiPlacement Persisted(string key, UiPlacement fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return fallback with { Kind = UiPlacementKind.Persisted, PersistenceKey = key };
    }
}

/// <summary>
/// Provides application-owned storage for persisted UI placement.
/// </summary>
public interface IUiLayoutStore
{
    /// <summary>
    /// Attempts to load a placement for the specified key.
    /// </summary>
    /// <param name="key">The persistence key.</param>
    /// <param name="placement">The loaded placement when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a placement was found; otherwise, <see langword="false"/>.</returns>
    bool TryLoad(string key, out UiPlacement placement);

    /// <summary>
    /// Saves a placement for the specified key.
    /// </summary>
    /// <param name="key">The persistence key.</param>
    /// <param name="placement">The placement to store.</param>
    void Save(string key, UiPlacement placement);
}
