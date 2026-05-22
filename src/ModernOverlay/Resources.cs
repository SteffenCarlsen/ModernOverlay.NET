using ModernOverlay.Diagnostics;

using System.Diagnostics;

namespace ModernOverlay;

public abstract class OverlayResourceHandle : IDisposable
{
    private readonly System.Threading.Lock nativeRealizationsGate = new();
    private readonly Dictionary<long, OverlayNativeResourceSnapshot> nativeRealizations = [];
    private readonly Action<long>? unregister;
    private bool disposed;
    private long nextNativeRealizationId = 1;

    protected OverlayResourceHandle(long id, string kind, long generation, Action<long>? unregister)
    {
        Id = id;
        Kind = kind;
        Generation = generation;
        this.unregister = unregister;
    }

    public long Id { get; }

    public string Kind { get; }

    public long Generation { get; }

    public bool IsDisposed => disposed;

    internal string? AllocationSite { get; set; }

    public IReadOnlyList<OverlayNativeResourceSnapshot> NativeRealizations
    {
        get
        {
            lock (nativeRealizationsGate)
            {
                return nativeRealizations.Values.ToArray();
            }
        }
    }

    public int NativeRealizationCount
    {
        get
        {
            lock (nativeRealizationsGate)
            {
                return nativeRealizations.Count;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeCore();
        unregister?.Invoke(Id);
        GC.SuppressFinalize(this);
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    protected virtual void DisposeCore()
    {
    }

    internal IDisposable RegisterNativeRealization(string backendName, long backendGeneration, string resourceKind)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKind);

        long realizationId;
        lock (nativeRealizationsGate)
        {
            realizationId = nextNativeRealizationId++;
            nativeRealizations.Add(
                realizationId,
                new OverlayNativeResourceSnapshot(backendName, backendGeneration, resourceKind));
        }

        return new NativeRealizationRegistration(this, realizationId);
    }

    private void UnregisterNativeRealization(long realizationId)
    {
        lock (nativeRealizationsGate)
        {
            nativeRealizations.Remove(realizationId);
        }
    }

    private sealed class NativeRealizationRegistration : IDisposable
    {
        private OverlayResourceHandle? owner;
        private readonly long realizationId;

        public NativeRealizationRegistration(OverlayResourceHandle owner, long realizationId)
        {
            this.owner = owner;
            this.realizationId = realizationId;
        }

        public void Dispose()
        {
            if (owner is { } activeOwner)
            {
                activeOwner.UnregisterNativeRealization(realizationId);
                owner = null;
            }
        }
    }
}

public abstract class BrushHandle : OverlayResourceHandle
{
    protected BrushHandle(long id, string kind, long generation, Action<long>? unregister)
        : base(id, kind, generation, unregister)
    {
    }
}

public sealed class SolidBrushHandle : BrushHandle
{
    internal SolidBrushHandle(long id, ColorRgba color, long generation, Action<long>? unregister)
        : base(id, "SolidBrush", generation, unregister)
    {
        Color = color;
    }

    public ColorRgba Color { get; }
}

public sealed class LinearGradientBrushHandle : BrushHandle
{
    internal LinearGradientBrushHandle(long id, LinearGradientBrushOptions options, long generation, Action<long>? unregister)
        : base(id, "LinearGradientBrush", generation, unregister)
    {
        Options = options;
    }

    public LinearGradientBrushOptions Options { get; }
}

public sealed class FontHandle : OverlayResourceHandle
{
    internal FontHandle(long id, FontOptions options, long generation, Action<long>? unregister)
        : base(id, "Font", generation, unregister)
    {
        Options = options;
    }

    public FontOptions Options { get; }
}

public sealed class ImageHandle : OverlayResourceHandle
{
    internal ImageHandle(long id, string path, long generation, Action<long>? unregister)
        : base(id, "Image", generation, unregister)
    {
        SourceKind = ImageSourceKind.Path;
        Path = path;
    }

    internal ImageHandle(long id, byte[] encodedBytes, long generation, Action<long>? unregister)
        : base(id, "Image", generation, unregister)
    {
        SourceKind = ImageSourceKind.EncodedBytes;
        EncodedBytes = encodedBytes;
    }

    public string? Path { get; }

    internal ImageSourceKind SourceKind { get; }

    internal byte[]? EncodedBytes { get; }
}

internal enum ImageSourceKind
{
    Path,
    EncodedBytes,
}

public sealed class GeometryPath : OverlayResourceHandle
{
    internal GeometryPath(long id, IReadOnlyList<GeometryPathCommand> commands, long generation, Action<long>? unregister)
        : base(id, "GeometryPath", generation, unregister)
    {
        Commands = commands;
    }

