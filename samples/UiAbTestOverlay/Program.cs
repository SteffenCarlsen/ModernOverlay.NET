using System;
using System.Collections.Generic;
using System.Threading;
using ModernOverlay;
using ModernOverlay.UI;
using Forms = System.Windows.Forms;
using UiImage = ModernOverlay.UI.Image;

Forms.Screen? launchScreen = Forms.Screen.FromPoint(Forms.Cursor.Position) ?? Forms.Screen.PrimaryScreen;
System.Drawing.Rectangle screen = launchScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
WindowBounds fullScreenBounds = screen.Width > 0 && screen.Height > 0
    ? new WindowBounds(screen.X, screen.Y, screen.Width, screen.Height)
    : new WindowBounds(0, 0, 1280, 720);

byte[] samplePng =
[
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x48, 0xAA, 0xE5, 0xFF,
    0x1F, 0x00, 0x04, 0x8E, 0x01, 0xEA, 0xF7, 0x97, 0x64, 0x99, 0x00, 0x00,
    0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
];

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay UI A/B Test",
    Bounds = fullScreenBounds,
    InputMode = OverlayInputMode.SelectiveClickThrough,
    FrameRateLimit = FrameRateLimit.Fixed(60),
    TransparencyMode = TransparencyMode.DwmGlassFrame,
    NoActivate = false,
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay);
using ImageHandle sampleImage = overlay.Resources.CreateImage(samplePng);

UiTheme themeA = UiTheme.Default;
UiTheme themeB = UiTheme.Default with
{
    Accent = ColorRgba.FromBytes(227, 117, 89),
    Surface = ColorRgba.FromBytes(28, 37, 42, 238),
    SurfaceHover = ColorRgba.FromBytes(46, 61, 66, 242),
    SurfacePressed = ColorRgba.FromBytes(58, 73, 78, 248),
    Border = ColorRgba.FromBytes(122, 151, 142, 230),
};

var layoutStore = new MemoryLayoutStore();
bool themeBActive = false;
bool commandEnabled = true;
int frameCounter = 0;
float controlsWindowWidth = Math.Clamp(fullScreenBounds.Width - 64f, 360f, 860f);
float controlsWindowHeight = Math.Clamp(fullScreenBounds.Height - 96f, 420f, 760f);

TextBlock status = new() { Text = "Ready", TextWrapping = UiTextWrapping.Wrap, MaxLines = 2 };
TextBlock metrics = new() { Text = "Metrics pending" };
TextBlock bounds = new() { Text = "Bounds pending", TextWrapping = UiTextWrapping.Wrap, MaxLines = 2 };

void SetStatus(string value)
{
    status.Text = $"{DateTime.Now:T} {value}";
}

UiCommand sampleCommand = new(_ => SetStatus("Command executed"), _ => commandEnabled);

UiWindow controlsWindow = CreateControlsWindow(
    status,
    bounds,
    sampleImage,
    sampleCommand,
    () =>
    {
        commandEnabled = !commandEnabled;
        sampleCommand.RaiseCanExecuteChanged();
        SetStatus(commandEnabled ? "Command enabled" : "Command disabled");
    },
    () =>
    {
        themeBActive = !themeBActive;
        ui.ApplyTheme(themeBActive ? themeB : themeA);
        SetStatus(themeBActive ? "Theme B active" : "Theme A active");
    },
    layoutStore,
    controlsWindowWidth,
    controlsWindowHeight);

