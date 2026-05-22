using ModernOverlay.Rendering;

namespace ModernOverlay.Direct2D;

public static class Direct2DOverlayBackend
{
    public static void Register()
    {
        RenderBackendRegistry.Register(new Direct2DRenderBackendProvider());
    }

    internal static IDisposable RegisterForScope()
        => RenderBackendRegistry.RegisterForScope(new Direct2DRenderBackendProvider());

    private sealed class Direct2DRenderBackendProvider : IRenderBackendProvider
    {
        public IRenderBackend CreateBackend(OverlayWindowOptions options) => new Direct2DRenderBackend();
    }
}
