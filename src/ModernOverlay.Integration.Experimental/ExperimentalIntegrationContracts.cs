namespace ModernOverlay.Integration.Experimental;

public sealed record OverlayIntegrationProviderDescriptor(
    string Name,
    string? Description = null,
    string? Version = null);

public sealed record OverlayIntegrationProviderResult(bool Success, string? Error = null)
{
    public static OverlayIntegrationProviderResult Ok() => new(Success: true);

    public static OverlayIntegrationProviderResult Failed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new OverlayIntegrationProviderResult(Success: false, Error: error);
    }
}

public interface IOverlayCommandTransport
{
    ValueTask<OverlayCommandResult> SendAsync(OverlayCommandMessage message, CancellationToken cancellationToken = default);
}

public interface IRenderBridge
{
    ValueTask<IReadOnlyList<OverlayDrawCommand>> GetDrawCommandsAsync(CancellationToken cancellationToken = default);
}

public interface IOverlayIntegrationProvider
{
    OverlayIntegrationProviderDescriptor Descriptor { get; }

    ValueTask<OverlayIntegrationProviderResult> InitializeAsync(
        IOverlayCommandTransport transport,
        CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}
