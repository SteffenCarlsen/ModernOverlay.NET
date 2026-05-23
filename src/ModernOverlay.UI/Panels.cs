using System.Runtime.CompilerServices;

namespace ModernOverlay.UI;

public sealed class Canvas : UiPanel
{
    private static readonly ConditionalWeakTable<UiElement, CanvasPlacement> Placements = [];

    public static void SetLeft(UiElement element, float value) => SetCoordinate(element, placement => placement.Left = Validate(value));

    public static void SetTop(UiElement element, float value) => SetCoordinate(element, placement => placement.Top = Validate(value));

    public static void SetRight(UiElement element, float value) => SetCoordinate(element, placement => placement.Right = Validate(value));

    public static void SetBottom(UiElement element, float value) => SetCoordinate(element, placement => placement.Bottom = Validate(value));

    public static void ClearLeft(UiElement element) => SetCoordinate(element, placement => placement.Left = null);

    public static void ClearTop(UiElement element) => SetCoordinate(element, placement => placement.Top = null);

    public static void ClearRight(UiElement element) => SetCoordinate(element, placement => placement.Right = null);

    public static void ClearBottom(UiElement element) => SetCoordinate(element, placement => placement.Bottom = null);

    public static float GetLeft(UiElement element) => GetPlacement(element).Left ?? 0f;

    public static float GetTop(UiElement element) => GetPlacement(element).Top ?? 0f;

    public static float? GetRight(UiElement element) => GetPlacement(element).Right;

    public static float? GetBottom(UiElement element) => GetPlacement(element).Bottom;

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        float width = 0f;
        float height = 0f;
        SizeF childAvailable = UiGeometry.Deflate(availableSize, Padding);
        foreach (UiElement child in Children)
        {
            SizeF desired = child.Measure(childAvailable);
            CanvasPlacement placement = GetPlacement(child);
            width = MathF.Max(width, placement.DesiredExtentX(desired.Width));
            height = MathF.Max(height, placement.DesiredExtentY(desired.Height));
        }

        return new SizeF(width + Padding.Horizontal, height + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        foreach (UiElement child in Children)
        {
            CanvasPlacement placement = GetPlacement(child);
            RectF childRect = placement.Arrange(content, child.DesiredSize);
            child.Arrange(childRect);
        }
    }

    private static CanvasPlacement GetPlacement(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Placements.GetOrCreateValue(element);
    }

    private static void SetCoordinate(UiElement element, Action<CanvasPlacement> set)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(set);
        set(GetPlacement(element));
        element.Parent?.InvalidateMeasure();
    }

    private static float Validate(float value)
    {
        return value >= 0f && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Canvas coordinates must be finite and non-negative.");
    }

    private sealed class CanvasPlacement
    {
        public float? Left { get; set; }

        public float? Top { get; set; }

        public float? Right { get; set; }

        public float? Bottom { get; set; }

        public float DesiredExtentX(float desiredWidth)
            => Left.GetValueOrDefault() + desiredWidth + Right.GetValueOrDefault();

        public float DesiredExtentY(float desiredHeight)
            => Top.GetValueOrDefault() + desiredHeight + Bottom.GetValueOrDefault();

        public RectF Arrange(RectF content, SizeF desiredSize)
        {
            float x = ResolveStart(content.X, content.Width, desiredSize.Width, Left, Right);
            float y = ResolveStart(content.Y, content.Height, desiredSize.Height, Top, Bottom);
            float width = ResolveSize(content.Width, desiredSize.Width, Left, Right);
            float height = ResolveSize(content.Height, desiredSize.Height, Top, Bottom);
            return new RectF(x, y, width, height);
        }

        private static float ResolveStart(float origin, float available, float desired, float? start, float? end)
            => start.HasValue
                ? origin + start.Value
                : end.HasValue
                    ? origin + MathF.Max(0f, available - end.Value - MathF.Min(desired, available))
                    : origin;

        private static float ResolveSize(float available, float desired, float? start, float? end)
            => start.HasValue && end.HasValue
                ? MathF.Max(0f, available - start.Value - end.Value)
                : MathF.Min(desired, available);
    }
}

