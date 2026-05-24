using ModernOverlay;
using ModernOverlay.UI;

await using OverlayWindow overlay = await OverlayWindow.CreateAsync(new OverlayWindowOptions
{
    Title = "ModernOverlay Interactive UI Sample",
    Bounds = new WindowBounds(160, 160, 760, 520),
    InputMode = OverlayInputMode.SelectiveClickThrough,
    FrameRateLimit = FrameRateLimit.Fixed(60),
});

using OverlayUiRoot ui = OverlayUi.Attach(overlay);
var layoutStore = new MemoryLayoutStore();

TextBlock status = new()
{
    Text = "Ready",
    Padding = new Thickness(0f, 0f, 0f, 4f),
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

UiWindow window = new()
{
    Title = "Interactive UI",
    Width = 500f,
    Height = 430f,
    LayoutKey = "main-window",
    LayoutStore = layoutStore,
    Placement = UiPlacement.Persisted("main-window", UiPlacement.AnchorTo(OverlayAnchor.TopLeft, new Thickness(24f))),
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

content.Children.Add(status);
content.Children.Add(textBox);
content.Children.Add(CreateControlsGrid(button, checkbox, firstRadio, secondRadio));
content.Children.Add(slider);
content.Children.Add(progress);
content.Children.Add(comboBox);
content.Children.Add(listBox);
content.Children.Add(CreateTabs(segmented, colorPicker));

ui.Root.Children.Add(window);

overlay.Render += frame =>
{
    frame.Clear(ColorRgba.Transparent);
    ui.Render(frame);
};

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
await overlay.RunAsync(cts.Token);

static Grid CreateControlsGrid(Button button, CheckBox checkbox, RadioButton firstRadio, RadioButton secondRadio)
{
    var grid = new Grid
    {
        Height = 66f,
    };
    grid.Columns.Add(new GridDefinition(GridLength.Pixel(130f)));
    grid.Columns.Add(new GridDefinition(GridLength.Star()));
    grid.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));
    grid.Rows.Add(new GridDefinition(GridLength.Pixel(32f)));

    Grid.SetRow(button, 0);
    Grid.SetColumn(button, 0);
    Grid.SetRowSpan(button, 2);
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

    grid.Children.Add(button);
    grid.Children.Add(checkbox);
    grid.Children.Add(radioRow);
    return grid;
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
