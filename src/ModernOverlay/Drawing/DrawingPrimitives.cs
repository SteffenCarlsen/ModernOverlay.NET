using System.Numerics;

namespace ModernOverlay.Drawing;

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
