namespace ModernOverlay.Windows;

public readonly record struct WindowHandle(nint Value)
{
    public bool IsNull => Value == 0;
}

public readonly record struct WindowBounds(int X, int Y, int Width, int Height)
{
    public static WindowBounds Empty => new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static WindowBounds FromPixels(int x, int y, int width, int height)
        => new(x, y, width, height);

    public static WindowBounds FromDips(float x, float y, float width, float height, DpiScale dpi)
        => dpi.DipsToPixels(new RectF(x, y, width, height));

    public static WindowBounds FromDips(RectF bounds, DpiScale dpi)
        => dpi.DipsToPixels(bounds);
}

public readonly record struct DpiScale(float X, float Y)
{
    public static DpiScale Default => new(1f, 1f);

    public RectF PixelsToDips(WindowBounds bounds)
        => new(bounds.X / X, bounds.Y / Y, bounds.Width / X, bounds.Height / Y);

    public WindowBounds DipsToPixels(RectF bounds)
    {
        return float.IsFinite(X) && X > 0f && float.IsFinite(Y) && Y > 0f
            ? new WindowBounds(
            RoundDipToPixel(bounds.X, X),
            RoundDipToPixel(bounds.Y, Y),
            RoundDipToPixel(bounds.Width, X),
            RoundDipToPixel(bounds.Height, Y))
            : throw new InvalidOperationException("DPI scale values must be finite and greater than zero.");
    }

    private static int RoundDipToPixel(float value, float scale)
        => checked((int)MathF.Round(value * scale, MidpointRounding.AwayFromZero));
}
