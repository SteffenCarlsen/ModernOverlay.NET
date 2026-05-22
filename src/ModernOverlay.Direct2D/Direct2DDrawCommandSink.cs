using System.Numerics;
using ModernOverlay.Rendering;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.WIC;

namespace ModernOverlay.Direct2D;

internal sealed class Direct2DDrawCommandSink : IDrawCommandSink
{
    private readonly Dictionary<SolidBrushHandle, NativeResource<ID2D1SolidColorBrush>> solidBrushes = [];
    private readonly Dictionary<LinearGradientBrushHandle, NativeResource<ID2D1LinearGradientBrush>> linearGradientBrushes = [];
    private readonly Dictionary<ImageFrameKey, NativeResource<ID2D1Bitmap>> bitmaps = [];
    private readonly Dictionary<StrokeStyleHandle, NativeResource<ID2D1StrokeStyle>> strokeStyles = [];
    private readonly Dictionary<FontHandle, NativeResource<IDWriteTextFormat>> textFormats = [];
    private readonly Dictionary<TextLayoutHandle, NativeResource<IDWriteTextLayout>> textLayouts = [];
    private readonly Dictionary<GeometryPath, NativeResource<ID2D1PathGeometry>> strokedGeometries = [];
    private readonly Dictionary<GeometryPath, NativeResource<ID2D1PathGeometry>> filledGeometries = [];
    private readonly Stack<Matrix3x2> transformStack = new();
    private IDWriteFactory? directWriteFactory;
    private ID2D1Factory? factory;
    private ID2D1RenderTarget? renderTarget;
    private IWICImagingFactory? wicFactory;
    private int clipDepth;

    public int CommandCount { get; private set; }

    public int PrimitiveCount { get; private set; }

    public int TransientTextLayoutCount { get; private set; }

    public int NativeResourceCount
        => solidBrushes.Count
        + linearGradientBrushes.Count
        + bitmaps.Count
        + strokeStyles.Count
        + textFormats.Count
        + textLayouts.Count
        + strokedGeometries.Count
        + filledGeometries.Count;

    public ColorRgba? LastClearColor { get; private set; }

    public bool IsInsideFrame { get; private set; }

    public long BackendGeneration { get; set; } = 1;

    public void SetRenderTarget(ID2D1Factory? factory, ID2D1RenderTarget? target, IDWriteFactory? directWriteFactory, IWICImagingFactory? wicFactory)
    {
        DisposeCachedBrushes();
        DisposeCachedLinearGradientBrushes();
        DisposeCachedBitmaps();
        DisposeCachedStrokeStyles();
        DisposeCachedGeometries();
        if (!ReferenceEquals(this.directWriteFactory, directWriteFactory))
        {
            DisposeCachedTextLayouts();
            DisposeCachedTextFormats();
        }

        this.factory = factory;
        this.directWriteFactory = directWriteFactory;
        renderTarget = target;
        this.wicFactory = wicFactory;
        transformStack.Clear();
        clipDepth = 0;
    }

    public void BeginFrame(ID2D1RenderTarget target)
    {
        renderTarget = target;
        CommandCount = 0;
        PrimitiveCount = 0;
        TransientTextLayoutCount = 0;
        LastClearColor = null;
        IsInsideFrame = true;
        transformStack.Clear();
        clipDepth = 0;
        target.Transform = Matrix3x2.Identity;
        target.BeginDraw();
    }

    public void EndFrame()
    {
        IsInsideFrame = false;
    }

    public void Clear(ColorRgba color)
    {
        LastClearColor = color;
        CommandCount++;
        renderTarget?.Clear(ToColor4(color));
    }

    public void PushClip(RectF clip)
    {
        renderTarget?.PushAxisAlignedClip(ToRawRectF(clip), renderTarget.AntialiasMode);

        clipDepth++;
    }

    public void PopClip()
    {
        if (clipDepth <= 0)
        {
            throw new InvalidOperationException("Cannot pop a Direct2D clip because the clip stack is empty.");
        }

        renderTarget?.PopAxisAlignedClip();

        clipDepth--;
    }

