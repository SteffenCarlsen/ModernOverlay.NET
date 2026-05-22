using ModernOverlay.Diagnostics;
using ModernOverlay.Rendering;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.WIC;

namespace ModernOverlay.Direct2D;

internal sealed class Direct2DRenderBackend : IRenderBackend
{
    private readonly Direct2DDrawCommandSink commandSink = new();
    private RenderBackendInitializeContext? context;
    private IDWriteFactory? directWriteFactory;
    private ID2D1Factory? factory;
    private ID2D1HwndRenderTarget? renderTarget;
    private IWICImagingFactory? wicFactory;
    private bool disposed;
    private bool vSyncFallbackWarningEmitted;

    public RenderBackendKind Kind => RenderBackendKind.Direct2DHwnd;

    public RenderBackendGeneration Generation { get; private set; } = RenderBackendGeneration.Initial;

    public IDrawCommandSink CommandSink => commandSink;

    public IBackendResourceFactory Resources { get; } = new Direct2DResourceFactory();

    public PixelSize CurrentPixelSize { get; private set; }

    public DpiScale CurrentDpi { get; private set; } = DpiScale.Default;

    public RenderQualityOptions Quality { get; private set; } = RenderQualityOptions.Default;

    public PresentMode PresentMode { get; private set; } = PresentMode.BackendDefault;

    public PresentMode EffectivePresentMode { get; private set; } = PresentMode.BackendDefault;

    public bool IsInitialized => context is not null;

    public void Initialize(RenderBackendInitializeContext context)
    {
        ThrowIfDisposed();
        InitializeNativeResources(context, advanceGeneration: false);
    }

    public void Recreate(RenderBackendInitializeContext context)
    {
        ThrowIfDisposed();
        ReleaseNativeResources(clearContext: true);
        InitializeNativeResources(context, advanceGeneration: true);
    }

    private void InitializeNativeResources(RenderBackendInitializeContext context, bool advanceGeneration)
    {
        if (context.Hwnd.IsNull)
        {
            throw new ArgumentException("A Direct2D HWND backend requires a non-null HWND.", nameof(context));
        }

        if (context.PixelSize.IsEmpty)
        {
            throw new ArgumentException("A Direct2D HWND backend requires a positive pixel size.", nameof(context));
        }

        this.context = context;
        CurrentPixelSize = context.PixelSize;
        CurrentDpi = context.Dpi;
        Quality = context.Quality;
        SetPresentModeCore(context.PresentMode);

        directWriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
        wicFactory = new IWICImagingFactory();
        factory = D2D1.D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded, DebugLevel.None);
        renderTarget = factory.CreateHwndRenderTarget(CreateRenderTargetProperties(context.Dpi), CreateHwndRenderTargetProperties(context));
        if (advanceGeneration)
        {
            Generation = Generation.Next();
        }

        commandSink.BackendGeneration = Generation.Value;
        commandSink.SetRenderTarget(factory, renderTarget, directWriteFactory, wicFactory);
        ApplyQualityOptions(renderTarget, Quality);
    }

    public void Resize(PixelSize size, DpiScale dpi)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        if (size.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Pixel size must be positive.");
        }

        CurrentPixelSize = size;
        CurrentDpi = dpi;
        renderTarget!.Resize(ToSizeI(size));
        Generation = Generation.Next();
        commandSink.BackendGeneration = Generation.Value;
    }

    public BeginFrameResult BeginFrame(in FrameInfo frameInfo)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        commandSink.BeginFrame(EnsureRenderTarget());
        return BeginFrameResult.Ready;
    }

    public EndFrameResult EndFrame()
    {
        ThrowIfDisposed();
        EnsureInitialized();
        commandSink.EndFrame();
        EnsureRenderTarget().EndDraw(out _, out _);
        return new EndFrameResult(Presented: true);
    }

    public void Clear(ColorRgba color)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        commandSink.Clear(color);
    }

    public void SetQuality(RenderQualityOptions quality)
    {
        ThrowIfDisposed();
        Quality = quality;
        if (renderTarget is not null)
        {
            ApplyQualityOptions(renderTarget, quality);
        }
    }

    public void SetPresentMode(PresentMode presentMode)
    {
        ThrowIfDisposed();
        SetPresentModeCore(presentMode);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        ReleaseNativeResources(clearContext: true);
        disposed = true;
    }

    private void ReleaseNativeResources(bool clearContext)
    {
        commandSink.SetRenderTarget(null, null, null, null);
        renderTarget?.Dispose();
        factory?.Dispose();
        directWriteFactory?.Dispose();
        wicFactory?.Dispose();
        renderTarget = null;
        factory = null;
        directWriteFactory = null;
        wicFactory = null;
        if (clearContext)
        {
            context = null;
        }
    }

    private static RenderTargetProperties CreateRenderTargetProperties(DpiScale dpi)
    {
        const float DefaultDpi = 96f;
        return new RenderTargetProperties(
            RenderTargetType.Default,
            Vortice.DCommon.PixelFormat.Premultiplied,
            dpi.X * DefaultDpi,
            dpi.Y * DefaultDpi,
            RenderTargetUsage.None,
            FeatureLevel.Default);
    }

    private static HwndRenderTargetProperties CreateHwndRenderTargetProperties(RenderBackendInitializeContext context)
        => new()
        {
            Hwnd = context.Hwnd.Value,
            PixelSize = ToSizeI(context.PixelSize),
            PresentOptions = ToPresentOptions(GetEffectivePresentMode(context.PresentMode)),
        };

    private static PresentOptions ToPresentOptions(PresentMode presentMode)
        => presentMode == PresentMode.Immediate ? PresentOptions.Immediately : PresentOptions.None;

    private void SetPresentModeCore(PresentMode presentMode)
    {
        PresentMode = presentMode;
        EffectivePresentMode = GetEffectivePresentMode(presentMode);
        if (presentMode == PresentMode.VSync && !vSyncFallbackWarningEmitted)
        {
            vSyncFallbackWarningEmitted = true;
            OverlayEventSource.Log.BackendFallback(
                "Direct2D HWND",
                nameof(PresentMode),
                nameof(PresentMode.VSync),
                nameof(PresentMode.BackendDefault),
                "ID2D1HwndRenderTarget does not expose an explicit VSync present mode; using backend-default presentation.");
        }
    }

    private static PresentMode GetEffectivePresentMode(PresentMode presentMode)
        => presentMode switch
        {
            PresentMode.BackendDefault => PresentMode.BackendDefault,
            PresentMode.Immediate => PresentMode.Immediate,
            PresentMode.VSync => PresentMode.BackendDefault,
            _ => throw new ArgumentOutOfRangeException(nameof(presentMode), presentMode, "Unsupported present mode."),
        };

    private static SizeI ToSizeI(PixelSize size) => new(size.Width, size.Height);

    private static void ApplyQualityOptions(ID2D1RenderTarget target, RenderQualityOptions quality)
    {
        target.AntialiasMode = quality.AntialiasPrimitives ? AntialiasMode.PerPrimitive : AntialiasMode.Aliased;
        target.TextAntialiasMode = quality.AntialiasText
            ? Vortice.Direct2D1.TextAntialiasMode.Default
            : Vortice.Direct2D1.TextAntialiasMode.Aliased;
    }

    private ID2D1HwndRenderTarget EnsureRenderTarget()
    {
        EnsureInitialized();
        return renderTarget ?? throw new InvalidOperationException("The Direct2D render target has not been created.");
    }

    private void EnsureInitialized()
    {
        if (context is null)
        {
            throw new InvalidOperationException("The Direct2D backend has not been initialized.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
