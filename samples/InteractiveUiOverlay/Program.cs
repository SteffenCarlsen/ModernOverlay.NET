using ModernOverlay;
using ModernOverlay.UI;
using UiImage = ModernOverlay.UI.Image;

byte[] samplePng =
[
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
    0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D, 0x00, 0x00,
    0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
];

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Interactive UI Sample",
    Bounds = new WindowBounds(160, 160, 820, 700),
    InputMode = OverlayInputMode.SelectiveClickThrough,
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay);
using ImageHandle sampleImage = overlay.Resources.CreateImage(samplePng);
var layoutStore = new MemoryLayoutStore();
UiPlacement mainPlacement = UiPlacement.Persisted("main-window", UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(24f)));
UiTheme alternateTheme = UiTheme.Default with
{
    Accent = ColorRgba.FromBytes(113, 191, 134),
    Surface = ColorRgba.FromBytes(26, 35, 38, 236),
    SurfaceHover = ColorRgba.FromBytes(38, 50, 53, 242),
    SurfacePressed = ColorRgba.FromBytes(50, 65, 68, 248),
    Border = ColorRgba.FromBytes(90, 126, 120, 230),
};
bool alternateThemeActive = false;
int metricsFrameCounter = 0;

TextBlock status = new()
{
    Text = "Ready",
    Padding = new Thickness(0f, 0f, 0f, 4f),
};
TextBlock metricsStatus = new()
{
    Text = "Metrics pending",
};
TextBox textBox = new()
{
    Placeholder = "Type here",
    Text = "Hello overlay UI",
};
Slider slider = new()
{
    Minimum = 0f,
    Maximum = 100f,
    Value = 42f,
};
ProgressBar progress = new()
{
    Minimum = 0f,
    Maximum = 100f,
    Value = slider.Value,
    Height = 12f,
};
ComboBox comboBox = new()
{
    Placeholder = "Choose mode",
};
comboBox.Items.Add("Normal");
comboBox.Items.Add("Compact");
comboBox.Items.Add("Detailed");
comboBox.SelectedIndex = 0;

ListBox listBox = new()
{
    Height = 92f,
};
listBox.Items.Add("Canvas");
listBox.Items.Add("StackPanel");
listBox.Items.Add("Grid");
listBox.Items.Add("Popup");
listBox.SelectedIndex = 0;

SegmentedControl segmented = new()
{
    Width = 260f,
};
segmented.Items.Add("Inspect");
segmented.Items.Add("Edit");
segmented.Items.Add("Preview");
segmented.SelectedIndex = 0;

ColorPicker colorPicker = new()
{
    Height = 118f,
};
NumberBox opacityBox = new()
{
    Minimum = 0d,
    Maximum = 100d,
    Step = 5d,
    Value = 65d,
};
UiImage image = new()
{
    Source = sampleImage,
    SourceRect = new RectF(0f, 0f, 1f, 1f),
    Width = 36f,
    Height = 36f,
    Stretch = UiImageStretch.Fill,
    ImageOpacity = 0.65f,
};

UiWindow window = new()
{
    Title = "Interactive UI",
    Width = 520f,
    Height = 640f,
    LayoutKey = "main-window",
    LayoutStore = layoutStore,
    Placement = mainPlacement,
};

StackPanel content = new()
{
    Spacing = 8f,
};
window.Content = content;

Button button = new()
{
    Text = "Click",
    Width = 120f,
};
Button contextButton = new()
{
    Text = "Context",
    Width = 120f,
};
Button themeButton = new()
{
    Text = "Theme",
    Width = 120f,
};
Button restoreButton = new()
{
    Text = "Restore",
    Width = 120f,
};
CheckBox checkbox = new()
{
    Text = "Tri-state",
    IsThreeState = true,
};
RadioButton firstRadio = new()
{
    Text = "Alpha",
    GroupName = "sample",
    IsChecked = true,
};
RadioButton secondRadio = new()
{
    Text = "Beta",
    GroupName = "sample",
};

