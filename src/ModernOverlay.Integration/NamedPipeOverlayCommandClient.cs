using System.IO.Pipes;

namespace ModernOverlay.Integration;

public sealed class NamedPipeOverlayCommandClient
{
    private readonly string pipeName;
    private readonly string serverName;
    private readonly string? commandToken;

    public NamedPipeOverlayCommandClient(string pipeName, string serverName = ".", string? commandToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        this.pipeName = pipeName;
        this.serverName = serverName;
        this.commandToken = commandToken;
    }

    public async ValueTask<OverlayCommandResult> SendAsync(OverlayCommandMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using NamedPipeClientStream pipe = new(
            serverName,
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);

        using StreamReader reader = new(pipe, leaveOpen: true);
        await using StreamWriter writer = new(pipe, leaveOpen: true)
        {
            AutoFlush = true,
        };

        OverlayCommandMessage outgoing = commandToken is null
            ? message
            : message with { CommandToken = commandToken };
        await writer.WriteLineAsync(OverlayCommandProtocol.SerializeMessage(outgoing).AsMemory(), cancellationToken).ConfigureAwait(false);
        string? response = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return response is not null
            ? OverlayCommandProtocol.DeserializeResult(response)
            : OverlayCommandResult.Rejected(message.Sequence, "The overlay command server closed the pipe before returning a result.");
    }
}
