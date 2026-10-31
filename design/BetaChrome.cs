using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XD.Design;

public static class BetaChrome
{
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
        var menu = new Menu { Name = "TopMenu", Background = Brush(Color.FromRgb(244, 246, 249)), Padding = new Thickness(16, 3, 16, 3) };
        var toolbar = new WrapPanel { Margin = new Thickness(25, 0, 25, 10) };
        var state = new State { English = english, Menu = menu, Toolbar = toolbar }; states.Add(window, state);
        if (NameScope.GetNameScope(window) == null) NameScope.SetNameScope(window, new NameScope());
        window.RegisterName("TopMenu", menu);
        DockPanel.SetDock(menu, Dock.Top); shell.Children.Add(menu);
        var identity = new DockPanel { Margin = new Thickness(25, 15, 25, 13), LastChildFill = true };
        var badge = new TextBlock { Text = beta ? "BETA" : "XD SUITE", Tag = "XD.BrandBadge", FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.Bold, FontSize = 12, Foreground = Brush(beta ? Color.FromRgb(66, 81, 105) : Color.FromRgb(233, 249, 238)), VerticalAlignment = VerticalAlignment.Center };
        var badgeBox = new Border { Tag = "XD.BrandBadge", Child = badge, Background = Brush(beta ? Color.FromRgb(232, 235, 240) : Color.FromRgb(89, 77, 206)), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0) };
        DockPanel.SetDock(badgeBox, Dock.Right); identity.Children.Add(badgeBox);
        if (beta || window.Icon != null)
        {
            ImageSource artwork = window.Icon!;
            if (beta) { var uri = new Uri("pack://application:,,,/" + window.GetType().Assembly.GetName().Name + ";component/Assets/Beta.ico", UriKind.Absolute); artwork = BitmapFrame.Create(uri); window.Icon = artwork; }
            var image = new Image { Source = artwork, Width = 48, Height = 48, Margin = new Thickness(0, 0, 15, 0) };
            DockPanel.SetDock(image, Dock.Left); identity.Children.Add(image);
        }
        var title = new TextBlock { FontSize = 23, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var version = new TextBlock { Text = beta ? Version(window) : window.GetType().Assembly.GetName().Version!.ToString(3), FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = Brush(Color.FromRgb(104, 118, 138)), Margin = new Thickness(0, 5, 0, 0) };
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
        // Beta identity is carried by the icon. Keep the app's own window and control styles.
        window.Loaded += (_, _) => Refresh(window);
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
    sealed class MenuCommand(Action action, Func<bool> enabled) : ICommand
    {
        public bool CanExecute(object? parameter) => enabled();
        public void Execute(object? parameter) { if (enabled()) action(); }
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}
