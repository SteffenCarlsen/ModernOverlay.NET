namespace ModernOverlay.Win32;

public sealed record Win32NativeFailureInfo(
    string Operation,
    int? Win32Error,
    int? HResult,
    DateTimeOffset TimestampUtc);

public static class Win32NativeDiagnostics
{
    private static readonly Lock Gate = new();
    private static Win32NativeFailureInfo? lastFailure;

    public static Win32NativeFailureInfo? LastFailure
    {
        get
        {
            lock (Gate)
            {
                return lastFailure;
            }
        }
    }

    internal static void RecordWin32Failure(string operation, int error)
        => Record(new Win32NativeFailureInfo(operation, error, null, DateTimeOffset.UtcNow));

    internal static void RecordHResultFailure(string operation, int hresult)
        => Record(new Win32NativeFailureInfo(operation, null, hresult, DateTimeOffset.UtcNow));

    private static void Record(Win32NativeFailureInfo failure)
    {
        lock (Gate)
        {
            lastFailure = failure;
        }
    }
}
