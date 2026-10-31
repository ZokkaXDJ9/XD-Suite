using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XD.Design;

public static class BetaChrome
{
    public static readonly Color Canvas = Color.FromRgb(8, 29, 19);
    public static readonly Color Surface = Color.FromRgb(16, 43, 29);
    public static readonly Color Raised = Color.FromRgb(24, 57, 39);
    public static readonly Color Line = Color.FromRgb(46, 88, 61);
    public static readonly Color Ink = Color.FromRgb(233, 249, 238);
    public static readonly Color Muted = Color.FromRgb(163, 198, 174);
    public static readonly Color CodeGreen = Color.FromRgb(57, 255, 20);
    static SolidColorBrush Brush(Color color) { var b = new SolidColorBrush(color); b.Freeze(); return b; }
    static readonly ConditionalWeakTable<Window, State> states = new();
    sealed class State
    {
        public required Func<bool> English;
        public readonly List<Action> Labels = new();
        public required Menu Menu;
        public required WrapPanel Toolbar;
    }
    public static string Version(Window window) => window.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? window.GetType().Assembly.GetName().Version!.ToString(3);
    public static void Attach(Window window, Func<bool> english, Action<bool>? language = null, bool beta = true)
    {
        if (states.TryGetValue(window, out _)) return;
        var body = (UIElement)window.Content;
        window.Content = null;
        var shell = new DockPanel { LastChildFill = true };
        var menu = new Menu { Name = "TopMenu", Background = Brush(beta ? Surface : Color.FromRgb(244, 246, 249)), Padding = new Thickness(16, 3, 16, 3) };
        var toolbar = new WrapPanel { Margin = new Thickness(25, 0, 25, 10) };
        var state = new State { English = english, Menu = menu, Toolbar = toolbar }; states.Add(window, state);
        if (NameScope.GetNameScope(window) == null) NameScope.SetNameScope(window, new NameScope());
        window.RegisterName("TopMenu", menu);
        DockPanel.SetDock(menu, Dock.Top); shell.Children.Add(menu);
        var identity = new DockPanel { Margin = new Thickness(25, 15, 25, 13), LastChildFill = true };
        var badge = new TextBlock { Text = beta ? "BETA" : "XD SUITE", Tag = "XD.BrandBadge", FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.Bold, FontSize = 12, Foreground = Brush(beta ? Canvas : Ink), VerticalAlignment = VerticalAlignment.Center };
        var badgeBox = new Border { Tag = "XD.BrandBadge", Child = badge, Background = Brush(beta ? CodeGreen : Color.FromRgb(89, 77, 206)), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0) };
        DockPanel.SetDock(badgeBox, Dock.Right); identity.Children.Add(badgeBox);
        if (beta) window.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(DarkControls));
        if (beta || window.Icon != null)
        {
            ImageSource artwork = window.Icon!;
            if (beta) { var uri = new Uri("pack://application:,,,/" + window.GetType().Assembly.GetName().Name + ";component/Assets/Beta.ico", UriKind.Absolute); artwork = BitmapFrame.Create(uri); window.Icon = artwork; }
            var image = new Image { Source = artwork, Width = 48, Height = 48, Margin = new Thickness(0, 0, 15, 0) };
            DockPanel.SetDock(image, Dock.Left); identity.Children.Add(image);
        }
        var title = new TextBlock { FontSize = 23, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var version = new TextBlock { Text = beta ? Version(window) : window.GetType().Assembly.GetName().Version!.ToString(3), FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = Brush(beta ? Muted : Color.FromRgb(104, 118, 138)), Margin = new Thickness(0, 5, 0, 0) };
        state.Labels.Add(() => title.Text = window.Title);
        var names = new StackPanel(); names.Children.Add(title); names.Children.Add(version); identity.Children.Add(names);
        DockPanel.SetDock(identity, Dock.Top); shell.Children.Add(identity);
        DockPanel.SetDock(toolbar, Dock.Top); shell.Children.Add(toolbar);
        shell.Children.Add(body); window.Content = shell;
        window.Height = Math.Max(window.Height, 900);
        if (language != null)
        {
            var languages = Group(window, "_Sprache", "_Language");
            languages.Tag = "XD.Language";
            Add(window, languages, "Deutsch", "Deutsch", () => { language(false); Refresh(window); });
            Add(window, languages, "English", "English", () => { language(true); Refresh(window); });
        }
        window.Loaded += (_, _) => { if (beta) Theme(window); Refresh(window); };
        if (beta)
        {
            window.Background = Brush(Canvas); window.Foreground = Brush(Ink);
            window.Resources[SystemColors.HighlightBrushKey] = Brush(Color.FromRgb(37, 96, 58));
            window.Resources[SystemColors.HighlightTextBrushKey] = Brush(Ink);
            window.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = Brush(Raised);
            window.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brush(Ink);
            window.Resources[SystemColors.WindowBrushKey] = Brush(Surface);
            window.Resources[SystemColors.WindowTextBrushKey] = Brush(Ink);
            window.Resources[SystemColors.ControlBrushKey] = Brush(Raised);
            window.Resources[SystemColors.ControlTextBrushKey] = Brush(Ink);
            window.Resources[SystemColors.MenuBrushKey] = Brush(Surface);
            window.Resources[SystemColors.MenuTextBrushKey] = Brush(Ink);
            window.Resources[SystemColors.GrayTextBrushKey] = Brush(Muted);
        }
        Refresh(window);
    }
    public static MenuItem Group(Window window, string de, string en)
    {
        var state = states.GetValue(window, _ => throw new InvalidOperationException("Attach the menu first."));
        var item = new MenuItem { Padding = new Thickness(12, 7, 12, 7) };
        state.Labels.Add(() => item.Header = state.English() ? en : de);
        MenuItem? language = null; foreach (var entry in state.Menu.Items) if (entry is MenuItem m && Equals(m.Tag, "XD.Language")) language = m;
        if (language == null) state.Menu.Items.Add(item); else state.Menu.Items.Insert(state.Menu.Items.IndexOf(language), item);
        Refresh(window); return item;
    }
    public static MenuItem Add(Window window, MenuItem parent, string de, string en, Action action, UIElement? enabledBy = null, KeyGesture? gesture = null)
    {
        var state = states.GetValue(window, _ => throw new InvalidOperationException());
        var item = new MenuItem(); state.Labels.Add(() => item.Header = state.English() ? en : de);
        var command = new MenuCommand(action, () => enabledBy == null || enabledBy.IsEnabled && enabledBy.IsVisible);
        item.Command = command;
        if (enabledBy != null) item.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(UIElement.IsEnabled)) { Source = enabledBy });
        if (gesture != null) { item.InputGestureText = gesture.GetDisplayStringForCulture(System.Globalization.CultureInfo.InvariantCulture); window.InputBindings.Add(new KeyBinding(command, gesture)); }
        parent.Items.Add(item); Refresh(window); return item;
    }
    public static MenuItem Button(Window window, MenuItem parent, string de, string en, Button button, KeyGesture? gesture = null) =>
        Add(window, parent, de, en, () => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)), button, gesture);
    public static MenuItem Button(Window window, MenuItem parent, Button button)
    {
        var item = new MenuItem { Command = new MenuCommand(() => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)), () => button.IsEnabled) };
        item.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(UIElement.IsEnabled)) { Source = button });
        item.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding(nameof(ContentControl.Content)) { Source = button });
        parent.Items.Add(item);
        return item;
    }
    public static void MoveLanguages(Window window, Menu oldMenu)
    {
        if (!states.TryGetValue(window, out var state)) return;
        var items = new List<object>(); foreach (var item in oldMenu.Items) items.Add(item);
        oldMenu.Items.Clear(); oldMenu.Visibility = Visibility.Collapsed;
        foreach (var item in items) state.Menu.Items.Add(item);
    }
    public static void MoveLanguages(Window window, MenuItem child)
    {
        DependencyObject? current = child;
        while (current != null && current is not Menu) current = LogicalTreeHelper.GetParent(current);
        if (current is Menu menu) MoveLanguages(window, menu);
    }
    public static void Toolbar(Window window, FrameworkElement element)
    {
        if (element.Parent is Panel panel) panel.Children.Remove(element);
        else if (element.Parent is Decorator border) border.Child = null;
        states.GetValue(window, _ => throw new InvalidOperationException()).Toolbar.Children.Add(element);
    }
    public static void Refresh(Window window)
    {
        if (!states.TryGetValue(window, out var state)) return;
        foreach (var label in state.Labels) label();
        foreach (var item in state.Menu.Items) if (item is MenuItem m) { m.Foreground = window.Foreground; }
    }
    public static void Theme(Window window)
    {
        window.Background = Brush(Canvas); window.Foreground = Brush(Ink);
        void Visit(DependencyObject node)
        {
            if (node is FrameworkElement marked && Equals(marked.Tag, "XD.BrandBadge")) return;
            if (node is Menu menu) { menu.Background = Brush(Surface); menu.Foreground = Brush(Ink); }
            if (node is Control control)
            {
                bool primary = control is Button && control.Background is SolidColorBrush original && original.Color.A > 0 && Saturation(original.Color) > .3;
                if (control.Background is SolidColorBrush background && background.Color.A > 0) control.SetCurrentValue(Control.BackgroundProperty, Brush(primary ? CodeGreen : Surface));
                control.SetCurrentValue(Control.ForegroundProperty, Brush(primary ? Canvas : Ink));
                if (control.BorderBrush is SolidColorBrush line && line.Color.A > 0) control.SetCurrentValue(Control.BorderBrushProperty, Brush(Line));
            }
            // Templates inherit the button's colors. Repainting their child Border would undo the accent.
            if (node is Button button) { button.Template = (ControlTemplate)window.Resources["XD.ButtonTemplate"]; return; }
            if (node is ComboBox combo) { combo.Template = (ControlTemplate)window.Resources["XD.ComboTemplate"]; return; }
            if (node is ScrollBar scroll) { scroll.Template = (ControlTemplate)window.Resources["XD.ScrollTemplate"]; return; }
            if (node is Border border)
            {
                if (border.Background is SolidColorBrush b && b.Color.A > 0) border.Background = Brush(Surface);
                if (border.BorderBrush is SolidColorBrush b2 && b2.Color.A > 0) border.BorderBrush = Brush(Line);
            }
            if (node is Panel panel && panel.Background is SolidColorBrush pb && pb.Color.A > 0) panel.Background = Brush(Surface);
            if (node is TextBlock text && text.Foreground is SolidColorBrush fg && fg.Color.A > 0)
            {
                double light = Luma(fg.Color);
                if (light < .3) text.SetCurrentValue(TextBlock.ForegroundProperty, Brush(Ink));
                else if (light < .8) text.SetCurrentValue(TextBlock.ForegroundProperty, Brush(Muted));
            }
            if (node is TextBox box) { box.Background = Brush(Canvas); box.Foreground = Brush(Ink); box.CaretBrush = Brush(CodeGreen); box.SelectionBrush = Brush(Line); }
            if (node is DataGrid grid)
            {
                grid.Background = Brush(Surface); grid.RowBackground = Brush(Surface); grid.AlternatingRowBackground = Brush(Raised); grid.Foreground = Brush(Ink); grid.HorizontalGridLinesBrush = Brush(Line);
                var header = new Style(typeof(DataGridColumnHeader)); header.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Raised))); header.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Ink))); header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 8, 9, 8))); grid.ColumnHeaderStyle = header;
                var row = new Style(typeof(DataGridRow)); row.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Ink))); grid.RowStyle = row;
            }
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) Visit(VisualTreeHelper.GetChild(node, i));
        }
        Visit(window);
        if (states.TryGetValue(window, out var state))
        {
            state.Menu.Background = Brush(Canvas);
            foreach (var item in state.Menu.Items) ThemeMenu(item as MenuItem);
        }
    }
    static void ThemeMenu(MenuItem? item)
    {
        if (item == null) return;
        item.Foreground = Brush(Ink); item.Background = Brush(Surface);
        foreach (var child in item.Items) ThemeMenu(child as MenuItem);
    }
    static double Luma(Color c) => (c.R * .2126 + c.G * .7152 + c.B * .0722) / 255d;
    static double Saturation(Color c) => (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B))) / 255d;
    const string DarkControls = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <ControlTemplate x:Key="XD.ButtonTemplate" TargetType="Button"><Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="5" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#39FF14"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#39FF14"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Frame" Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate>
 <ControlTemplate x:Key="XD.ComboTemplate" TargetType="ComboBox">
  <Grid>
   <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" ClickMode="Press">
    <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="Frame" Background="#102B1D" BorderBrush="#2E583D" BorderThickness="1" CornerRadius="4"><Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="28"/></Grid.ColumnDefinitions><Path Grid.Column="1" Data="M0,0 L4,4 L8,0" Stroke="#A3C6AE" StrokeThickness="1.5" HorizontalAlignment="Center" VerticalAlignment="Center"/></Grid></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#39FF14"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter Margin="9,7,30,7" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}" VerticalAlignment="Center"/>
   <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="Slide"><Border Background="#102B1D" BorderBrush="#39FF14" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="320"><ScrollViewer CanContentScroll="True"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup>
  </Grid>
  <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key="XD.ScrollTemplate" TargetType="ScrollBar"><Grid Background="#081D13"><Track x:Name="PART_Track" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#2E583D" CornerRadius="4" Margin="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0"/></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/></Trigger></ControlTemplate.Triggers></ControlTemplate>
</ResourceDictionary>
""";
    sealed class MenuCommand(Action action, Func<bool> enabled) : ICommand
    {
        public bool CanExecute(object? parameter) => enabled();
        public void Execute(object? parameter) { if (enabled()) action(); }
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}