public sealed class StackPanel : UiPanel
{
    private UiOrientation orientation;
    private float spacing;

    public UiOrientation Orientation
    {
        get => orientation;
        set => SetProperty(ref orientation, value, UiInvalidation.Measure);
    }

    public float Spacing
    {
        get => spacing;
        set
        {
            if (value < 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spacing must be finite and non-negative.");
            }

            SetProperty(ref spacing, value, UiInvalidation.Measure);
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentAvailable = UiGeometry.Deflate(availableSize, Padding);
        float primary = 0f;
        float cross = 0f;
        int visibleCount = 0;
        foreach (UiElement child in Children)
        {
            SizeF childSize = child.Measure(contentAvailable);
            if (child.Visibility == UiVisibility.Collapsed)
            {
                continue;
            }

            visibleCount++;
            if (Orientation == UiOrientation.Vertical)
            {
                primary += childSize.Height;
                cross = MathF.Max(cross, childSize.Width);
            }
            else
            {
                primary += childSize.Width;
                cross = MathF.Max(cross, childSize.Height);
            }
        }

        primary += MathF.Max(0, visibleCount - 1) * Spacing;
        return Orientation == UiOrientation.Vertical
            ? new SizeF(cross + Padding.Horizontal, primary + Padding.Vertical)
            : new SizeF(primary + Padding.Horizontal, cross + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        float cursor = Orientation == UiOrientation.Vertical ? content.Y : content.X;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(new RectF(content.X, content.Y, 0f, 0f));
                continue;
            }

            if (Orientation == UiOrientation.Vertical)
            {
                child.Arrange(new RectF(content.X, cursor, content.Width, child.DesiredSize.Height));
                cursor += child.DesiredSize.Height + Spacing;
            }
            else
            {
                child.Arrange(new RectF(cursor, content.Y, child.DesiredSize.Width, content.Height));
                cursor += child.DesiredSize.Width + Spacing;
            }
        }
    }
}

public sealed class DockPanel : UiPanel
{
    private static readonly ConditionalWeakTable<UiElement, DockPlacement> Placements = [];
    private bool fillLastChild = true;

    public bool FillLastChild
    {
        get => fillLastChild;
        set => SetProperty(ref fillLastChild, value, UiInvalidation.Measure | UiInvalidation.Arrange);
    }

    public static void SetDock(UiElement element, Dock dock) => GetPlacement(element).Dock = dock;

    public static Dock GetDock(UiElement element) => GetPlacement(element).Dock;

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentAvailable = UiGeometry.Deflate(availableSize, Padding);
        float width = 0f;
        float height = 0f;
        float remainingWidth = contentAvailable.Width;
        float remainingHeight = contentAvailable.Height;
        foreach (UiElement child in Children)
        {
            Dock dock = GetDock(child);
            SizeF childSize = child.Measure(new SizeF(remainingWidth, remainingHeight));
            if (dock is Dock.Left or Dock.Right)
            {
                width += childSize.Width;
                remainingWidth = MathF.Max(0f, remainingWidth - childSize.Width);
                height = MathF.Max(height, childSize.Height);
            }
            else
            {
                height += childSize.Height;
                remainingHeight = MathF.Max(0f, remainingHeight - childSize.Height);
                width = MathF.Max(width, childSize.Width);
            }
        }

