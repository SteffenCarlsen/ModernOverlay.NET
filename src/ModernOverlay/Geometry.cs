using System.Numerics;

namespace ModernOverlay;

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

public readonly record struct PointF(float X, float Y);

public readonly record struct SizeF(float Width, float Height);

public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct ColorRgba(float R, float G, float B, float A)
{
    public static ColorRgba Transparent => new(0f, 0f, 0f, 0f);
    public static ColorRgba White => new(1f, 1f, 1f, 1f);
    public static ColorRgba Black => new(0f, 0f, 0f, 1f);

    public static ColorRgba FromBytes(byte r, byte g, byte b, byte a = byte.MaxValue)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);
}

public readonly record struct Matrix3x2F(float M11, float M12, float M21, float M22, float M31, float M32)
{
    public static Matrix3x2F Identity => new(1f, 0f, 0f, 1f, 0f, 0f);

    public static Matrix3x2F CreateTranslation(float x, float y) => new(1f, 0f, 0f, 1f, x, y);

    public Matrix3x2 ToSystemMatrix() => new(M11, M12, M21, M22, M31, M32);
}