    internal IReadOnlyList<GeometryPathCommand> Commands { get; }
}

public sealed class StrokeStyleHandle : OverlayResourceHandle
{
    internal StrokeStyleHandle(long id, StrokeStyleOptions options, long generation, Action<long>? unregister)
        : base(id, "StrokeStyle", generation, unregister)
    {
        Options = options;
    }

    public StrokeStyleOptions Options { get; }
}

public sealed class TextLayoutHandle : OverlayResourceHandle
{
    internal TextLayoutHandle(long id, string text, FontHandle font, TextLayoutOptions options, long generation, Action<long>? unregister)
        : base(id, "TextLayout", generation, unregister)
    {
        Text = text;
        Font = font;
        Options = options;
    }

    public string Text { get; }

    public FontHandle Font { get; }

    public TextLayoutOptions Options { get; }
}

public sealed record FontOptions(string FamilyName, float Size);

public sealed record TextLayoutOptions
{
    public static TextLayoutOptions Default { get; } = new();

    public float MaxWidth { get; init; } = 4096f;

    public float MaxHeight { get; init; } = 4096f;

    public TextWrapping Wrapping { get; init; } = TextWrapping.Wrap;

    public TextHorizontalAlignment HorizontalAlignment { get; init; } = TextHorizontalAlignment.Leading;

    public TextVerticalAlignment VerticalAlignment { get; init; } = TextVerticalAlignment.Near;

    public TextTrimming Trimming { get; init; } = TextTrimming.None;
}

public enum TextWrapping
{
    Wrap,
    NoWrap,
    EmergencyBreak,
    WholeWord,
    Character,
}

public enum TextHorizontalAlignment
{
    Leading,
    Center,
    Trailing,
    Justified,
}

public enum TextVerticalAlignment
{
    Near,
    Center,
    Far,
}

public enum TextTrimming
{
    None,
    Character,
    Word,
}

public sealed record GradientStop(float Position, ColorRgba Color);

public sealed record LinearGradientBrushOptions(PointF Start, PointF End, IReadOnlyList<GradientStop> Stops);

internal enum GeometryPathCommandKind
{
    MoveTo,
    LineTo,
    CubicBezierTo,
    QuadraticBezierTo,
    ArcTo,
    Close,
}

internal readonly record struct GeometryPathCommand(
    GeometryPathCommandKind Kind,
    PointF Point,
    PointF ControlPoint1 = default,
    PointF ControlPoint2 = default,
    SizeF Size = default,
    float RotationAngle = 0f,
    GeometrySweepDirection SweepDirection = GeometrySweepDirection.Clockwise,
    GeometryArcSize ArcSize = GeometryArcSize.Small);

public enum GeometrySweepDirection
{
    CounterClockwise,
    Clockwise,
}

public enum GeometryArcSize
{
    Small,
    Large,
}

public sealed class GeometryPathBuilder
{
    private readonly List<GeometryPathCommand> commands = [];
    private bool hasOpenFigure;

    public GeometryPathBuilder MoveTo(PointF point)
    {
        if (hasOpenFigure)
        {
            throw new InvalidOperationException("Close the current geometry figure before starting another one.");
        }

        commands.Add(new GeometryPathCommand(GeometryPathCommandKind.MoveTo, point));
        hasOpenFigure = true;
        return this;
    }

    public GeometryPathBuilder LineTo(PointF point)
    {
        if (!hasOpenFigure)
        {
            throw new InvalidOperationException("Geometry paths must begin with MoveTo before LineTo.");
        }

        commands.Add(new GeometryPathCommand(GeometryPathCommandKind.LineTo, point));
        return this;
    }

    public GeometryPathBuilder BezierTo(PointF controlPoint1, PointF controlPoint2, PointF endPoint)
    {
        EnsureOpenFigure(nameof(BezierTo));
        commands.Add(new GeometryPathCommand(GeometryPathCommandKind.CubicBezierTo, endPoint, controlPoint1, controlPoint2));
        return this;
    }

    public GeometryPathBuilder QuadraticBezierTo(PointF controlPoint, PointF endPoint)
    {
        EnsureOpenFigure(nameof(QuadraticBezierTo));
        commands.Add(new GeometryPathCommand(GeometryPathCommandKind.QuadraticBezierTo, endPoint, controlPoint));
        return this;
    }

