namespace ModernOverlay.Integration.Experimental;

public sealed class NamedPipeOverlayCommandTransport : IOverlayCommandTransport
{
    private readonly NamedPipeOverlayCommandClient client;

    public NamedPipeOverlayCommandTransport(string pipeName, string serverName = ".")
    {
        client = new NamedPipeOverlayCommandClient(pipeName, serverName);
    }

    public ValueTask<OverlayCommandResult> SendAsync(OverlayCommandMessage message, CancellationToken cancellationToken = default)
        => client.SendAsync(message, cancellationToken);
}
