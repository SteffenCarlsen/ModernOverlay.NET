namespace ModernOverlay.UI;

/// <summary>
/// Determines whether a point should be treated as interactive for an element-specific input region.
/// </summary>
/// <param name="element">The element that owns the input region.</param>
/// <param name="point">The pointer position in overlay DIPs.</param>
/// <returns><see langword="true"/> when the point belongs to the element's interactive region.</returns>
public delegate bool UiInputRegionHandler(UiElement element, PointF point);

/// <summary>
/// Describes how a routed UI event is currently being dispatched.
/// </summary>
public enum UiRoutedEventPhase
{
    /// <summary>
    /// The event is delivered directly to the target element.
    /// </summary>
    Direct,

    /// <summary>
    /// The event is bubbling from the target element toward the root.
    /// </summary>
    Bubble,
}

/// <summary>
/// Provides routed pointer input data for UI elements.
/// </summary>
public sealed class UiPointerEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UiPointerEventArgs"/> class.
    /// </summary>
    /// <param name="kind">The pointer event kind.</param>
    /// <param name="button">The pointer button associated with the event.</param>
    /// <param name="position">The pointer position in overlay DIPs.</param>
    /// <param name="wheelDelta">The wheel delta for wheel events.</param>
    /// <param name="isHorizontalWheel">Whether the wheel delta is horizontal.</param>
    /// <param name="isDragGesture">Whether this event is part of a drag gesture.</param>
    /// <param name="clickCount">The click count associated with the event.</param>
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

    /// <summary>
    /// Gets the pointer event kind.
    /// </summary>
    public OverlayPointerEventKind Kind { get; }

    /// <summary>
    /// Gets the pointer button associated with the event.
    /// </summary>
    public OverlayPointerButton Button { get; }

    /// <summary>
    /// Gets the pointer position in overlay DIPs.
    /// </summary>
    public PointF Position { get; }

    /// <summary>
    /// Gets the wheel delta for wheel events.
    /// </summary>
    public int WheelDelta { get; }

    /// <summary>
    /// Gets a value indicating whether the wheel delta is horizontal.
    /// </summary>
    public bool IsHorizontalWheel { get; }

    /// <summary>
    /// Gets a value indicating whether the pointer event is part of a drag gesture.
    /// </summary>
    public bool IsDragGesture { get; }

    /// <summary>
    /// Gets the click count associated with the event.
    /// </summary>
    public int ClickCount { get; }

    /// <summary>
    /// Gets a value indicating whether this event represents a double-click.
    /// </summary>
    public bool IsDoubleClick => ClickCount == 2;

    /// <summary>
    /// Gets the current routed event phase.
    /// </summary>
    public UiRoutedEventPhase RoutePhase { get; internal set; } = UiRoutedEventPhase.Direct;

    /// <summary>
    /// Gets the element that was originally hit by the event.
    /// </summary>
    public UiElement? OriginalSource { get; internal set; }

    /// <summary>
    /// Gets the element currently receiving the routed event callback.
    /// </summary>
    public UiElement? Source { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing should stop after the current handler.
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Provides click data for controls that expose click-style events.
/// </summary>
public sealed class UiClickEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UiClickEventArgs"/> class.
    /// </summary>
    /// <param name="position">The click position in overlay DIPs.</param>
    /// <param name="button">The pointer button that produced the click.</param>
    /// <param name="clickCount">The click count.</param>
    public UiClickEventArgs(PointF position, OverlayPointerButton button, int clickCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clickCount);
        Position = position;
        Button = button;
        ClickCount = clickCount;
    }

    /// <summary>
    /// Gets the click position in overlay DIPs.
    /// </summary>
    public PointF Position { get; }

    /// <summary>
    /// Gets the pointer button that produced the click.
    /// </summary>
    public OverlayPointerButton Button { get; }

    /// <summary>
    /// Gets the click count.
    /// </summary>
    public int ClickCount { get; }

    /// <summary>
    /// Gets a value indicating whether this event represents a double-click.
    /// </summary>
    public bool IsDoubleClick => ClickCount == 2;
}

/// <summary>
/// Provides routed keyboard input data for UI elements.
/// </summary>
public sealed class UiKeyboardEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UiKeyboardEventArgs"/> class.
    /// </summary>
    /// <param name="virtualKey">The platform virtual-key code.</param>
    /// <param name="isRepeat">Whether the key event is an auto-repeat.</param>
    /// <param name="modifiers">The active keyboard modifiers.</param>
    public UiKeyboardEventArgs(int virtualKey, bool isRepeat, OverlayModifierKeys modifiers = OverlayModifierKeys.None)
    {
        VirtualKey = virtualKey;
        IsRepeat = isRepeat;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the platform virtual-key code.
    /// </summary>
    public int VirtualKey { get; }

    /// <summary>
    /// Gets a value indicating whether the key event is an auto-repeat.
    /// </summary>
    public bool IsRepeat { get; }

    /// <summary>
    /// Gets the active keyboard modifiers.
    /// </summary>
    public OverlayModifierKeys Modifiers { get; }

    /// <summary>
    /// Gets the current routed event phase.
    /// </summary>
    public UiRoutedEventPhase RoutePhase { get; internal set; } = UiRoutedEventPhase.Direct;

    /// <summary>
    /// Gets the focused element that originally received the event.
    /// </summary>
    public UiElement? OriginalSource { get; internal set; }

    /// <summary>
    /// Gets the element currently receiving the routed event callback.
    /// </summary>
    public UiElement? Source { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing should stop after the current handler.
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Provides routed text input data for focused text-aware UI elements.
/// </summary>
public sealed class UiTextInputEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UiTextInputEventArgs"/> class.
    /// </summary>
    /// <param name="text">The text payload produced by the platform input message.</param>
    public UiTextInputEventArgs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>
    /// Gets the text payload produced by the platform input message.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the current routed event phase.
    /// </summary>
    public UiRoutedEventPhase RoutePhase { get; internal set; } = UiRoutedEventPhase.Direct;

    /// <summary>
    /// Gets the focused element that originally received the text input event.
    /// </summary>
    public UiElement? OriginalSource { get; internal set; }

    /// <summary>
    /// Gets the element currently receiving the routed event callback.
    /// </summary>
    public UiElement? Source { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing should stop after the current handler.
    /// </summary>
    public bool Handled { get; set; }
}