button.Click += (_, _) => status.Text = $"Clicked at {DateTime.Now:T}";
ToolTip buttonToolTip = new()
{
    Owner = button,
    Text = "Button tooltip",
    InitialDelay = TimeSpan.FromMilliseconds(250),
};
ContextMenu contextMenu = CreateContextMenu(status);
contextMenu.Owner = contextButton;
contextButton.Click += (_, _) =>
{
    contextMenu.IsOpen = true;
    status.Text = "Context menu opened";
};
themeButton.Click += (_, _) =>
{
    alternateThemeActive = !alternateThemeActive;
    ui.ApplyTheme(alternateThemeActive ? alternateTheme : UiTheme.Default);
    status.Text = alternateThemeActive ? "Applied alternate theme" : "Applied default theme";
};
restoreButton.Click += (_, _) =>
{
    layoutStore.Save("main-window", UiPlacement.Manual(24f, 24f, 520f, 640f));
    window.Placement = mainPlacement;
    status.Text = "Layout restored through IUiLayoutStore";
};
checkbox.CheckStateChanged += (_, _) => status.Text = $"Check state: {checkbox.CheckState}";
firstRadio.CheckedChanged += (_, _) =>
{
    if (firstRadio.IsChecked)
    {
        status.Text = "Selected Alpha";
    }
};
secondRadio.CheckedChanged += (_, _) =>
{
    if (secondRadio.IsChecked)
    {
        status.Text = "Selected Beta";
    }
};
slider.ValueChanged += (_, _) =>
{
    progress.Value = slider.Value;
    status.Text = $"Slider: {slider.Value:0}";
};
textBox.TextChanged += (_, _) => status.Text = $"Text length: {textBox.Text.Length}";
comboBox.SelectionChanged += (_, _) => status.Text = $"Mode: {comboBox.SelectedItem}";
listBox.SelectionChanged += (_, _) => status.Text = $"List: {listBox.SelectedItem}";
segmented.SelectionChanged += (_, _) => status.Text = $"Segment: {segmented.Items[segmented.SelectedIndex]}";
colorPicker.ColorChanged += (_, _) => status.Text = $"Color: {colorPicker.Value.R:0.00}, {colorPicker.Value.G:0.00}, {colorPicker.Value.B:0.00}";
opacityBox.ValueChanged += (_, _) =>
{
    image.ImageOpacity = (float)(opacityBox.Value / 100d);
    status.Text = $"Image opacity: {opacityBox.Value:0}%";
};

content.Children.Add(CreateMenu(status));
content.Children.Add(status);
content.Children.Add(textBox);
content.Children.Add(CreateControlsGrid(button, contextButton, checkbox, firstRadio, secondRadio));
content.Children.Add(CreateActionGroup(themeButton, restoreButton));
content.Children.Add(slider);
content.Children.Add(progress);
content.Children.Add(comboBox);
content.Children.Add(listBox);
content.Children.Add(CreateMediaGroup(opacityBox, image));
content.Children.Add(CreateTabs(segmented, colorPicker));

ui.Root.Children.Add(window);
ui.Root.Children.Add(CreateDiagnosticsWindow(metricsStatus));
ui.Root.Children.Add(contextMenu);
ui.Root.Children.Add(buttonToolTip);

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    if (metricsFrameCounter++ % 15 == 0)
    {
        OverlayUiMetrics metrics = ui.Metrics;
        metricsStatus.Text = $"Elements {metrics.ElementCount} | Layout {metrics.LayoutPasses} | Render {metrics.RenderPasses} | Popups {metrics.ActivePopupCount}";
    }

    ui.Render(frame);
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
await overlay.RunAsync(cts.Token);

