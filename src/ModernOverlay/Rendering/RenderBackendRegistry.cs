namespace ModernOverlay.Rendering;

internal interface IRenderBackendProvider
{
    IRenderBackend CreateBackend(OverlayWindowOptions options);
}

internal static class RenderBackendRegistry
{
    private static readonly System.Threading.Lock Gate = new();
    private static IRenderBackendProvider provider = new NullRenderBackendProvider();

    public static IRenderBackend CreateBackend(OverlayWindowOptions options)
    {
        lock (Gate)
        {
            return provider.CreateBackend(options);
        }
    }

    public static void Register(IRenderBackendProvider renderBackendProvider)
    {
        ArgumentNullException.ThrowIfNull(renderBackendProvider);
        lock (Gate)
        {
            provider = renderBackendProvider;
        }
    }

    internal static IDisposable RegisterForScope(IRenderBackendProvider renderBackendProvider)
    {
        ArgumentNullException.ThrowIfNull(renderBackendProvider);
        lock (Gate)
        {
            IRenderBackendProvider previous = provider;
            provider = renderBackendProvider;
            return new RestoreScope(previous);
        }
    }

    private sealed class NullRenderBackendProvider : IRenderBackendProvider
    {
        public IRenderBackend CreateBackend(OverlayWindowOptions options) => new NullRenderBackend();
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IRenderBackendProvider previous;
        private bool disposed;

        public RestoreScope(IRenderBackendProvider previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lock (Gate)
            {
                provider = previous;
            }
        }
    }
}
