namespace ModernOverlay.Integration.Experimental;

public sealed class IsolatedOverlayIntegrationProvider : IOverlayIntegrationProvider
{
    private readonly IOverlayIntegrationProvider inner;

    public IsolatedOverlayIntegrationProvider(IOverlayIntegrationProvider inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public OverlayIntegrationProviderDescriptor Descriptor => inner.Descriptor;

    public async ValueTask<OverlayIntegrationProviderResult> InitializeAsync(
        IOverlayCommandTransport transport,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.InitializeAsync(transport, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return OverlayIntegrationProviderResult.Failed(ex.Message);
        }
    }

    public async ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Experimental providers are isolated from the host. Shutdown errors are intentionally swallowed.
        }
    }
}
