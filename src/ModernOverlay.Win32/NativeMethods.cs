using System.Runtime.InteropServices;

namespace ModernOverlay.Win32;

internal static class NativeMethods
{
    internal const uint Infinite = 0xFFFFFFFF;
    internal const int SwHide = 0;
    internal const int SwShowNoActivate = 4;
    internal const uint PmRemove = 0x0001;
    internal const uint QsAllInput = 0x04FF;
    internal const uint MwmoInputAvailable = 0x0004;
    internal const uint LwaAlpha = 0x00000002;
    internal const uint CoinitApartmentThreaded = 0x2;
    internal const int SOk = 0;
    internal const int SFalse = 1;
    internal const int RpcEChangedMode = unchecked((int)0x80010106);
    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpShowWindow = 0x0040;

    internal static readonly nint HwndTopMost = new(-1);
    internal static readonly nint HwndNoTopMost = new(-2);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WndProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Margins
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal sealed class WndClassEx
    {
        public int Size = Marshal.SizeOf<WndClassEx>();
        public uint Style;
        public WndProc? WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint BackgroundBrush;
        public string? MenuName;
        public string? ClassName;
        public nint SmallIcon;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern void CoUninitialize();

    [DllImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea", ExactSpelling = true)]
    internal static extern int DwmExtendFrameIntoClientArea(nint hwnd, in Margins margins);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClassEx([In] WndClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterClass(string className, nint instance);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint param);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", ExactSpelling = true)]
    internal static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW", ExactSpelling = true)]
    internal static extern nint DispatchMessage(in Msg message);

    [DllImport("user32.dll", EntryPoint = "PeekMessageW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessage(out Msg message, nint hwnd, uint messageFilterMin, uint messageFilterMax, uint removeMessage);

    [DllImport("user32.dll", EntryPoint = "ShowWindow", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int commandShow);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "TranslateMessage", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(in Msg message);

    [DllImport("user32.dll", EntryPoint = "MsgWaitForMultipleObjectsEx", ExactSpelling = true, SetLastError = true)]
    internal static extern uint MsgWaitForMultipleObjectsEx(
        uint count,
        nint[] handles,
        uint milliseconds,
        uint wakeMask,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", ExactSpelling = true, SetLastError = true)]
    internal static extern nint GetWindowLongPtr64(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", ExactSpelling = true, SetLastError = true)]
    internal static extern nint SetWindowLongPtr64(nint hwnd, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true, SetLastError = true)]
    internal static extern int GetWindowLong32(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", ExactSpelling = true, SetLastError = true)]
    internal static extern int SetWindowLong32(nint hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);

    internal static nint GetWindowLongPtr(nint hwnd, int index)
        => nint.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new nint(GetWindowLong32(hwnd, index));

    internal static nint SetWindowLongPtr(nint hwnd, int index, nint value)
        => nint.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new nint(SetWindowLong32(hwnd, index, value.ToInt32()));
}