        return new SizeF(width + Padding.Horizontal, height + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF remaining = ContentBounds;
        int index = 0;
        int count = Children.Count;
        foreach (UiElement child in Children)
        {
            bool fill = FillLastChild && index == count - 1;
            Dock dock = GetDock(child);
            RectF childRect;
            if (fill)
            {
                childRect = remaining;
            }
            else if (dock == Dock.Left)
            {
                childRect = remaining with { Width = MathF.Min(child.DesiredSize.Width, remaining.Width) };
                remaining = new RectF(remaining.X + childRect.Width, remaining.Y, MathF.Max(0f, remaining.Width - childRect.Width), remaining.Height);
            }
            else if (dock == Dock.Right)
            {
                float width = MathF.Min(child.DesiredSize.Width, remaining.Width);
                childRect = new RectF(remaining.X + remaining.Width - width, remaining.Y, width, remaining.Height);
                remaining = remaining with { Width = MathF.Max(0f, remaining.Width - width) };
            }
            else if (dock == Dock.Top)
            {
                childRect = remaining with { Height = MathF.Min(child.DesiredSize.Height, remaining.Height) };
                remaining = new RectF(remaining.X, remaining.Y + childRect.Height, remaining.Width, MathF.Max(0f, remaining.Height - childRect.Height));
            }
            else
            {
                float height = MathF.Min(child.DesiredSize.Height, remaining.Height);
                childRect = new RectF(remaining.X, remaining.Y + remaining.Height - height, remaining.Width, height);
                remaining = remaining with { Height = MathF.Max(0f, remaining.Height - height) };
            }

            child.Arrange(childRect);
            index++;
        }
    }

    private static DockPlacement GetPlacement(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Placements.GetOrCreateValue(element);
    }

    private sealed class DockPlacement
    {
        public Dock Dock { get; set; } = Dock.Left;
    }
}

public sealed class WrapPanel : UiPanel
{
    private float spacing;

    public float Spacing
    {
        get => spacing;
        set
        {
            if (value < 0f || !float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spacing must be finite and non-negative.");
            }

            SetProperty(ref spacing, value, UiInvalidation.Measure);
        }
    }

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentAvailable = UiGeometry.Deflate(availableSize, Padding);
        float rowWidth = 0f;
        float rowHeight = 0f;
        float width = 0f;
        float height = 0f;
        foreach (UiElement child in Children)
        {
            SizeF childSize = child.Measure(contentAvailable);
            bool wrap = rowWidth > 0f && rowWidth + Spacing + childSize.Width > contentAvailable.Width;
            if (wrap)
            {
                width = MathF.Max(width, rowWidth);
                height += rowHeight + Spacing;
                rowWidth = 0f;
                rowHeight = 0f;
            }

            rowWidth = rowWidth == 0f ? childSize.Width : rowWidth + Spacing + childSize.Width;
            rowHeight = MathF.Max(rowHeight, childSize.Height);
        }

        width = MathF.Max(width, rowWidth);
        height += rowHeight;
        return new SizeF(width + Padding.Horizontal, height + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        float x = content.X;
        float y = content.Y;
        float rowHeight = 0f;
        foreach (UiElement child in Children)
        {
            SizeF childSize = child.DesiredSize;
            if (x > content.X && x + childSize.Width > content.X + content.Width)
            {
                x = content.X;
                y += rowHeight + Spacing;
                rowHeight = 0f;
            }

            child.Arrange(new RectF(x, y, childSize.Width, childSize.Height));
            x += childSize.Width + Spacing;
            rowHeight = MathF.Max(rowHeight, childSize.Height);
        }
    }
}

public sealed class Grid : UiPanel
{
    private static readonly ConditionalWeakTable<UiElement, GridPlacement> Placements = [];

    public IList<GridDefinition> Rows { get; } = [];

    public IList<GridDefinition> Columns { get; } = [];

    public static void SetRow(UiElement element, int row) => GetPlacement(element).Row = ValidateIndex(row);

    public static void SetColumn(UiElement element, int column) => GetPlacement(element).Column = ValidateIndex(column);

    public static int GetRow(UiElement element) => GetPlacement(element).Row;

    public static int GetColumn(UiElement element) => GetPlacement(element).Column;

