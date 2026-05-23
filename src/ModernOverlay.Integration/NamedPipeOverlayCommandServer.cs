using System.IO.Pipes;

namespace ModernOverlay.Integration;

public sealed class NamedPipeOverlayCommandServer
{
    private readonly string pipeName;
    private readonly Func<OverlayCommandMessage, CancellationToken, ValueTask<OverlayCommandResult>> handler;
    private readonly NamedPipeOverlayCommandSecurity security;
    private readonly int maxConcurrentConnections;

    public NamedPipeOverlayCommandServer(
        string pipeName,
        Func<OverlayCommandMessage, CancellationToken, ValueTask<OverlayCommandResult>> handler,
        NamedPipeOverlayCommandSecurity? security = null,
        int maxConcurrentConnections = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentConnections, 1);
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        this.security = security ?? NamedPipeOverlayCommandSecurity.None;
        this.maxConcurrentConnections = maxConcurrentConnections;
        this.pipeName = pipeName;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var connectionSlots = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
        var connections = new List<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await connectionSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                NamedPipeServerStream pipe = CreateServerStream();

                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    connectionSlots.Release();
                    throw;
                }

                connections.RemoveAll(static connection => connection.IsCompleted);
                connections.Add(ProcessConnectionAndReleaseSlotAsync(pipe, connectionSlots, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        try
        {
            await Task.WhenAll(connections).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        PipeSecurity? pipeSecurity = security.PipeSecurity;
        return pipeSecurity is null
            ? new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxConcurrentConnections,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous)
            : NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxConcurrentConnections,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity,
                HandleInheritability.None,
                additionalAccessRights: 0);
    }

    private async Task ProcessConnectionAndReleaseSlotAsync(
        NamedPipeServerStream pipe,
        SemaphoreSlim connectionSlots,
        CancellationToken cancellationToken)
    {
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                await ProcessConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            finally
            {
                connectionSlots.Release();
            }
        }
    }

    private async Task ProcessConnectionAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(pipe, leaveOpen: true);
        await using StreamWriter writer = new(pipe, leaveOpen: true)
        {
            AutoFlush = true,
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            string? request = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            OverlayCommandResult result;
            try
            {
                OverlayCommandMessage message = OverlayCommandProtocol.DeserializeMessage(request);
                result = security.IsAuthorized(message.CommandToken)
                    ? await handler(message with { CommandToken = null }, cancellationToken).ConfigureAwait(false)
                    : OverlayCommandResult.Rejected(message.Sequence, "The overlay command token was missing or invalid.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or System.Text.Json.JsonException)
            {
                result = OverlayCommandResult.Rejected(0, ex.Message);
            }

            await writer.WriteLineAsync(OverlayCommandProtocol.SerializeResult(result).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }
}
