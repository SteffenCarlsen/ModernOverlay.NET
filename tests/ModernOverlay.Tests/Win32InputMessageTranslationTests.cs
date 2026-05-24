using ModernOverlay.Win32;
using System.Reflection;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class Win32InputMessageTranslationTests
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmChar = 0x0102;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmSysChar = 0x0106;
    private const uint WmMouseMove = 0x0200;
    private const int VirtualKeyA = 0x41;
    private const int VirtualKeyMenu = 0x12;

    [TestMethod]
    public void KeyboardMessagesTranslateRepeatScanAndStateFlags()
    {
        Win32KeyboardEvent? keyDown = TranslateKeyboard(
            WmKeyDown,
            VirtualKeyA,
            CreateKeyLParam(repeatCount: 3, scanCode: 0x1E, extended: true, wasDown: true, transition: false));

        Assert.IsNotNull(keyDown);
        Assert.AreEqual(VirtualKeyA, keyDown.Value.VirtualKey);
        Assert.IsTrue(keyDown.Value.IsPressed);
        Assert.IsFalse(keyDown.Value.IsSystemKey);
        Assert.AreEqual(3, keyDown.Value.RepeatCount);
        Assert.AreEqual(0x1E, keyDown.Value.ScanCode);
        Assert.IsTrue(keyDown.Value.IsExtendedKey);
        Assert.IsTrue(keyDown.Value.WasDown);
        Assert.IsFalse(keyDown.Value.IsTransitionState);
    }

    [TestMethod]
    public void SystemKeyboardMessagesTranslatePressedAndReleasedState()
    {
        Win32KeyboardEvent? sysDown = TranslateKeyboard(
            WmSysKeyDown,
            VirtualKeyMenu,
            CreateKeyLParam(repeatCount: 1, scanCode: 0x38, extended: false, wasDown: false, transition: false));
        Win32KeyboardEvent? sysUp = TranslateKeyboard(
            WmSysKeyUp,
            VirtualKeyMenu,
            CreateKeyLParam(repeatCount: 1, scanCode: 0x38, extended: false, wasDown: true, transition: true));

        Assert.IsNotNull(sysDown);
        Assert.IsTrue(sysDown.Value.IsPressed);
        Assert.IsTrue(sysDown.Value.IsSystemKey);
        Assert.IsFalse(sysDown.Value.IsTransitionState);

        Assert.IsNotNull(sysUp);
        Assert.IsFalse(sysUp.Value.IsPressed);
        Assert.IsTrue(sysUp.Value.IsSystemKey);
        Assert.IsTrue(sysUp.Value.WasDown);
        Assert.IsTrue(sysUp.Value.IsTransitionState);
    }

    [TestMethod]
    public void UnsupportedKeyboardMessageIsIgnored()
    {
        Win32KeyboardEvent? keyboard = TranslateKeyboard(WmMouseMove, VirtualKeyA, 0);

        Assert.IsNull(keyboard);
    }

    [TestMethod]
    public void CharacterMessagesTranslateTextAndSystemFlag()
    {
        object translator = CreateTextTranslator();
        Win32TextInputEvent? normal = TranslateText(translator, WmChar, 'A');
        Win32TextInputEvent? system = TranslateText(translator, WmSysChar, 'x');

        Assert.IsNotNull(normal);
        Assert.AreEqual("A", normal.Value.Text);
        Assert.IsFalse(normal.Value.IsSystemCharacter);

        Assert.IsNotNull(system);
        Assert.AreEqual("x", system.Value.Text);
        Assert.IsTrue(system.Value.IsSystemCharacter);
    }

    [TestMethod]
    public void CharacterMessagesBufferUtf16SurrogatePairs()
    {
        object translator = CreateTextTranslator();

        Win32TextInputEvent? high = TranslateText(translator, WmChar, '\uD83D');
        Win32TextInputEvent? low = TranslateText(translator, WmChar, '\uDE80');

        Assert.IsNull(high);
        Assert.IsNotNull(low);
        Assert.AreEqual("🚀", low.Value.Text);
    }

    [TestMethod]
    public void CharacterMessagesReplaceUnpairedSurrogates()
    {
        object translator = CreateTextTranslator();

        Win32TextInputEvent? unpairedLow = TranslateText(translator, WmChar, '\uDE80');
        _ = TranslateText(translator, WmSysChar, '\uD83D');
        Win32TextInputEvent? pendingHighThenText = TranslateText(translator, WmChar, 'A');

        Assert.IsNotNull(unpairedLow);
        Assert.AreEqual("\uFFFD", unpairedLow.Value.Text);
        Assert.IsNotNull(pendingHighThenText);
        Assert.AreEqual("\uFFFDA", pendingHighThenText.Value.Text);
        Assert.IsTrue(pendingHighThenText.Value.IsSystemCharacter);
    }

    [TestMethod]
    public void CharacterMessagesCanTranslateFullCodePointInput()
    {
        object translator = CreateTextTranslator();

        Win32TextInputEvent? emoji = TranslateText(translator, WmChar, 0x1F680);

        Assert.IsNotNull(emoji);
        Assert.AreEqual("🚀", emoji.Value.Text);
    }

    [TestMethod]
    public void UnsupportedTextMessageIsIgnored()
    {
        object translator = CreateTextTranslator();
        Win32TextInputEvent? text = TranslateText(translator, WmKeyDown, 'A');

        Assert.IsNull(text);
    }

    private static nint CreateKeyLParam(int repeatCount, int scanCode, bool extended, bool wasDown, bool transition)
    {
        uint value = (ushort)repeatCount;
        value |= ((uint)scanCode & 0xFF) << 16;
        if (extended)
        {
            value |= 1u << 24;
        }

        if (wasDown)
        {
            value |= 1u << 30;
        }

        if (transition)
        {
            value |= 1u << 31;
        }

        return new nint(unchecked((int)value));
    }

    private static Win32KeyboardEvent? TranslateKeyboard(uint message, int virtualKey, nint lParam)
    {
        MethodInfo method = typeof(Win32OverlayWindow).GetMethod("TryGetKeyboardEvent", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(Win32OverlayWindow), "TryGetKeyboardEvent");
        object?[] args = [message, new nuint((uint)virtualKey), lParam, default(Win32KeyboardEvent)];
        return (bool)method.Invoke(null, args)!
            ? (Win32KeyboardEvent)args[3]!
            : null;
    }

    private static object CreateTextTranslator()
    {
        Type type = typeof(Win32OverlayWindow).Assembly.GetType("ModernOverlay.Win32.Win32TextInputTranslator")
            ?? throw new MissingMemberException("ModernOverlay.Win32.Win32TextInputTranslator");
        return Activator.CreateInstance(type, nonPublic: true)
            ?? throw new MissingMethodException("ModernOverlay.Win32.Win32TextInputTranslator", ".ctor");
    }

    private static Win32TextInputEvent? TranslateText(object translator, uint message, int codePoint)
    {
        MethodInfo method = translator.GetType().GetMethod("TryGetTextInputEvent", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException("Win32TextInputTranslator", "TryGetTextInputEvent");
        object?[] args = [message, new nuint((uint)codePoint), default(Win32TextInputEvent)];
        return (bool)method.Invoke(translator, args)!
            ? (Win32TextInputEvent)args[2]!
            : null;
    }
}
