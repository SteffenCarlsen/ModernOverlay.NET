using ModernOverlay.UI;
using UiButton = ModernOverlay.UI.Button;
using UiCheckBox = ModernOverlay.UI.CheckBox;
using UiComboBox = ModernOverlay.UI.ComboBox;
using UiContextMenu = ModernOverlay.UI.ContextMenu;
using UiGroupBox = ModernOverlay.UI.GroupBox;
using UiLabel = ModernOverlay.UI.Label;
using UiMenu = ModernOverlay.UI.Menu;
using UiProgressBar = ModernOverlay.UI.ProgressBar;
using UiRadioButton = ModernOverlay.UI.RadioButton;
using UiTabControl = ModernOverlay.UI.TabControl;
using UiTextBox = ModernOverlay.UI.TextBox;
using UiToolTip = ModernOverlay.UI.ToolTip;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class SpecExampleCompileTests
{
    [TestMethod]
    public void MinimalSpecExampleImportsCompile()
    {
        _ = typeof(DrawingNamespace);
        _ = typeof(WindowsNamespace);

        var options = new OverlayWindowOptions
        {
            Title = "Demo Overlay",
            Bounds = WindowBounds.FromPixels(100, 100, 800, 600),
            InputMode = OverlayInputMode.ClickThrough,
            ZOrder = OverlayZOrder.TopMost,
            IsVisible = true,
            FrameRateLimit = FrameRateLimit.Fixed(144),
        };

        Assert.AreEqual("Demo Overlay", options.Title);
        Assert.AreEqual(new WindowBounds(100, 100, 800, 600), options.Bounds);
        Assert.AreEqual(OverlayInputMode.ClickThrough, options.InputMode);
        Assert.AreEqual(OverlayZOrder.TopMost, options.ZOrder);
        Assert.IsTrue(options.IsVisible);
        Assert.AreEqual(144d, options.FrameRateLimit.FramesPerSecond);
    }

    [TestMethod]
    public void SpecNamedWindowHelpersCompile()
    {
        Assert.IsFalse(WindowQuery.IsWindow(default));
        Assert.IsFalse(WindowQuery.IsVisible(default));
        Assert.IsFalse(WindowQuery.TryGetWindowBounds(default, out WindowBounds bounds));
        Assert.AreEqual(default, bounds);

        Assert.IsFalse(WindowQuery.TryGetWindowStyles(default, out WindowStylesSnapshot styles));
        Assert.AreEqual(default, styles);

        Assert.IsFalse(WindowEffects.TryExtendFrameIntoClientArea(default));
        Assert.IsFalse(WindowEffects.TryEnableBlurBehind(default));
        Assert.IsFalse(WindowEffects.TryExcludeFromCapture(default));
        Assert.IsFalse(WindowEffects.TryClearDisplayAffinity(default));
    }

    [TestMethod]
    public void InteractiveUiPublicApiShapeCompiles()
    {
        var options = new OverlayUiOptions
        {
            RegisterInputRegions = true,
            Theme = UiTheme.Default,
        };
        UiThemeReadabilityReport readability = options.Theme.CheckReadability();

        var store = new MemoryUiLayoutStore();
        store.Save("settings", UiPlacement.Manual(10f, 20f, 320f, 240f));

        var applyCommand = new UiCommand(_ => { }, _ => true);
        var window = new UiWindow
        {
            Title = "Settings",
            Placement = UiPlacement.Persisted("settings", UiPlacement.AnchorTo(OverlayAnchor.TopRight, new Thickness(16f))),
            LayoutStore = store,
            MinimizeBehavior = MinimizeBehavior.CollapseToTitleBar,
            CanClose = true,
        };

        var grid = new Grid();
        grid.Columns.Add(new GridDefinition(GridLength.Pixel(120f)));
        grid.Columns.Add(new GridDefinition(GridLength.Star()));
        grid.Rows.Add(new GridDefinition(GridLength.Auto));
        grid.Rows.Add(new GridDefinition(GridLength.Auto));

        var comboBox = new UiComboBox { Placeholder = "Mode" };
        comboBox.Items.Add("Manual");
        comboBox.Items.Add("Auto");
        comboBox.SelectedIndex = 0;

        var tabs = new UiTabControl();
        tabs.Add("General", new TextBlock { Text = "General" });
        tabs.Add("Advanced", new ColorPicker { Value = ColorRgba.FromBytes(32, 96, 192, 255) });

        var stack = new StackPanel { Orientation = UiOrientation.Vertical, Spacing = 6f };
        stack.Children.Add(new UiLabel { Text = "Profile", Target = comboBox });
        stack.Children.Add(comboBox);
        stack.Children.Add(new UiTextBox { Placeholder = "Name", Text = "Default" });
        stack.Children.Add(new NumberBox { Minimum = 0d, Maximum = 10d, Value = 3d });
        stack.Children.Add(new UiCheckBox { Text = "Enabled", IsChecked = true });
        stack.Children.Add(new UiRadioButton { Text = "Primary", IsChecked = true });
        stack.Children.Add(new Slider { Minimum = 0f, Maximum = 100f, Value = 50f });
        stack.Children.Add(new UiProgressBar { Minimum = 0f, Maximum = 100f, Value = 75f });
        stack.Children.Add(new SegmentedControl { SelectedIndex = -1 });
        stack.Children.Add(tabs);
        stack.Children.Add(new UiButton { Text = "Apply", Command = applyCommand, CommandParameter = "profile" });

        var groupBox = new UiGroupBox { Header = "Controls", Content = stack };
        grid.Children.Add(groupBox);
        Grid.SetColumnSpan(groupBox, 2);

        var popup = new Popup { Owner = window, PlacementMode = UiPopupPlacementMode.OwnerAnchor };
        popup.Children.Add(new TextBlock { Text = "Popup content" });

        var menu = new UiMenu();
        menu.Items.Add(new UiMenuItem("Apply", applyCommand) { CommandParameter = "menu" });

        var contextMenu = new UiContextMenu { Owner = window, IsOpen = true };
        contextMenu.Items.Add(new UiMenuItem("Reset", UiCommand.FromAction(() => { })));

        var toolTip = new UiToolTip { Owner = comboBox, Text = "Choose a mode" };

        Canvas.SetLeft(window, 24f);
        Canvas.SetTop(window, 36f);
        window.Children.Add(menu);
        window.Children.Add(grid);

        var canvas = new Canvas();
        canvas.Children.Add(window);
        canvas.Children.Add(popup);
        canvas.Children.Add(contextMenu);
        canvas.Children.Add(toolTip);

        Assert.AreEqual(4, canvas.Children.Count);
        Assert.AreEqual("settings", window.Placement?.PersistenceKey);
        Assert.AreEqual("Manual", comboBox.SelectedItem);
        Assert.AreEqual("profile", ((UiButton)stack.Children[10]).CommandParameter);
        Assert.IsFalse(readability.Failures.Any());
    }

    private sealed class MemoryUiLayoutStore : IUiLayoutStore
    {
        private readonly Dictionary<string, UiPlacement> placements = [];

        public bool TryLoad(string key, out UiPlacement placement)
            => placements.TryGetValue(key, out placement);

        public void Save(string key, UiPlacement placement)
            => placements[key] = placement;
    }
}