UiWindow layoutWindow = CreateLayoutWindow();
UiWindow popupWindow = CreatePopupWindow(out Button popupButton, out Button contextButton, out Label focusLabel);
UiWindow collapseWindow = CreateMinimizeWindow("Collapse", MinimizeBehavior.CollapseToTitleBar, new Thickness(24f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow hideWindow = CreateMinimizeWindow("Hide", MinimizeBehavior.HideUntilRestored, new Thickness(236f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow dockWindow = CreateMinimizeWindow("Dock", MinimizeBehavior.Dock, new Thickness(448f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow diagnosticsWindow = CreateDiagnosticsWindow(metrics);

Popup popup = CreatePopup(popupButton);
ContextMenu contextMenu = CreateContextMenu(contextButton);
ToolTip labelToolTip = new()
{
    Owner = focusLabel,
    Text = "Label tooltip",
    InitialDelay = TimeSpan.FromMilliseconds(250),
};

popupButton.Click += (_, _) =>
{
    popup.IsOpen = !popup.IsOpen;
    SetStatus(popup.IsOpen ? "Popup opened" : "Popup closed");
};

contextButton.Click += (_, _) =>
{
    contextMenu.IsOpen = true;
    SetStatus("Context menu opened");
};

foreach (UiWindow window in new[] { controlsWindow, layoutWindow, popupWindow, collapseWindow, hideWindow, dockWindow, diagnosticsWindow })
{
    window.CloseRequested += (_, _) =>
    {
        window.Visibility = UiVisibility.Collapsed;
        SetStatus($"{window.Title} closed");
    };
}

Button restoreAll = new() { Text = "Restore windows", Width = 150f };
restoreAll.Click += (_, _) =>
{
    foreach (UiWindow window in new[] { controlsWindow, layoutWindow, popupWindow, collapseWindow, hideWindow, dockWindow, diagnosticsWindow })
    {
        window.Visibility = UiVisibility.Visible;
        window.Restore();
    }

    SetStatus("All windows restored");
};

Canvas.SetLeft(restoreAll, 18f);
Canvas.SetTop(restoreAll, 18f);

ui.Root.Children.Add(controlsWindow);
ui.Root.Children.Add(layoutWindow);
ui.Root.Children.Add(popupWindow);
ui.Root.Children.Add(collapseWindow);
ui.Root.Children.Add(hideWindow);
ui.Root.Children.Add(dockWindow);
ui.Root.Children.Add(diagnosticsWindow);
ui.Root.Children.Add(restoreAll);
ui.Root.Children.Add(popup);
ui.Root.Children.Add(contextMenu);
ui.Root.Children.Add(labelToolTip);

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    if (frameCounter++ % 15 == 0)
    {
        OverlayUiMetrics uiMetrics = ui.Metrics;
        metrics.Text = $"Elements {uiMetrics.ElementCount} | Layout {uiMetrics.LayoutPasses} | Render {uiMetrics.RenderPasses} | Popups {uiMetrics.ActivePopupCount}";
        WindowBounds pixels = overlay.BoundsPixels;
        RectF dips = overlay.BoundsDips;
        DpiScale dpi = overlay.DpiScale;
        bounds.Text = $"DPI {dpi.X:0.##} x {dpi.Y:0.##} | px {pixels.Width} x {pixels.Height} | DIPs {dips.Width:0} x {dips.Height:0}";
    }

    ui.Render(frame);
};

await overlay.RunAsync(CancellationToken.None);

UiWindow CreateControlsWindow(
    TextBlock statusText,
    TextBlock boundsText,
    ImageHandle imageHandle,
    UiCommand command,
    Action toggleCommand,
    Action toggleTheme,
    IUiLayoutStore store,
    float windowWidth,
    float windowHeight)
{
    Button commandButton = new() { Text = "Command", Width = 120f, Command = command };
    Button canExecuteButton = new() { Text = "CanExecute", Width = 120f };
    Button themeButton = new() { Text = "Theme A/B", Width = 120f };
    Button persistButton = new() { Text = "Persist", Width = 100f };
    ToggleButton toggle = new() { Text = "Toggle", Width = 110f, IsThreeState = true };
    CheckBox checkBox = new() { Text = "Tri-state", IsThreeState = true };
    RadioButton alpha = new() { Text = "Alpha", GroupName = "ab", IsChecked = true };
    RadioButton beta = new() { Text = "Beta", GroupName = "ab" };
    TextBox textBox = new() { Text = "Type here", Placeholder = "TextBox input" };
    Slider slider = new() { Minimum = 0f, Maximum = 100f, Value = 40f };
    ProgressBar progress = new() { Minimum = 0f, Maximum = 100f, Value = slider.Value, Height = 12f };
    ComboBox comboBox = new() { Placeholder = "Combo" };
    ListBox listBox = new() { Height = 116f };
    NumberBox numberBox = new() { Minimum = 0d, Maximum = 100d, Step = 5d, Value = 55d };
    ColorPicker colorPicker = new()
    {
        Label = "Selfmade indicator colour",
        Value = ColorRgba.FromBytes(18, 240, 52, 254),
    };
    SegmentedControl segmented = new() { Width = 260f };
    UiImage image = new()
    {
        Source = imageHandle,
        SourceRect = new RectF(0f, 0f, 1f, 1f),
        Width = 48f,
        Height = 48f,
        Stretch = UiImageStretch.Fill,
        ImageOpacity = 0.55f,
    };

    comboBox.Items.Add("Normal");
    comboBox.Items.Add("Compact");
    comboBox.Items.Add("Dense");
    comboBox.SelectedIndex = 0;

    foreach (string item in new[] { "Canvas", "StackPanel", "DockPanel", "WrapPanel", "Grid" })
    {
        listBox.Items.Add(item);
    }

    foreach (string item in new[] { "A", "B", "Compare" })
    {
        segmented.Items.Add(item);
    }

    segmented.SelectedIndex = 0;

    commandButton.Click += (_, _) => SetStatus("Command button clicked");
    canExecuteButton.Click += (_, _) => toggleCommand();
    themeButton.Click += (_, _) => toggleTheme();
    persistButton.Click += (_, _) =>
    {
        store.Save("ab-main", UiPlacement.Manual(32f, 64f, windowWidth, windowHeight));
        SetStatus("Saved layout through IUiLayoutStore");
    };
    toggle.CheckStateChanged += (_, _) => SetStatus($"Toggle: {toggle.CheckState}");
    checkBox.CheckStateChanged += (_, _) => SetStatus($"CheckBox: {checkBox.CheckState}");
    alpha.CheckedChanged += (_, _) =>
    {
        if (alpha.IsChecked)
        {
            SetStatus("Radio Alpha");
        }
    };
    beta.CheckedChanged += (_, _) =>
    {
        if (beta.IsChecked)
        {
            SetStatus("Radio Beta");
        }
    };
    textBox.TextChanged += (_, _) => SetStatus($"Text length: {textBox.Text.Length}");
    slider.ValueChanged += (_, _) =>
    {
        progress.Value = slider.Value;
        SetStatus($"Slider: {slider.Value:0}");
    };
    comboBox.SelectionChanged += (_, _) => SetStatus($"Combo: {comboBox.SelectedItem}");
    listBox.SelectionChanged += (_, _) => SetStatus($"List: {listBox.SelectedItem}");
    numberBox.ValueChanged += (_, _) =>
    {
        image.ImageOpacity = (float)(numberBox.Value / 100d);
        SetStatus($"Image opacity: {numberBox.Value:0}%");
    };
    colorPicker.ColorChanged += (_, _) => SetStatus($"Color: {colorPicker.Value.R:0.00}, {colorPicker.Value.G:0.00}, {colorPicker.Value.B:0.00}");
    segmented.SelectionChanged += (_, _) => SetStatus($"Segment: {segmented.Items[segmented.SelectedIndex]}");

    Grid form = new() { Height = 96f };
    form.Columns.Add(new GridDefinition(GridLength.Pixel(140f)));
    form.Columns.Add(new GridDefinition(GridLength.Star()));
    form.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));
    form.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));
    form.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));
    Grid.SetRow(commandButton, 0);
    Grid.SetColumn(commandButton, 0);
    Grid.SetRow(canExecuteButton, 1);
    Grid.SetColumn(canExecuteButton, 0);
    Grid.SetRow(themeButton, 2);
    Grid.SetColumn(themeButton, 0);
    Grid.SetRow(checkBox, 0);
    Grid.SetColumn(checkBox, 1);
    Grid.SetRow(alpha, 1);
    Grid.SetColumn(alpha, 1);
    Grid.SetRow(beta, 2);
    Grid.SetColumn(beta, 1);
    form.Children.Add(commandButton);
    form.Children.Add(canExecuteButton);
    form.Children.Add(themeButton);
    form.Children.Add(checkBox);
    form.Children.Add(alpha);
    form.Children.Add(beta);

    StackPanel media = new() { Orientation = UiOrientation.Horizontal, Spacing = 10f };
    media.Children.Add(image);
    media.Children.Add(numberBox);

    StackPanel alignmentButtons = new() { Orientation = UiOrientation.Horizontal, Spacing = 6f };
    alignmentButtons.Children.Add(new Button { Text = "Left", Width = 82f, TextHorizontalAlignment = UiHorizontalAlignment.Left });
    alignmentButtons.Children.Add(new Button { Text = "Center", Width = 82f });
    alignmentButtons.Children.Add(new Button { Text = "Right", Width = 82f, TextHorizontalAlignment = UiHorizontalAlignment.Right });

    TabControl tabs = new() { Height = 238f };
    StackPanel overview = new() { Spacing = 8f, Padding = new Thickness(4f) };
    overview.Children.Add(statusText);
    overview.Children.Add(boundsText);
    overview.Children.Add(segmented);
    overview.Children.Add(alignmentButtons);
    tabs.Add("Overview", overview);
    tabs.Add("Color", colorPicker);

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(CreateMenu());
    content.Children.Add(form);
    content.Children.Add(textBox);
    content.Children.Add(slider);
    content.Children.Add(progress);
    content.Children.Add(comboBox);
    content.Children.Add(listBox);
    content.Children.Add(new GroupBox { Header = "Image + NumberBox", Height = 88f, Content = media });
    content.Children.Add(persistButton);
    content.Children.Add(toggle);
    content.Children.Add(tabs);

    return new UiWindow
    {
        Title = "UI A/B Controls",
        Width = windowWidth,
        Height = windowHeight,
        LayoutKey = "ab-main",
        LayoutStore = store,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(32f, 64f, 0f, 0f)),
        Content = content,
    };
}

