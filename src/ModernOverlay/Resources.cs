namespace ModernOverlay;

public abstract class OverlayResourceHandle : IDisposable
{
    private bool disposed;

    protected OverlayResourceHandle(long generation)
    {
        Generation = generation;
    }

    public long Generation { get; }

    public bool IsDisposed => disposed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    protected virtual void DisposeCore()
    {
    }
}

public sealed class SolidBrushHandle : OverlayResourceHandle
{
    internal SolidBrushHandle(ColorRgba color, long generation)
        : base(generation)
    {
        Color = color;
    }

    public ColorRgba Color { get; }
}

public sealed class LinearGradientBrushHandle : OverlayResourceHandle
{
    internal LinearGradientBrushHandle(long generation)
        : base(generation)
    {
    }
}

public sealed class FontHandle : OverlayResourceHandle
{
    internal FontHandle(FontOptions options, long generation)
        : base(generation)
    {
        Options = options;
    }

    public FontOptions Options { get; }
}

public sealed class ImageHandle : OverlayResourceHandle
{
    internal ImageHandle(string path, long generation)
        : base(generation)
    {
        Path = path;
    }

    public string Path { get; }
}

public sealed class GeometryPath : OverlayResourceHandle
{
    internal GeometryPath(long generation)
        : base(generation)
    {
    }
}

public sealed class StrokeStyleHandle : OverlayResourceHandle
{
    internal StrokeStyleHandle(long generation)
        : base(generation)
    {
    }
}

public sealed class TextLayoutHandle : OverlayResourceHandle
{
    internal TextLayoutHandle(string text, FontHandle font, long generation)
        : base(generation)
    {
        Text = text;
        Font = font;
    }

    public string Text { get; }

    public FontHandle Font { get; }
}

public sealed record FontOptions(string FamilyName, float Size);

public sealed class OverlayResourceManager
{
    private long generation = 1;

    public long CurrentGeneration => generation;

    public SolidBrushHandle CreateSolidBrush(ColorRgba color) => new(color, generation);

    public FontHandle CreateFont(FontOptions options) => new(options, generation);

    public ImageHandle CreateImage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ImageHandle(path, generation);
    }

    public TextLayoutHandle CreateTextLayout(string text, FontHandle font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new TextLayoutHandle(text, font, generation);
    }

    internal void AdvanceGeneration() => generation++;
}