    public GeometryPathBuilder ArcTo(
        PointF endPoint,
        SizeF radius,
        float rotationAngleDegrees = 0f,
        GeometrySweepDirection sweepDirection = GeometrySweepDirection.Clockwise,
        GeometryArcSize arcSize = GeometryArcSize.Small)
    {
        EnsureOpenFigure(nameof(ArcTo));
        if (radius.Width <= 0f || radius.Height <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Arc radius must have positive width and height.");
        }

        commands.Add(new GeometryPathCommand(
            GeometryPathCommandKind.ArcTo,
            endPoint,
            Size: radius,
            RotationAngle: rotationAngleDegrees,
            SweepDirection: sweepDirection,
            ArcSize: arcSize));
        return this;
    }

    public GeometryPathBuilder Close()
    {
        if (!hasOpenFigure)
        {
            throw new InvalidOperationException("Cannot close a geometry figure before MoveTo.");
        }

        commands.Add(new GeometryPathCommand(GeometryPathCommandKind.Close, default));
        hasOpenFigure = false;
        return this;
    }

    internal IReadOnlyList<GeometryPathCommand> Build()
    {
        return commands.Count > 0
            ? commands.ToArray()
            : throw new InvalidOperationException("Geometry paths require at least one figure.");
    }

    private void EnsureOpenFigure(string operation)
    {
        if (!hasOpenFigure)
        {
            throw new InvalidOperationException($"Geometry paths must begin with MoveTo before {operation}.");
        }
    }
}

public enum StrokeDashStyle
{
    Solid,
    Dash,
    Dot,
    DashDot,
    DashDotDot,
    Custom,
}

public enum ImageInterpolationMode
{
    NearestNeighbor,
    Linear,
}

public sealed record StrokeStyleOptions
{
    public StrokeDashStyle DashStyle { get; init; } = StrokeDashStyle.Solid;

    public float DashOffset { get; init; }

    public IReadOnlyList<float> CustomDashes { get; init; } = [];
}

public sealed record OverlayNativeResourceSnapshot(string BackendName, long BackendGeneration, string ResourceKind);

public sealed record OverlayResourceSnapshot(
    long Id,
    string Kind,
    long Generation,
    int NativeRealizationCount,
    IReadOnlyList<OverlayNativeResourceSnapshot> NativeRealizations,
    string? AllocationSite = null);

public sealed record OverlayResourceLeakReport(int LiveCount, IReadOnlyList<OverlayResourceSnapshot> LiveResources);

public sealed class OverlayResourceManager
{
    private readonly System.Threading.Lock gate = new();
    private readonly Dictionary<long, OverlayResourceHandle> liveResources = [];
    private long generation = 1;
    private long nextResourceId = 1;
    private int renderCallbackDepth;

    public long CurrentGeneration => generation;

    internal bool RejectCreationDuringRender { get; set; }

    public SolidBrushHandle CreateSolidBrush(ColorRgba color)
        => Track(new SolidBrushHandle(GetNextResourceId(), color, generation, Unregister));

    public LinearGradientBrushHandle CreateLinearGradientBrush(LinearGradientBrushOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateLinearGradientOptions(options);
        return Track(new LinearGradientBrushHandle(
            GetNextResourceId(),
            options with { Stops = options.Stops.ToArray() },
            generation,
            Unregister));
    }

    public FontHandle CreateFont(FontOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FamilyName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Size);

        return Track(new FontHandle(GetNextResourceId(), options, generation, Unregister));
    }

