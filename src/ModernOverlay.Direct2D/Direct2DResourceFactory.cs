using ModernOverlay.Rendering;

namespace ModernOverlay.Direct2D;

internal sealed class Direct2DResourceFactory : IBackendResourceFactory
{
    public string BackendName => "Direct2D HWND";
}
