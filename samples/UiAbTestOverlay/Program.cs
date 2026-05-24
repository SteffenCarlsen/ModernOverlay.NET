using System;
using System.Collections.Generic;
using System.IO;
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
using ImageHandle sampleImage = overlay.Resources.CreateImage(ResolveAssetPath("modernoverlay-icon.png"));

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
float controlsWindowWidth = Math.Clamp(fullScreenBounds.Width - 220f, 520f, 760f);
float controlsWindowHeight = Math.Clamp(fullScreenBounds.Height - 96f, 420f, 920f);

TextBlock status = new() { Text = "Ready", TextWrapping = UiTextWrapping.Wrap, MaxLines = 2 };
TextBlock metrics = new() { Text = "Metrics pending" };
TextBlock bounds = new() { Text = "Bounds pending", TextWrapping = UiTextWrapping.Wrap, MaxLines = 2 };

void SetStatus(string value)
{
    status.Text = $"{DateTime.Now:T} {value}";
}

UiCommand sampleCommand = new(_ => SetStatus("Command executed"), _ => commandEnabled);

UiWindow layoutWindow = CreateLayoutWindow(out Action<string> showLayoutPreview);
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
    showLayoutPreview,
    controlsWindowWidth,
    controlsWindowHeight);

UiWindow popupWindow = CreatePopupWindow(out Button popupButton, out Button contextButton, out Button tooltipButton);
UiWindow textInputWindow = CreateTextInputWindow();
UiWindow collapseWindow = CreateMinimizeWindow("Collapse", MinimizeBehavior.CollapseToTitleBar, new Thickness(24f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow hideWindow = CreateMinimizeWindow("Hide", MinimizeBehavior.HideUntilRestored, new Thickness(236f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow dockWindow = CreateMinimizeWindow("Dock", MinimizeBehavior.Dock, new Thickness(448f, 24f, 0f, 0f), OverlayAnchor.BottomLeft);
UiWindow diagnosticsWindow = CreateDiagnosticsWindow(metrics);

Popup popup = CreatePopup(popupButton);
ContextMenu contextMenu = CreateContextMenu(contextButton);
ToolTip showcaseToolTip = new()
{
    Owner = tooltipButton,
    Text = "ToolTip opened from hover",
    InitialDelay = TimeSpan.FromMilliseconds(250),
    ShowDuration = TimeSpan.FromSeconds(4),
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

foreach (UiWindow window in new[] { controlsWindow, layoutWindow, popupWindow, textInputWindow, collapseWindow, hideWindow, dockWindow, diagnosticsWindow })
{
    window.CloseRequested += (_, _) =>
    {
        window.Visibility = UiVisibility.Collapsed;
        SetStatus($"{window.Title} closed");
    };
}

Button restoreAll = new() { Text = "Restore windows", Width = 150f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
restoreAll.Click += (_, _) =>
{
    foreach (UiWindow window in new[] { controlsWindow, layoutWindow, popupWindow, textInputWindow, collapseWindow, hideWindow, dockWindow, diagnosticsWindow })
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
ui.Root.Children.Add(textInputWindow);
ui.Root.Children.Add(collapseWindow);
ui.Root.Children.Add(hideWindow);
ui.Root.Children.Add(dockWindow);
ui.Root.Children.Add(diagnosticsWindow);
ui.Root.Children.Add(restoreAll);
ui.Root.Children.Add(popup);
ui.Root.Children.Add(contextMenu);
ui.Root.Children.Add(showcaseToolTip);

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
    Action<string> showLayoutPreview,
    float windowWidth,
    float windowHeight)
{
    Button commandButton = new() { Text = "Command", Width = 120f, Command = command, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    Button canExecuteButton = new() { Text = "CanExecute", Width = 120f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    Button themeButton = new() { Text = "Theme A/B", Width = 120f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    Button persistButton = new() { Text = "Persist", Width = 100f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    ToggleButton toggle = new() { Text = "Toggle", Width = 110f, IsThreeState = true, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    CheckBox checkBox = new() { Text = "Tri-state", IsThreeState = true };
    RadioButton alpha = new() { Text = "Alpha", GroupName = "ab", IsChecked = true };
    RadioButton beta = new() { Text = "Beta", GroupName = "ab" };
    TextBox textBox = new() { Text = "Type here", Placeholder = "TextBox input" };
    Slider slider = new() { Minimum = 0f, Maximum = 100f, Value = 40f };
    ProgressBar progress = new() { Minimum = 0f, Maximum = 100f, Value = slider.Value, Height = 12f };
    ComboBox comboBox = new() { Placeholder = "Combo" };
    ListBox listBox = new() { Title = "Layout preview", Height = 152f };
    NumberBox numberBox = new() { Minimum = 0d, Maximum = 100d, Step = 5d, Value = 55d };
    ColorPicker colorPicker = new()
    {
        Label = "Selfmade indicator colour",
        ShowHexText = false,
        Value = ColorRgba.FromBytes(18, 240, 52, 254),
        Width = 230f,
    };
    SegmentedControl segmented = new() { Width = 260f };
    UiImage image = new()
    {
        Source = imageHandle,
        Width = 64f,
        Height = 52f,
        Stretch = UiImageStretch.Uniform,
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
    listBox.SelectedIndex = 0;

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
    listBox.SelectionChanged += (_, _) =>
    {
        if (listBox.SelectedItem is string layoutName)
        {
            showLayoutPreview(layoutName);
            SetStatus($"Previewing {layoutName}");
        }
    };
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
    alignmentButtons.Children.Add(new Button { Text = "Center", Width = 82f, TextHorizontalAlignment = UiHorizontalAlignment.Center });
    alignmentButtons.Children.Add(new Button { Text = "Right", Width = 82f, TextHorizontalAlignment = UiHorizontalAlignment.Right });

    TabControl tabs = new() { Height = 238f };
    StackPanel overview = new() { Spacing = 8f, Padding = new Thickness(4f) };
    overview.Children.Add(statusText);
    overview.Children.Add(boundsText);
    overview.Children.Add(segmented);
    overview.Children.Add(alignmentButtons);
    tabs.Add("Overview", overview);
    Canvas colorSurface = new();
    colorSurface.Children.Add(colorPicker);
    tabs.Add("Color", colorSurface);

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(CreateMenu());
    content.Children.Add(form);
    content.Children.Add(textBox);
    content.Children.Add(slider);
    content.Children.Add(progress);
    content.Children.Add(comboBox);
    content.Children.Add(listBox);
    content.Children.Add(new GroupBox { Header = "Icon + NumberBox for opacity", Height = 88f, Content = media });
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

UiWindow CreateLayoutWindow(out Action<string> showLayoutPreview)
{
    GroupBox preview = new() { Height = 190f };
    showLayoutPreview = layoutName =>
    {
        preview.Header = $"{layoutName} preview";
        preview.Content = CreateLayoutPreview(layoutName);
    };
    showLayoutPreview("Canvas");

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(preview);

    return new UiWindow
    {
        Title = "Layouts",
        Width = 420f,
        Height = 244f,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(0f, 64f, 32f, 0f)),
        Content = content,
    };
}

UiElement CreateLayoutPreview(string layoutName)
{
    return layoutName switch
    {
        "StackPanel" => CreateStackPanelPreview(),
        "DockPanel" => CreateDockPanelPreview(),
        "WrapPanel" => CreateWrapPanelPreview(),
        "Grid" => CreateGridPreview(),
        _ => CreateCanvasPreview(),
    };
}

Canvas CreateCanvasPreview()
{
    Canvas canvas = new() { Height = 132f };
    Button canvasA = CreateLayoutBlock("Canvas A", 108f, 34f);
    Button canvasB = CreateLayoutBlock("Canvas B", 132f, 34f);
    Button canvasC = CreateLayoutBlock("Manual position", 150f, 34f);
    Canvas.SetLeft(canvasA, 8f);
    Canvas.SetTop(canvasA, 8f);
    Canvas.SetLeft(canvasB, 124f);
    Canvas.SetTop(canvasB, 48f);
    Canvas.SetLeft(canvasC, 220f);
    Canvas.SetTop(canvasC, 90f);
    canvas.Children.Add(canvasA);
    canvas.Children.Add(canvasB);
    canvas.Children.Add(canvasC);
    return canvas;
}

StackPanel CreateStackPanelPreview()
{
    StackPanel stack = new() { Spacing = 7f, Height = 132f };
    stack.Children.Add(CreateLayoutBlock("First row", 120f, 32f));
    stack.Children.Add(CreateLayoutBlock("Second row", 180f, 32f));
    stack.Children.Add(CreateLayoutBlock("Third row", 96f, 32f));
    return stack;
}

DockPanel CreateDockPanelPreview()
{
    DockPanel dock = new() { Height = 132f };
    Button dockLeft = CreateLayoutBlock("Left", 74f, 132f);
    Button dockTop = CreateLayoutBlock("Top", 0f, 34f);
    Button dockFill = CreateLayoutBlock("Fill", 0f, 0f);
    DockPanel.SetDock(dockLeft, Dock.Left);
    DockPanel.SetDock(dockTop, Dock.Top);
    dock.Children.Add(dockLeft);
    dock.Children.Add(dockTop);
    dock.Children.Add(dockFill);
    return dock;
}

WrapPanel CreateWrapPanelPreview()
{
    WrapPanel wrap = new() { Spacing = 7f, Height = 132f };
    foreach (string token in new[] { "Wrap", "Panel", "Flow", "Resizes", "Across", "Rows" })
    {
        wrap.Children.Add(CreateLayoutBlock(token, 82f, 32f));
    }

    return wrap;
}

Grid CreateGridPreview()
{
    Grid grid = new() { Height = 132f };
    grid.Columns.Add(new GridDefinition(GridLength.Pixel(108f)));
    grid.Columns.Add(new GridDefinition(GridLength.Star()));
    grid.Rows.Add(new GridDefinition(GridLength.Pixel(40f)));
    grid.Rows.Add(new GridDefinition(GridLength.Star()));

    Button fixedCell = CreateLayoutBlock("108px", 84f, 32f);
    Button starCell = CreateLayoutBlock("Star column", 140f, 32f);
    Button fillCell = CreateLayoutBlock("Column span", 220f, 52f);
    Grid.SetRow(fixedCell, 0);
    Grid.SetColumn(fixedCell, 0);
    Grid.SetRow(starCell, 0);
    Grid.SetColumn(starCell, 1);
    Grid.SetRow(fillCell, 1);
    Grid.SetColumn(fillCell, 0);
    Grid.SetColumnSpan(fillCell, 2);
    grid.Children.Add(fixedCell);
    grid.Children.Add(starCell);
    grid.Children.Add(fillCell);
    return grid;
}

Button CreateLayoutBlock(string text, float width, float height)
{
    Button block = new()
    {
        Text = text,
        MinWidth = 72f,
        MinHeight = 30f,
        TextHorizontalAlignment = UiHorizontalAlignment.Center,
        TextVerticalAlignment = UiVerticalAlignment.Center,
    };
    if (width > 0f)
    {
        block.Width = width;
    }

    if (height > 0f)
    {
        block.Height = height;
    }

    return block;
}

UiWindow CreatePopupWindow(out Button popupButton, out Button contextButton, out Button tooltipButton)
{
    TextBox target = new() { Placeholder = "Label focus target" };
    Label focusLabel = new() { Text = "Focus target label", Target = target };
    popupButton = new Button { Text = "Popup", Width = 104f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    contextButton = new Button { Text = "Context", Width = 104f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    tooltipButton = new Button { Text = "Tooltip", Width = 104f, TextHorizontalAlignment = UiHorizontalAlignment.Center };
    tooltipButton.Click += (_, _) => SetStatus("Tooltip button clicked");

    StackPanel buttons = new() { Orientation = UiOrientation.Horizontal, Spacing = 8f };
    buttons.Children.Add(popupButton);
    buttons.Children.Add(contextButton);
    buttons.Children.Add(tooltipButton);

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

UiWindow CreateTextInputWindow()
{
    TextBox singleLine = new()
    {
        Text = "Single line",
        Placeholder = "Single-line TextBox",
    };
    TextBox multiline = new()
    {
        Mode = TextBoxMode.MultiLine,
        Text = "First line\nSecond line\nEdit me",
        Placeholder = "Multiline TextBox",
        Height = 112f,
        MaxLines = 4,
    };
    singleLine.TextChanged += (_, _) => SetStatus($"Single-line length: {singleLine.Text.Length}");
    multiline.TextChanged += (_, _) => SetStatus($"Multiline lines: {multiline.Text.AsSpan().Count('\n') + 1}");

    StackPanel content = new() { Spacing = 8f };
    content.Children.Add(new Label { Text = "Single-line TextBox", Target = singleLine });
    content.Children.Add(singleLine);
    content.Children.Add(new Label { Text = "Multiline TextBox", Target = multiline });
    content.Children.Add(multiline);

    return new UiWindow
    {
        Title = "Text Input",
        Width = 420f,
        Height = 252f,
        MinimizeBehavior = MinimizeBehavior.HideUntilRestored,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(0f, 324f, 32f, 0f)),
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
    content.Children.Add(new Button { Text = "Popup button", Width = 130f, TextHorizontalAlignment = UiHorizontalAlignment.Center });
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

static string ResolveAssetPath(string fileName)
{
    foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "assets", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    throw new FileNotFoundException($"Could not find sample asset '{fileName}'.", fileName);
}

internal sealed class MemoryLayoutStore : IUiLayoutStore
{
    private readonly Dictionary<string, UiPlacement> placements = [];

    public bool TryLoad(string key, out UiPlacement placement)
        => placements.TryGetValue(key, out placement);

    public void Save(string key, UiPlacement placement)
        => placements[key] = placement;
}