UiWindow CreateLayoutWindow()
{
    Canvas canvas = new() { Height = 84f };
    TextBlock canvasA = new() { Text = "Canvas A", Background = null };
    TextBlock canvasB = new() { Text = "Canvas B" };
    Canvas.SetLeft(canvasA, 8f);
    Canvas.SetTop(canvasA, 8f);
    Canvas.SetLeft(canvasB, 124f);
    Canvas.SetTop(canvasB, 42f);
    canvas.Children.Add(canvasA);
    canvas.Children.Add(canvasB);

    DockPanel dock = new() { Height = 86f };
    TextBlock dockLeft = new() { Text = "Left", Width = 64f };
    TextBlock dockTop = new() { Text = "Top", Height = 24f };
    TextBlock dockFill = new() { Text = "Fill" };
    DockPanel.SetDock(dockLeft, Dock.Left);
    DockPanel.SetDock(dockTop, Dock.Top);
    dock.Children.Add(dockLeft);
    dock.Children.Add(dockTop);
    dock.Children.Add(dockFill);

    WrapPanel wrap = new() { Spacing = 6f };
    foreach (string token in new[] { "Wrap", "Panel", "Flow", "Resizes", "Across", "Rows" })
    {
        wrap.Children.Add(new Button { Text = token, Width = 74f });
    }

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(new GroupBox { Header = "Canvas", Height = 104f, Content = canvas });
    content.Children.Add(new GroupBox { Header = "DockPanel", Height = 106f, Content = dock });
    content.Children.Add(new GroupBox { Header = "WrapPanel", Height = 116f, Content = wrap });

    return new UiWindow
    {
        Title = "Layouts",
        Width = 420f,
        Height = 390f,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(0f, 64f, 32f, 0f)),
        Content = content,
    };
}