    protected override SizeF MeasureCore(SizeF availableSize)
    {
        SizeF contentAvailable = UiGeometry.Deflate(availableSize, Padding);
        GridDefinition[] rows = EffectiveRows();
        GridDefinition[] columns = EffectiveColumns();
        float[] rowSizes = ResolveInitialSizes(rows);
        float[] columnSizes = ResolveInitialSizes(columns);

        foreach (UiElement child in Children)
        {
            GridPlacement placement = GetPlacement(child);
            int row = Math.Min(placement.Row, rows.Length - 1);
            int column = Math.Min(placement.Column, columns.Length - 1);
            SizeF childSize = child.Measure(contentAvailable);
            if (rows[row].Length.UnitType == GridUnitType.Auto)
            {
                rowSizes[row] = MathF.Max(rowSizes[row], childSize.Height);
            }

            if (columns[column].Length.UnitType == GridUnitType.Auto)
            {
                columnSizes[column] = MathF.Max(columnSizes[column], childSize.Width);
            }
        }

        AllocateStar(rows, rowSizes, contentAvailable.Height);
        AllocateStar(columns, columnSizes, contentAvailable.Width);
        return new SizeF(columnSizes.Sum() + Padding.Horizontal, rowSizes.Sum() + Padding.Vertical);
    }

    protected override void ArrangeCore(RectF finalRect)
    {
        RectF content = ContentBounds;
        GridDefinition[] rows = EffectiveRows();
        GridDefinition[] columns = EffectiveColumns();
        float[] rowSizes = ResolveInitialSizes(rows);
        float[] columnSizes = ResolveInitialSizes(columns);

        foreach (UiElement child in Children)
        {
            GridPlacement placement = GetPlacement(child);
            int row = Math.Min(placement.Row, rows.Length - 1);
            int column = Math.Min(placement.Column, columns.Length - 1);
            if (rows[row].Length.UnitType == GridUnitType.Auto)
            {
                rowSizes[row] = MathF.Max(rowSizes[row], child.DesiredSize.Height);
            }

            if (columns[column].Length.UnitType == GridUnitType.Auto)
            {
                columnSizes[column] = MathF.Max(columnSizes[column], child.DesiredSize.Width);
            }
        }

        AllocateStar(rows, rowSizes, content.Height);
        AllocateStar(columns, columnSizes, content.Width);

        foreach (UiElement child in Children)
        {
            GridPlacement placement = GetPlacement(child);
            int row = Math.Min(placement.Row, rows.Length - 1);
            int column = Math.Min(placement.Column, columns.Length - 1);
            float x = content.X + columnSizes.Take(column).Sum();
            float y = content.Y + rowSizes.Take(row).Sum();
            child.Arrange(new RectF(x, y, columnSizes[column], rowSizes[row]));
        }
    }

    private GridDefinition[] EffectiveRows() => Rows.Count == 0 ? [new GridDefinition(GridLength.Star())] : Rows.ToArray();

    private GridDefinition[] EffectiveColumns() => Columns.Count == 0 ? [new GridDefinition(GridLength.Star())] : Columns.ToArray();

    private static float[] ResolveInitialSizes(GridDefinition[] definitions)
        => definitions
            .Select(definition => definition.Length.UnitType == GridUnitType.Pixel ? definition.Length.Value : 0f)
            .ToArray();

    private static void AllocateStar(GridDefinition[] definitions, float[] sizes, float available)
    {
        float used = sizes.Sum();
        float remaining = MathF.Max(0f, available - used);
        float totalStar = definitions
            .Where(definition => definition.Length.UnitType == GridUnitType.Star)
            .Sum(definition => MathF.Max(0f, definition.Length.Value));
        if (totalStar <= 0f)
        {
            return;
        }

        for (int index = 0; index < definitions.Length; index++)
        {
            if (definitions[index].Length.UnitType == GridUnitType.Star)
            {
                sizes[index] = remaining * MathF.Max(0f, definitions[index].Length.Value) / totalStar;
            }
        }
    }

    private static GridPlacement GetPlacement(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Placements.GetOrCreateValue(element);
    }

    private static int ValidateIndex(int index)
        => index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(index), "Grid indexes must be non-negative.");

    private sealed class GridPlacement
    {
        public int Row { get; set; }

        public int Column { get; set; }
    }
}
