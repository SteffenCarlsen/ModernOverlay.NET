namespace ModernOverlay.UI;

public enum OverlayAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

public enum UiPlacementKind
{
    Manual,
    Anchor,
    TargetAnchor,
    Cursor,
    Persisted,
}

public readonly record struct UiPlacement(
    UiPlacementKind Kind,
    RectF Bounds,
    OverlayAnchor Anchor,
    Thickness Margin,
    string? PersistenceKey)
{
    public static UiPlacement Manual(float x, float y, float width, float height)
        => new(UiPlacementKind.Manual, new RectF(x, y, width, height), OverlayAnchor.TopLeft, Thickness.Zero, null);

    public static UiPlacement AnchorTo(OverlayAnchor anchor, Thickness margin)
        => new(UiPlacementKind.Anchor, default, anchor, margin, null);

    public static UiPlacement TargetAnchor(OverlayAnchor anchor, Thickness margin)
        => new(UiPlacementKind.TargetAnchor, default, anchor, margin, null);

    public static UiPlacement Cursor(Thickness offset)
        => new(UiPlacementKind.Cursor, default, OverlayAnchor.TopLeft, offset, null);

    public static UiPlacement Persisted(string key, UiPlacement fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return fallback with { Kind = UiPlacementKind.Persisted, PersistenceKey = key };
    }
}

public interface IUiLayoutStore
{
    bool TryLoad(string key, out UiPlacement placement);

    void Save(string key, UiPlacement placement);
}