static Grid CreateControlsGrid(Button button, Button contextButton, CheckBox checkbox, RadioButton firstRadio, RadioButton secondRadio)
{
    var grid = new Grid
    {
        Height = 66f,
    };
    grid.Columns.Add(new GridDefinition(GridLength.Pixel(130f)));
    grid.Columns.Add(new GridDefinition(GridLength.Star()));
    grid.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));
    grid.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));

    var buttonColumn = new StackPanel
    {
        Spacing = 4f,
    };
    buttonColumn.Children.Add(button);
    buttonColumn.Children.Add(contextButton);
    Grid.SetRow(buttonColumn, 0);
    Grid.SetColumn(buttonColumn, 0);
    Grid.SetRowSpan(buttonColumn, 2);
    Grid.SetRow(checkbox, 0);
    Grid.SetColumn(checkbox, 1);
    var radioRow = new StackPanel
    {
        Orientation = UiOrientation.Horizontal,
        Spacing = 12f,
    };
    radioRow.Children.Add(firstRadio);
    radioRow.Children.Add(secondRadio);
    Grid.SetRow(radioRow, 1);
    Grid.SetColumn(radioRow, 1);

    grid.Children.Add(buttonColumn);
    grid.Children.Add(checkbox);
    grid.Children.Add(radioRow);
    return grid;
}

static StackPanel CreateActionGroup(Button themeButton, Button restoreButton)
{
    var row = new StackPanel
    {
        Orientation = UiOrientation.Horizontal,
        Spacing = 8f,
    };
    row.Children.Add(themeButton);
    row.Children.Add(restoreButton);
    return row;
}

static UiWindow CreateDiagnosticsWindow(TextBlock metricsStatus)
{
    var content = new StackPanel
    {
        Spacing = 8f,
    };
    content.Children.Add(new TextBlock { Text = "Live UI metrics" });
    content.Children.Add(metricsStatus);
    return new UiWindow
    {
        Title = "Diagnostics",
        Width = 230f,
        Height = 150f,
        Placement = UiPlacement.AnchorTo(OverlayAnchor.BottomRight, new Thickness(24f)),
        MinimizeBehavior = MinimizeBehavior.Dock,
        Content = content,
    };
}

static Menu CreateMenu(TextBlock status)
{
    var menu = new Menu();
    menu.Items.Add(new UiMenuItem("Apply", UiCommand.FromAction(() => status.Text = "Menu Apply")));
    menu.Items.Add(new UiMenuItem("Reset", UiCommand.FromAction(() => status.Text = "Menu Reset")));
    menu.Items.Add(new UiMenuItem("Disabled") { IsEnabled = false });
    return menu;
}

static ContextMenu CreateContextMenu(TextBlock status)
{
    var menu = new ContextMenu
    {
        PlacementMode = UiPopupPlacementMode.OwnerAnchor,
    };
    menu.Items.Add(new UiMenuItem("Pin panel", UiCommand.FromAction(() => status.Text = "Pinned")));
    menu.Items.Add(new UiMenuItem("Restore layout", UiCommand.FromAction(() => status.Text = "Restore requested")));
    menu.Items.Add(new UiMenuItem("Unavailable") { IsEnabled = false });
    return menu;
}

static GroupBox CreateMediaGroup(NumberBox opacityBox, UiImage image)
{
    var row = new StackPanel
    {
        Orientation = UiOrientation.Horizontal,
        Spacing = 10f,
    };
    row.Children.Add(image);
    row.Children.Add(opacityBox);
    return new GroupBox
    {
        Header = "Image",
        Height = 86f,
        Content = row,
    };
}

static TabControl CreateTabs(SegmentedControl segmented, ColorPicker colorPicker)
{
    var tabs = new TabControl
    {
        Height = 160f,
    };
    var overview = new StackPanel
    {
        Spacing = 8f,
        Padding = new Thickness(4f),
    };
    overview.Children.Add(new TextBlock { Text = "Keyboard focus, popup selection, and retained layout are active." });
    overview.Children.Add(segmented);
    tabs.Add("Overview", overview);
    tabs.Add("Color", colorPicker);
    return tabs;
}

internal sealed class MemoryLayoutStore : IUiLayoutStore
{
    private readonly Dictionary<string, UiPlacement> placements = [];

    public bool TryLoad(string key, out UiPlacement placement)
        => placements.TryGetValue(key, out placement);

    public void Save(string key, UiPlacement placement)
        => placements[key] = placement;
}
