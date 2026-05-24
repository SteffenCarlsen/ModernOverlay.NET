namespace ModernOverlay.Windows;

public enum OverlayInputRegionResult
{
    PassThrough,
    Interactive,
}

public interface IOverlayInputRegionResolver
{
    OverlayInputRegionResult ResolveInputRegion(PointF position);
}
