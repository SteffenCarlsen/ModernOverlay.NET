using ModernOverlay.Win32;

namespace ModernOverlay;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public readonly record struct KeyGesture(int VirtualKey, HotkeyModifiers Modifiers)
{
    public static KeyGesture CtrlAltO => FromKey('O', HotkeyModifiers.Control | HotkeyModifiers.Alt);

    public static KeyGesture FromKey(char key, HotkeyModifiers modifiers)
    {
        char normalized = char.ToUpperInvariant(key);
        return normalized is >= 'A' and <= 'Z'
            ? new KeyGesture(normalized, modifiers)
            : throw new ArgumentOutOfRangeException(nameof(key), "Only A-Z key gestures are supported by this helper.");
    }

    public static KeyGesture FunctionKey(int number, HotkeyModifiers modifiers)
    {
        return number is >= 1 and <= 24
            ? new KeyGesture(0x70 + number - 1, modifiers)
            : throw new ArgumentOutOfRangeException(nameof(number), "Function key gestures must be between F1 and F24.");
    }
}

public sealed class OverlayHotkeyManager : IDisposable
{
    private readonly Win32OverlayWindow nativeWindow;
    private readonly Lock gate = new();
    private readonly Dictionary<int, HotkeyRegistration> registrations = [];
    private bool disposed;
    private int nextId = 1;

    internal OverlayHotkeyManager(Win32OverlayWindow nativeWindow)
    {
        this.nativeWindow = nativeWindow;
        nativeWindow.SetHotkeyCallback(HandleHotkey);
    }

    public IDisposable Register(string name, KeyGesture gesture, Action callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(disposed, this);

        int id;
        lock (gate)
        {
            id = nextId++;
        }

        if (gesture.VirtualKey <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gesture), "Hotkey virtual keys must be positive.");
        }

        nativeWindow.RegisterHotKey(id, ToNativeModifiers(gesture.Modifiers), checked((uint)gesture.VirtualKey));
        var registration = new HotkeyRegistration(this, id, name, gesture, callback);
        lock (gate)
        {
            registrations.Add(id, registration);
        }

        return registration;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        HotkeyRegistration[] snapshot;
        lock (gate)
        {
            snapshot = registrations.Values.ToArray();
            registrations.Clear();
        }

        foreach (HotkeyRegistration registration in snapshot)
        {
            nativeWindow.UnregisterHotKey(registration.Id);
        }

        nativeWindow.SetHotkeyCallback(null);
    }

    private void Unregister(HotkeyRegistration registration)
    {
        bool removed;
        lock (gate)
        {
            removed = registrations.Remove(registration.Id);
        }

        if (removed)
        {
            nativeWindow.UnregisterHotKey(registration.Id);
        }
    }

    private void HandleHotkey(int id)
    {
        HotkeyRegistration? registration;
        lock (gate)
        {
            _ = registrations.TryGetValue(id, out registration);
        }

        registration?.Callback();
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        uint nativeModifiers = Win32HotkeyModifiers.NoRepeat;
        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            nativeModifiers |= Win32HotkeyModifiers.Alt;
        }

        if ((modifiers & HotkeyModifiers.Control) != 0)
        {
            nativeModifiers |= Win32HotkeyModifiers.Control;
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            nativeModifiers |= Win32HotkeyModifiers.Shift;
        }

        if ((modifiers & HotkeyModifiers.Windows) != 0)
        {
            nativeModifiers |= Win32HotkeyModifiers.Windows;
        }

        return nativeModifiers;
    }

    private sealed class HotkeyRegistration : IDisposable
    {
        private readonly OverlayHotkeyManager owner;
        private bool disposed;

        public HotkeyRegistration(OverlayHotkeyManager owner, int id, string name, KeyGesture gesture, Action callback)
        {
            this.owner = owner;
            Id = id;
            Name = name;
            Gesture = gesture;
            Callback = callback;
        }

        public int Id { get; }

        public string Name { get; }

        public KeyGesture Gesture { get; }

        public Action Callback { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            owner.Unregister(this);
        }
    }
}