UiWindow CreatePopupWindow(out Button popupButton, out Button contextButton, out Label focusLabel)
{
    TextBox target = new() { Placeholder = "Label focus target" };
    focusLabel = new Label { Text = "Focus target label", Target = target };
    popupButton = new Button { Text = "Popup", Width = 120f };
    contextButton = new Button { Text = "Context", Width = 120f };

    StackPanel buttons = new() { Orientation = UiOrientation.Horizontal, Spacing = 8f };
    buttons.Children.Add(popupButton);
    buttons.Children.Add(contextButton);

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(buttons);
    content.Children.Add(focusLabel);
    content.Children.Add(target);

    return new UiWindow
    {
        Title = "Popups + Focus",
        Width = 360f,
        Height = 210f,
        MinimizeBehavior = MinimizeBehavior.HideUntilRestored,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(0f, 0f, 32f, 220f)),
        Content = content,
    };
}

UiWindow CreateMinimizeWindow(string title, MinimizeBehavior behavior, Thickness margin, OverlayAnchor anchor)
{
    return new UiWindow
    {
        Title = $"{title} minimize",
        Width = 200f,
        Height = 128f,
        MinimizeBehavior = behavior,
        Placement = UiPlacement.AnchorTo(anchor, margin),
        Content = new TextBlock { Text = behavior.ToString(), TextWrapping = UiTextWrapping.Wrap },
    };
}

