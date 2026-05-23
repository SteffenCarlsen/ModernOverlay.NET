namespace ModernOverlay.UI;

public delegate bool UiInputRegionHandler(UiElement element, PointF point);

public sealed class UiPointerEventArgs : EventArgs
{
    public UiPointerEventArgs(
        OverlayPointerEventKind kind,
        OverlayPointerButton button,
        PointF position,
        int wheelDelta = 0,
        bool isHorizontalWheel = false,
        bool isDragGesture = false,
        int clickCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clickCount);
        Kind = kind;
        Button = button;
        Position = position;
        WheelDelta = wheelDelta;
        IsHorizontalWheel = isHorizontalWheel;
        IsDragGesture = isDragGesture;
        ClickCount = clickCount;
    }

    public OverlayPointerEventKind Kind { get; }

    public OverlayPointerButton Button { get; }

    public PointF Position { get; }

    public int WheelDelta { get; }

    public bool IsHorizontalWheel { get; }

    public bool IsDragGesture { get; }

    public int ClickCount { get; }

    public bool IsDoubleClick => ClickCount == 2;

    public bool Handled { get; set; }
}

public sealed class UiClickEventArgs : EventArgs
{
    public UiClickEventArgs(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clickCount);
        Position = position;
        Button = button;
        ClickCount = clickCount;
    }

    public PointF Position { get; }

    public OverlayPointerButton Button { get; }

    public int ClickCount { get; }

    public bool IsDoubleClick => ClickCount == 2;
}

public sealed class UiKeyboardEventArgs : EventArgs
{
    public UiKeyboardEventArgs(int virtualKey, bool isRepeat, OverlayModifierKeys modifiers = OverlayModifierKeys.None)
    {
        VirtualKey = virtualKey;
        IsRepeat = isRepeat;
        Modifiers = modifiers;
    }

    public int VirtualKey { get; }

    public bool IsRepeat { get; }

    public OverlayModifierKeys Modifiers { get; }

    public bool Handled { get; set; }
}

public sealed class UiTextInputEventArgs : EventArgs
{
    public UiTextInputEventArgs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    public string Text { get; }

    public bool Handled { get; set; }
}