    public void PushTransform(Matrix3x2F transform)
    {
        if (renderTarget is null)
        {
            transformStack.Push(Matrix3x2.Identity);
            return;
        }

        Matrix3x2 previousTransform = renderTarget.Transform;
        transformStack.Push(previousTransform);
        renderTarget.Transform = transform.ToSystemMatrix() * previousTransform;
    }

    public void PopTransform()
    {
        if (!transformStack.TryPop(out Matrix3x2 previousTransform))
        {
            throw new InvalidOperationException("Cannot pop a Direct2D transform because the transform stack is empty.");
        }

        ID2D1RenderTarget? target = renderTarget;
        if (target is null)
        {
            return;
        }

        target.Transform = previousTransform;
    }

    public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            Vector2 startVector = ToVector2(start);
            Vector2 endVector = ToVector2(end);
            renderTarget!.DrawLine(startVector, endVector, nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            RawRectF nativeRect = ToRawRectF(rect);
            renderTarget!.DrawRectangle(nativeRect, nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void FillRectangle(RectF rect, BrushHandle brush)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        if (nativeBrush is not null)
        {
            renderTarget!.FillRectangle(ToRawRectF(rect), nativeBrush);
        }
    }

    public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            RoundedRectangle roundedRectangle = ToRoundedRectangle(rect, radiusX, radiusY);
            renderTarget!.DrawRoundedRectangle(ref roundedRectangle, nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        if (nativeBrush is not null)
        {
            RoundedRectangle roundedRectangle = ToRoundedRectangle(rect, radiusX, radiusY);
            renderTarget!.FillRoundedRectangle(ref roundedRectangle, nativeBrush);
        }
    }

    public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        DrawEllipse(new RectF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), brush, strokeWidth, strokeStyle);
    }

    public void FillCircle(PointF center, float radius, BrushHandle brush)
    {
        FillEllipse(new RectF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), brush);
    }

    public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            renderTarget!.DrawEllipse(ToEllipse(bounds), nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void FillEllipse(RectF bounds, BrushHandle brush)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        if (nativeBrush is not null)
        {
            renderTarget!.FillEllipse(ToEllipse(bounds), nativeBrush);
        }
    }

    public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            using ID2D1PathGeometry geometry = CreateTriangleGeometry(a, b, c);
            renderTarget!.DrawGeometry(geometry, nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        if (nativeBrush is not null)
        {
            using ID2D1PathGeometry geometry = CreateTriangleGeometry(a, b, c);
            renderTarget!.FillGeometry(geometry, nativeBrush, null);
        }
    }

    public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        ID2D1StrokeStyle? nativeStrokeStyle = GetStrokeStyle(strokeStyle);
        if (nativeBrush is not null)
        {
            ID2D1PathGeometry nativeGeometry = GetPathGeometry(geometry, FigureBegin.Hollow);
            renderTarget!.DrawGeometry(nativeGeometry, nativeBrush, strokeWidth, nativeStrokeStyle);
        }
    }

    public void FillGeometry(GeometryPath geometry, BrushHandle brush)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        if (nativeBrush is not null)
        {
            ID2D1PathGeometry nativeGeometry = GetPathGeometry(geometry, FigureBegin.Filled);
            renderTarget!.FillGeometry(nativeGeometry, nativeBrush, null);
        }
    }

    public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
    {
        Count();
        ID2D1Bitmap? bitmap = GetBitmap(image, frameIndex);
        if (bitmap is not null)
        {
            RawRectF? sourceRect = source is { } value ? ToRawRectF(value) : null;
            renderTarget!.DrawBitmap(bitmap, ToRawRectF(destination), opacity, ToBitmapInterpolationMode(interpolationMode), sourceRect);
        }
    }

    public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
    {
        Count();
        TransientTextLayoutCount++;
        ID2D1Brush? nativeBrush = GetBrush(brush);
        IDWriteTextFormat? nativeFormat = GetTextFormat(font);
        if (nativeBrush is not null && nativeFormat is not null)
        {
            var layoutRect = new Rect(origin.X, origin.Y, 4096f, 4096f);
            renderTarget!.DrawText(text, nativeFormat, layoutRect, nativeBrush, DrawTextOptions.None, MeasuringMode.Natural);
        }
    }

    public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
    {
        Count();
        ID2D1Brush? nativeBrush = GetBrush(brush);
        IDWriteTextLayout? nativeLayout = GetTextLayout(layout);
        if (nativeBrush is not null && nativeLayout is not null)
        {
            renderTarget!.DrawTextLayout(ToVector2(origin), nativeLayout, nativeBrush, DrawTextOptions.None);
        }
    }

    public SizeF MeasureText(string text, FontHandle font)
    {
        IDWriteTextFormat? nativeFormat = GetTextFormat(font);
        if (nativeFormat is null || directWriteFactory is null)
        {
            return new SizeF(text.Length * font.Options.Size * 0.5f, font.Options.Size);
        }

        using IDWriteTextLayout layout = directWriteFactory.CreateTextLayout(text, nativeFormat, 4096f, 4096f);
        TransientTextLayoutCount++;
        TextMetrics metrics = layout.Metrics;
        return new SizeF(metrics.WidthIncludingTrailingWhitespace, metrics.Height);
    }

    public SizeF MeasureTextLayout(TextLayoutHandle layout)
    {
        IDWriteTextLayout? nativeLayout = GetTextLayout(layout);
        if (nativeLayout is null)
        {
            return new SizeF(layout.Text.Length * layout.Font.Options.Size * 0.5f, layout.Font.Options.Size);
        }

        TextMetrics metrics = nativeLayout.Metrics;
        return new SizeF(metrics.WidthIncludingTrailingWhitespace, metrics.Height);
    }

    private void Count()
    {
        CommandCount++;
        PrimitiveCount++;
    }

    private ID2D1Brush? GetBrush(BrushHandle brush)
    {
        ObjectDisposedException.ThrowIf(brush.IsDisposed, brush);
        return renderTarget is not null
            ? brush switch
            {
                SolidBrushHandle solidBrush => GetSolidBrush(solidBrush),
                LinearGradientBrushHandle linearGradientBrush => GetLinearGradientBrush(linearGradientBrush),
                _ => throw new ArgumentOutOfRangeException(nameof(brush), "Unsupported brush kind."),
            }
            : null;
    }

    private ID2D1SolidColorBrush GetSolidBrush(SolidBrushHandle brush)
    {
        if (!solidBrushes.TryGetValue(brush, out NativeResource<ID2D1SolidColorBrush>? nativeBrush))
        {
            nativeBrush = new NativeResource<ID2D1SolidColorBrush>(
                renderTarget!.CreateSolidColorBrush(ToColor4(brush.Color)),
                brush.RegisterNativeRealization("Direct2D", BackendGeneration, "SolidColorBrush"));
            solidBrushes.Add(brush, nativeBrush);
        }

        return nativeBrush.Resource;
    }

    private ID2D1LinearGradientBrush GetLinearGradientBrush(LinearGradientBrushHandle brush)
    {
        if (!linearGradientBrushes.TryGetValue(brush, out NativeResource<ID2D1LinearGradientBrush>? nativeBrush))
        {
            LinearGradientBrushOptions options = brush.Options;
            Vortice.Direct2D1.GradientStop[] stops = options.Stops
                .Select(stop => new Vortice.Direct2D1.GradientStop
                {
                    Position = stop.Position,
                    Color = ToColor4(stop.Color),
                })
                .ToArray();
            using ID2D1GradientStopCollection stopCollection = renderTarget!.CreateGradientStopCollection(stops, Gamma.StandardRgb, ExtendMode.Clamp);
            var properties = new LinearGradientBrushProperties(ToVector2(options.Start), ToVector2(options.End));
            nativeBrush = new NativeResource<ID2D1LinearGradientBrush>(
                renderTarget.CreateLinearGradientBrush(properties, new BrushProperties(1f), stopCollection),
                brush.RegisterNativeRealization("Direct2D", BackendGeneration, "LinearGradientBrush"));
            linearGradientBrushes.Add(brush, nativeBrush);
        }

        return nativeBrush.Resource;
    }

    private IDWriteTextFormat? GetTextFormat(FontHandle font)
    {
        ObjectDisposedException.ThrowIf(font.IsDisposed, font);
        if (directWriteFactory is null)
        {
            return null;
        }

        if (!textFormats.TryGetValue(font, out NativeResource<IDWriteTextFormat>? nativeFormat))
        {
            nativeFormat = new NativeResource<IDWriteTextFormat>(
                CreateTextFormat(font),
                font.RegisterNativeRealization("DirectWrite", BackendGeneration, "TextFormat"));
            textFormats.Add(font, nativeFormat);
        }

        return nativeFormat.Resource;
    }

    private IDWriteTextLayout? GetTextLayout(TextLayoutHandle layout)
    {
        ObjectDisposedException.ThrowIf(layout.IsDisposed, layout);
        ObjectDisposedException.ThrowIf(layout.Font.IsDisposed, layout.Font);
        if (directWriteFactory is null)
        {
            return null;
        }

        if (!textLayouts.TryGetValue(layout, out NativeResource<IDWriteTextLayout>? nativeLayout))
        {
            if (directWriteFactory is null)
            {
                return null;
            }

            using IDWriteTextFormat nativeFormat = CreateTextFormat(layout.Font);
            ApplyTextLayoutOptions(nativeFormat, layout.Options);
            nativeLayout = new NativeResource<IDWriteTextLayout>(
                directWriteFactory.CreateTextLayout(layout.Text, nativeFormat, layout.Options.MaxWidth, layout.Options.MaxHeight),
                layout.RegisterNativeRealization("DirectWrite", BackendGeneration, "TextLayout"));
            textLayouts.Add(layout, nativeLayout);
        }

        return nativeLayout.Resource;
    }

    private ID2D1PathGeometry GetPathGeometry(GeometryPath path, FigureBegin figureBegin)
    {
        ObjectDisposedException.ThrowIf(path.IsDisposed, path);
        Dictionary<GeometryPath, NativeResource<ID2D1PathGeometry>> cache = figureBegin == FigureBegin.Filled
            ? filledGeometries
            : strokedGeometries;
        if (!cache.TryGetValue(path, out NativeResource<ID2D1PathGeometry>? nativeGeometry))
        {
            string kind = figureBegin == FigureBegin.Filled ? "FilledPathGeometry" : "StrokedPathGeometry";
            nativeGeometry = new NativeResource<ID2D1PathGeometry>(
                CreatePathGeometry(path, figureBegin),
                path.RegisterNativeRealization("Direct2D", BackendGeneration, kind));
            cache.Add(path, nativeGeometry);
        }

        return nativeGeometry.Resource;
    }

    private IDWriteTextFormat CreateTextFormat(FontHandle font)
    {
        IDWriteFactory nativeFactory = directWriteFactory ?? throw new InvalidOperationException("The DirectWrite factory has not been created.");
        return nativeFactory.CreateTextFormat(
            font.Options.FamilyName,
            null,
            FontWeight.Normal,
            FontStyle.Normal,
            FontStretch.Normal,
            font.Options.Size,
            string.Empty);
    }

    private static void ApplyTextLayoutOptions(IDWriteTextFormat format, TextLayoutOptions options)
    {
        format.WordWrapping = ToWordWrapping(options.Wrapping);
        format.TextAlignment = ToTextAlignment(options.HorizontalAlignment);
        format.ParagraphAlignment = ToParagraphAlignment(options.VerticalAlignment);
        format.SetTrimming(new Trimming
        {
            Granularity = ToTrimmingGranularity(options.Trimming),
            Delimiter = 0,
            DelimiterCount = 0,
        }, null);
    }

    private ID2D1StrokeStyle? GetStrokeStyle(StrokeStyleHandle? strokeStyle)
    {
        if (strokeStyle is null)
        {
            return null;
        }

        ObjectDisposedException.ThrowIf(strokeStyle.IsDisposed, strokeStyle);
        if (factory is null)
        {
            return null;
        }

        if (!strokeStyles.TryGetValue(strokeStyle, out NativeResource<ID2D1StrokeStyle>? nativeStrokeStyle))
        {
            float[] customDashes = strokeStyle.Options.CustomDashes.ToArray();
            StrokeStyleProperties properties = CreateStrokeStyleProperties(strokeStyle.Options);
            nativeStrokeStyle = new NativeResource<ID2D1StrokeStyle>(
                factory.CreateStrokeStyle(properties, customDashes),
                strokeStyle.RegisterNativeRealization("Direct2D", BackendGeneration, "StrokeStyle"));
            strokeStyles.Add(strokeStyle, nativeStrokeStyle);
        }

        return nativeStrokeStyle.Resource;
    }

    private ID2D1Bitmap? GetBitmap(ImageHandle image, int frameIndex)
    {
        ObjectDisposedException.ThrowIf(image.IsDisposed, image);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        if (renderTarget is null || wicFactory is null)
        {
            return null;
        }

        var key = new ImageFrameKey(image, frameIndex);
        if (!bitmaps.TryGetValue(key, out NativeResource<ID2D1Bitmap>? bitmap))
        {
            ID2D1Bitmap nativeBitmap = image.SourceKind switch
            {
                ImageSourceKind.Path => CreateBitmapFromFile(image.Path!, frameIndex),
                ImageSourceKind.EncodedBytes => CreateBitmapFromEncodedBytes(image.EncodedBytes!, frameIndex),
                _ => throw new ArgumentOutOfRangeException(nameof(image), "Unsupported image source kind."),
            };

            bitmap = new NativeResource<ID2D1Bitmap>(
                nativeBitmap,
                image.RegisterNativeRealization("Direct2D", BackendGeneration, $"BitmapFrame{frameIndex}"));
            bitmaps.Add(key, bitmap);
        }

        return bitmap.Resource;
    }

    private ID2D1Bitmap CreateBitmapFromFile(string path, int frameIndex)
    {
        return CreateBitmapFromEncodedBytes(File.ReadAllBytes(path), frameIndex);
    }

    private ID2D1Bitmap CreateBitmapFromEncodedBytes(byte[] encodedBytes, int frameIndex)
    {
        IWICImagingFactory nativeWicFactory = wicFactory ?? throw new InvalidOperationException("The WIC factory has not been created.");
        using var stream = new MemoryStream(encodedBytes, writable: false);
        using IWICBitmapDecoder decoder = nativeWicFactory.CreateDecoderFromStream(stream, DecodeOptions.CacheOnLoad);
        return CreateBitmapFromDecoder(decoder, frameIndex);
    }

    private ID2D1Bitmap CreateBitmapFromDecoder(IWICBitmapDecoder decoder, int frameIndex)
    {
        ID2D1RenderTarget target = renderTarget ?? throw new InvalidOperationException("The Direct2D render target has not been created.");
        IWICImagingFactory nativeWicFactory = wicFactory ?? throw new InvalidOperationException("The WIC factory has not been created.");
        using IWICBitmapFrameDecode frame = decoder.GetFrame((uint)frameIndex);
        using IWICFormatConverter converter = nativeWicFactory.CreateFormatConverter();
        converter.Initialize(frame, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapDitherType.None, null, 0d, BitmapPaletteType.Custom);
        return target.CreateBitmapFromWicBitmap(converter, null);
    }

    private ID2D1PathGeometry CreateTriangleGeometry(PointF a, PointF b, PointF c)
    {
        ID2D1Factory nativeFactory = factory ?? throw new InvalidOperationException("The Direct2D factory has not been created.");
        ID2D1PathGeometry geometry = nativeFactory.CreatePathGeometry();
        using ID2D1GeometrySink sink = geometry.Open();
        sink.BeginFigure(ToVector2(a), FigureBegin.Filled);
        sink.AddLine(ToVector2(b));
        sink.AddLine(ToVector2(c));
        sink.EndFigure(FigureEnd.Closed);
        sink.Close();
        return geometry;
    }

    private ID2D1PathGeometry CreatePathGeometry(GeometryPath path, FigureBegin figureBegin)
    {
        ID2D1Factory nativeFactory = factory ?? throw new InvalidOperationException("The Direct2D factory has not been created.");
        ID2D1PathGeometry geometry = nativeFactory.CreatePathGeometry();
        using ID2D1GeometrySink sink = geometry.Open();
        bool openFigure = false;
        foreach (GeometryPathCommand command in path.Commands)
        {
            switch (command.Kind)
            {
                case GeometryPathCommandKind.MoveTo:
                    if (openFigure)
                    {
                        sink.EndFigure(FigureEnd.Open);
                    }

                    sink.BeginFigure(ToVector2(command.Point), figureBegin);
                    openFigure = true;
                    break;
                case GeometryPathCommandKind.LineTo:
                    sink.AddLine(ToVector2(command.Point));
                    break;
                case GeometryPathCommandKind.CubicBezierTo:
                    sink.AddBezier(new BezierSegment
                    {
                        Point1 = ToVector2(command.ControlPoint1),
                        Point2 = ToVector2(command.ControlPoint2),
                        Point3 = ToVector2(command.Point),
                    });
                    break;
                case GeometryPathCommandKind.QuadraticBezierTo:
                    sink.AddQuadraticBezier(new QuadraticBezierSegment
                    {
                        Point1 = ToVector2(command.ControlPoint1),
                        Point2 = ToVector2(command.Point),
                    });
                    break;
                case GeometryPathCommandKind.ArcTo:
                    sink.AddArc(new ArcSegment(
                        ToVector2(command.Point),
                        new Vortice.Mathematics.Size(command.Size.Width, command.Size.Height),
                        command.RotationAngle,
                        ToSweepDirection(command.SweepDirection),
                        ToArcSize(command.ArcSize)));
                    break;
                case GeometryPathCommandKind.Close:
                    sink.EndFigure(FigureEnd.Closed);
                    openFigure = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), "Unsupported geometry path command.");
            }
        }

        if (openFigure)
        {
            sink.EndFigure(FigureEnd.Open);
        }

        sink.Close();
        return geometry;
    }

    private void DisposeCachedBrushes()
    {
        foreach (NativeResource<ID2D1SolidColorBrush> brush in solidBrushes.Values)
        {
            brush.Dispose();
        }

        solidBrushes.Clear();
    }

    private void DisposeCachedLinearGradientBrushes()
    {
        foreach (NativeResource<ID2D1LinearGradientBrush> brush in linearGradientBrushes.Values)
        {
            brush.Dispose();
        }

        linearGradientBrushes.Clear();
    }

    private void DisposeCachedBitmaps()
    {
        foreach (NativeResource<ID2D1Bitmap> bitmap in bitmaps.Values)
        {
            bitmap.Dispose();
        }

        bitmaps.Clear();
    }

    private void DisposeCachedStrokeStyles()
    {
        foreach (NativeResource<ID2D1StrokeStyle> strokeStyle in strokeStyles.Values)
        {
            strokeStyle.Dispose();
        }

        strokeStyles.Clear();
    }

    private void DisposeCachedTextFormats()
    {
        foreach (NativeResource<IDWriteTextFormat> textFormat in textFormats.Values)
        {
            textFormat.Dispose();
        }

        textFormats.Clear();
    }

    private void DisposeCachedTextLayouts()
    {
        foreach (NativeResource<IDWriteTextLayout> textLayout in textLayouts.Values)
        {
            textLayout.Dispose();
        }

        textLayouts.Clear();
    }

    private void DisposeCachedGeometries()
    {
        foreach (NativeResource<ID2D1PathGeometry> geometry in strokedGeometries.Values)
        {
            geometry.Dispose();
        }

        foreach (NativeResource<ID2D1PathGeometry> geometry in filledGeometries.Values)
        {
            geometry.Dispose();
        }

        strokedGeometries.Clear();
        filledGeometries.Clear();
    }

    private static Vector2 ToVector2(PointF point) => new(point.X, point.Y);

    private static RawRectF ToRawRectF(RectF rect)
        => new System.Drawing.RectangleF(rect.X, rect.Y, rect.Width, rect.Height);

    private static RoundedRectangle ToRoundedRectangle(RectF rect, float radiusX, float radiusY)
        => new(new System.Drawing.RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radiusX, radiusY);

    private static Ellipse ToEllipse(RectF bounds)
        => new(new Vector2(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f)), bounds.Width / 2f, bounds.Height / 2f);

    private static Color4 ToColor4(ColorRgba color) => new(color.R, color.G, color.B, color.A);

    private static StrokeStyleProperties CreateStrokeStyleProperties(StrokeStyleOptions options)
        => new()
        {
            StartCap = CapStyle.Flat,
            EndCap = CapStyle.Flat,
            DashCap = CapStyle.Flat,
            LineJoin = LineJoin.Miter,
            MiterLimit = 10f,
            DashStyle = ToDashStyle(options.DashStyle),
            DashOffset = options.DashOffset,
        };

    private static DashStyle ToDashStyle(StrokeDashStyle dashStyle)
        => dashStyle switch
        {
            StrokeDashStyle.Solid => DashStyle.Solid,
            StrokeDashStyle.Dash => DashStyle.Dash,
            StrokeDashStyle.Dot => DashStyle.Dot,
            StrokeDashStyle.DashDot => DashStyle.DashDot,
            StrokeDashStyle.DashDotDot => DashStyle.DashDotDot,
            StrokeDashStyle.Custom => DashStyle.Custom,
            _ => throw new ArgumentOutOfRangeException(nameof(dashStyle), dashStyle, "Unsupported stroke dash style."),
        };

    private static SweepDirection ToSweepDirection(GeometrySweepDirection sweepDirection)
        => sweepDirection switch
        {
            GeometrySweepDirection.CounterClockwise => SweepDirection.CounterClockwise,
            GeometrySweepDirection.Clockwise => SweepDirection.Clockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(sweepDirection), sweepDirection, "Unsupported geometry sweep direction."),
        };

    private static Vortice.Direct2D1.ArcSize ToArcSize(GeometryArcSize arcSize)
        => arcSize switch
        {
            GeometryArcSize.Small => Vortice.Direct2D1.ArcSize.Small,
            GeometryArcSize.Large => Vortice.Direct2D1.ArcSize.Large,
            _ => throw new ArgumentOutOfRangeException(nameof(arcSize), arcSize, "Unsupported geometry arc size."),
        };

    private static WordWrapping ToWordWrapping(TextWrapping wrapping)
        => wrapping switch
        {
            TextWrapping.Wrap => WordWrapping.Wrap,
            TextWrapping.NoWrap => WordWrapping.NoWrap,
            TextWrapping.EmergencyBreak => WordWrapping.EmergencyBreak,
            TextWrapping.WholeWord => WordWrapping.WholeWord,
            TextWrapping.Character => WordWrapping.Character,
            _ => throw new ArgumentOutOfRangeException(nameof(wrapping), wrapping, "Unsupported text wrapping mode."),
        };

    private static TextAlignment ToTextAlignment(TextHorizontalAlignment alignment)
        => alignment switch
        {
            TextHorizontalAlignment.Leading => TextAlignment.Leading,
            TextHorizontalAlignment.Center => TextAlignment.Center,
            TextHorizontalAlignment.Trailing => TextAlignment.Trailing,
            TextHorizontalAlignment.Justified => TextAlignment.Justified,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unsupported text horizontal alignment."),
        };

    private static ParagraphAlignment ToParagraphAlignment(TextVerticalAlignment alignment)
        => alignment switch
        {
            TextVerticalAlignment.Near => ParagraphAlignment.Near,
            TextVerticalAlignment.Center => ParagraphAlignment.Center,
            TextVerticalAlignment.Far => ParagraphAlignment.Far,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unsupported text vertical alignment."),
        };

    private static TrimmingGranularity ToTrimmingGranularity(TextTrimming trimming)
        => trimming switch
        {
            TextTrimming.None => TrimmingGranularity.None,
            TextTrimming.Character => TrimmingGranularity.Character,
            TextTrimming.Word => TrimmingGranularity.Word,
            _ => throw new ArgumentOutOfRangeException(nameof(trimming), trimming, "Unsupported text trimming mode."),
        };

    private static Vortice.Direct2D1.BitmapInterpolationMode ToBitmapInterpolationMode(ImageInterpolationMode interpolationMode)
        => interpolationMode switch
        {
            ImageInterpolationMode.NearestNeighbor => Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor,
            ImageInterpolationMode.Linear => Vortice.Direct2D1.BitmapInterpolationMode.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(interpolationMode), interpolationMode, "Unsupported image interpolation mode."),
        };

    private sealed class NativeResource<T> : IDisposable
        where T : IDisposable
    {
        private readonly IDisposable registration;
        private bool disposed;

        public NativeResource(T resource, IDisposable registration)
        {
            Resource = resource;
            this.registration = registration;
        }

        public T Resource { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Resource.Dispose();
            registration.Dispose();
        }
    }

    private readonly record struct ImageFrameKey(ImageHandle Image, int FrameIndex);
}