UiWindow CreateDiagnosticsWindow(TextBlock metricsText)
{
    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(new TextBlock { Text = "Live metrics" });
    content.Children.Add(metricsText);
    return new UiWindow
    {
        Title = "Diagnostics",
        Width = 280f,
        Height = 150f,
        MinimizeBehavior = MinimizeBehavior.Dock,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(0f, 0f, 32f, 32f)),
        Content = content,
    };
}

Menu CreateMenu()
{
    Menu menu = new();
    menu.Items.Add(new UiMenuItem("Apply", UiCommand.FromAction(() => SetStatus("Menu Apply"))));
    menu.Items.Add(new UiMenuItem("Reset", UiCommand.FromAction(() => SetStatus("Menu Reset"))));
    menu.Items.Add(new UiMenuItem("Disabled") { IsEnabled = false });
    return menu;
}

Popup CreatePopup(Button owner)
{
    Popup popup = new()
    {
        Owner = owner,
        PlacementMode = UiPopupPlacementMode.OwnerAnchor,
        Width = 230f,
    };
    StackPanel content = new() { Spacing = 6f };
    content.Children.Add(new TextBlock { Text = "Popup content" });
    content.Children.Add(new Button { Text = "Popup button", Width = 130f });
    popup.Children.Add(content);
    return popup;
}

ContextMenu CreateContextMenu(Button owner)
{
    ContextMenu menu = new()
    {
        Owner = owner,
        PlacementMode = UiPopupPlacementMode.OwnerAnchor,
    };
    menu.Items.Add(new UiMenuItem("Pin", UiCommand.FromAction(() => SetStatus("Context pin"))));
    menu.Items.Add(new UiMenuItem("Restore", UiCommand.FromAction(() => SetStatus("Context restore"))));
    menu.Items.Add(new UiMenuItem("Unavailable") { IsEnabled = false });
    return menu;
}

internal sealed class MemoryLayoutStore : IUiLayoutStore
{
    private readonly Dictionary<string, UiPlacement> placements = [];

    public bool TryLoad(string key, out UiPlacement placement)
        => placements.TryGetValue(key, out placement);

    public void Save(string key, UiPlacement placement)
        => placements[key] = placement;
}
