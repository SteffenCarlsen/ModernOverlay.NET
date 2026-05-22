namespace ModernOverlay.Win32;

public sealed class NativeHResultException : Exception
{
    public NativeHResultException(string operation, int hresult)
        : base($"{operation} failed with HRESULT 0x{hresult:X8}.")
    {
        Operation = operation;
        HResult = hresult;
        Win32NativeDiagnostics.RecordHResultFailure(operation, hresult);
    }

    public string Operation { get; }
}
