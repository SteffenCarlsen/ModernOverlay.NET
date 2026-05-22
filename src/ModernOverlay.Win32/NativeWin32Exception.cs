using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ModernOverlay.Win32;

public sealed class NativeWin32Exception : Win32Exception
{
    public NativeWin32Exception(string operation)
        : this(operation, Marshal.GetLastPInvokeError())
    {
    }

    private NativeWin32Exception(string operation, int error)
        : base(error)
    {
        Operation = operation;
        Win32NativeDiagnostics.RecordWin32Failure(operation, error);
    }

    public string Operation { get; }

    public override string Message => $"{Operation} failed with Win32 error {NativeErrorCode}: {base.Message}";
}