    public ImageHandle CreateImage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Track(new ImageHandle(GetNextResourceId(), path, generation, Unregister));
    }

    public ImageHandle CreateImage(byte[] encodedBytes)
    {
        ArgumentNullException.ThrowIfNull(encodedBytes);
        return encodedBytes.Length > 0
            ? Track(new ImageHandle(GetNextResourceId(), encodedBytes.ToArray(), generation, Unregister))
            : throw new ArgumentException("Image byte arrays cannot be empty.", nameof(encodedBytes));
    }

    public ImageHandle CreateImage(ReadOnlyMemory<byte> encodedBytes)
    {
        return encodedBytes.Length > 0
            ? Track(new ImageHandle(GetNextResourceId(), encodedBytes.ToArray(), generation, Unregister))
            : throw new ArgumentException("Image memory buffers cannot be empty.", nameof(encodedBytes));
    }

    public ImageHandle CreateImage(Stream encodedStream)
    {
        ArgumentNullException.ThrowIfNull(encodedStream);
        using var buffer = new MemoryStream();
        encodedStream.CopyTo(buffer);
        return CreateImage(buffer.ToArray());
    }

    public TextLayoutHandle CreateTextLayout(string text, FontHandle font, TextLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ObjectDisposedException.ThrowIf(font.IsDisposed, font);
        TextLayoutOptions effectiveOptions = options ?? TextLayoutOptions.Default;
        ValidateTextLayoutOptions(effectiveOptions);

        return Track(new TextLayoutHandle(GetNextResourceId(), text, font, effectiveOptions, generation, Unregister));
    }

    public StrokeStyleHandle CreateStrokeStyle(StrokeStyleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DashStyle == StrokeDashStyle.Custom && options.CustomDashes.Count == 0)
        {
            throw new ArgumentException("Custom stroke styles require at least one dash length.", nameof(options));
        }

        foreach (float dash in options.CustomDashes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dash);
        }

        return Track(new StrokeStyleHandle(GetNextResourceId(), options, generation, Unregister));
    }

    public GeometryPath CreateGeometry(Action<GeometryPathBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var builder = new GeometryPathBuilder();
        build(builder);
        return Track(new GeometryPath(GetNextResourceId(), builder.Build(), generation, Unregister));
    }

    public OverlayResourceLeakReport CreateLeakReport()
    {
        IReadOnlyList<OverlayResourceSnapshot> resources = GetLiveResources();
        if (resources.Count > 0)
        {
            OverlayEventSource.Log.ResourceLeakDetected(resources.Count);
        }

        return new OverlayResourceLeakReport(resources.Count, resources);
    }

    public IReadOnlyList<OverlayResourceSnapshot> GetLiveResources()
    {
        lock (gate)
        {
            return liveResources.Values
                .Select(resource => new OverlayResourceSnapshot(
                    resource.Id,
                    resource.Kind,
                    resource.Generation,
                    resource.NativeRealizationCount,
                    resource.NativeRealizations,
                    resource.AllocationSite))
                .ToArray();
        }
    }

    internal void AdvanceGeneration() => generation++;

    internal IDisposable EnterRenderCallback()
    {
        renderCallbackDepth++;
        return new RenderCallbackScope(this);
    }

    private T Track<T>(T handle)
        where T : OverlayResourceHandle
    {
        if (RejectCreationDuringRender && renderCallbackDepth > 0)
        {
            throw new InvalidOperationException("Resource creation during the render callback is disabled. Create resources during setup or an explicit load phase.");
        }

        lock (gate)
        {
            handle.AllocationSite ??= CaptureAllocationSite();
            liveResources.Add(handle.Id, handle);
        }

        return handle;
    }

    private long GetNextResourceId()
    {
        lock (gate)
        {
            return nextResourceId++;
        }
    }

    private void Unregister(long id)
    {
        lock (gate)
        {
            liveResources.Remove(id);
        }
    }

    private static string CaptureAllocationSite()
        => new StackTrace(skipFrames: 2, fNeedFileInfo: false).ToString();

    private static void ValidateLinearGradientOptions(LinearGradientBrushOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Stops);
        if (options.Start == options.End)
        {
            throw new ArgumentException("Linear gradient start and end points must be different.", nameof(options));
        }

        if (options.Stops.Count < 2)
        {
            throw new ArgumentException("Linear gradients require at least two gradient stops.", nameof(options));
        }

        float previousPosition = 0f;
        for (int i = 0; i < options.Stops.Count; i++)
        {
            float position = options.Stops[i].Position;
            if (position is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Gradient stop positions must be between 0 and 1.");
            }

            if (i > 0 && position < previousPosition)
            {
                throw new ArgumentException("Gradient stop positions must be sorted in ascending order.", nameof(options));
            }

            previousPosition = position;
        }
    }

    private static void ValidateTextLayoutOptions(TextLayoutOptions options)
    {
        if (!float.IsFinite(options.MaxWidth) || options.MaxWidth <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Text layout maximum width must be finite and positive.");
        }

        if (!float.IsFinite(options.MaxHeight) || options.MaxHeight <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Text layout maximum height must be finite and positive.");
        }
    }

    private sealed class RenderCallbackScope : IDisposable
    {
        private OverlayResourceManager? owner;

        public RenderCallbackScope(OverlayResourceManager owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            if (owner is { } activeOwner)
            {
                activeOwner.renderCallbackDepth--;
                owner = null;
            }
        }
    }
}
