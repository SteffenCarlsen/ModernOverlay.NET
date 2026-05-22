using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace ModernOverlay.Diagnostics;

public sealed class OverlayEventSourceLogger : EventListener
{
    private readonly ILogger logger;
    private readonly EventLevel level;
    private bool disposed;

    public OverlayEventSourceLogger(ILogger logger, EventLevel level = EventLevel.Informational)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.level = level;
        foreach (EventSource eventSource in EventSource.GetSources())
        {
            EnableIfModernOverlay(eventSource);
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
        => EnableIfModernOverlay(eventSource);

    private void EnableIfModernOverlay(EventSource eventSource)
    {
        if (!disposed && logger is not null && eventSource.Name == "ModernOverlay")
        {
            EnableEvents(eventSource, level);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (disposed || eventData.EventSource.Name != "ModernOverlay")
        {
            return;
        }

        LogLevel logLevel = ToLogLevel(eventData.Level);
        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        string message = FormatMessage(eventData);
        logger.Log(logLevel, new EventId(eventData.EventId, eventData.EventName), message, null, static (state, _) => state);
    }

    public override void Dispose()
    {
        disposed = true;
        base.Dispose();
    }

    private static LogLevel ToLogLevel(EventLevel level)
        => level switch
        {
            EventLevel.Critical => LogLevel.Critical,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Warning => LogLevel.Warning,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Verbose => LogLevel.Trace,
            EventLevel.LogAlways => LogLevel.Information,
            _ => LogLevel.Debug,
        };

    private static string FormatMessage(EventWrittenEventArgs eventData)
    {
        string eventName = eventData.EventName ?? $"Event{eventData.EventId}";
        if (eventData.PayloadNames is null || eventData.Payload is null || eventData.Payload.Count == 0)
        {
            return eventName;
        }

        string[] payload = new string[eventData.Payload.Count];
        for (int i = 0; i < payload.Length; i++)
        {
            string name = i < eventData.PayloadNames.Count ? eventData.PayloadNames[i] : $"Arg{i}";
            payload[i] = $"{name}={eventData.Payload[i]}";
        }

        return $"{eventName}: {string.Join(", ", payload)}";
    }
}
