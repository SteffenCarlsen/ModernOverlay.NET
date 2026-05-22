namespace ModernOverlay.Rendering;

internal sealed class NullRenderBackend : IRenderBackend
{
    private bool disposed;
    private bool initialized;

    public RenderBackendKind Kind => RenderBackendKind.Null;

    public RenderBackendGeneration Generation { get; private set; } = RenderBackendGeneration.Initial;

    public IDrawCommandSink CommandSink { get; } = new NoOpDrawCommandSink();

    public IBackendResourceFactory Resources { get; } = new NullBackendResourceFactory();

    public PixelSize CurrentPixelSize { get; private set; }

    public DpiScale CurrentDpi { get; private set; } = DpiScale.Default;

    public void Initialize(RenderBackendInitializeContext context)
    {
        ThrowIfDisposed();
        initialized = true;
        CurrentPixelSize = context.PixelSize;
        CurrentDpi = context.Dpi;
    }

    public void Resize(PixelSize size, DpiScale dpi)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        CurrentPixelSize = size;
        CurrentDpi = dpi;
        Generation = Generation.Next();
    }

    public void Recreate(RenderBackendInitializeContext context)
    {
        ThrowIfDisposed();
        CurrentPixelSize = context.PixelSize;
        CurrentDpi = context.Dpi;
        initialized = true;
        Generation = Generation.Next();
    }

    public BeginFrameResult BeginFrame(in FrameInfo frameInfo)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        ((NoOpDrawCommandSink)CommandSink).ResetFrame();
        return BeginFrameResult.Ready;
    }

    public EndFrameResult EndFrame()
    {
        ThrowIfDisposed();
        EnsureInitialized();
        return new EndFrameResult(Presented: false);
    }

    public void Clear(ColorRgba color)
    {
    }

    public void SetQuality(RenderQualityOptions quality)
    {
    }

    public void SetPresentMode(PresentMode presentMode)
    {
    }

    public void Dispose()
    {
        disposed = true;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("The null backend has not been initialized.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class NullBackendResourceFactory : IBackendResourceFactory
    {
        public string BackendName => "Null";
    }
}
