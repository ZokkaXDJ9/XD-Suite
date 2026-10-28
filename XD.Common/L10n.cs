using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace XD.Common;

public enum UiLanguage { German, English }

public static class L10n
{
    private sealed record Entry(string De, string En);
    private sealed record Pattern(Regex German, Regex English, string De, string En, int Count);
    private static readonly Dictionary<string, string> forward = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> reverse = new(StringComparer.Ordinal);
    private static readonly List<Pattern> patterns = [];
    private static readonly string preference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XD", "language.txt");
    public static UiLanguage Current { get; private set; } = UiLanguage.German;
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Current == UiLanguage.German ? "de-DE" : "en-US");
    public static event EventHandler? Changed;

    static L10n()
    {
        using var input = typeof(L10n).Assembly.GetManifestResourceStream("XD.Common.translations.json")!;
        var entries = JsonSerializer.Deserialize<List<Entry>>(input, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var entry in entries)
        {
            if (forward.ContainsKey(entry.De)) throw new InvalidOperationException("Duplicate translation: " + entry.De);
            forward.Add(entry.De, entry.En);
            reverse.TryAdd(entry.En, entry.De);
            int count = Regex.Matches(entry.De, @"\{\d+\}").Select(m => int.Parse(m.Value[1..^1])).DefaultIfEmpty(-1).Max() + 1;
            if (count > 0) patterns.Add(new(ToRegex(entry.De), ToRegex(entry.En), entry.De, entry.En, count));
        }
    }

    public static void Initialize()
    {
        UiLanguage selected = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "de" ? UiLanguage.German : UiLanguage.English;
        try { if (File.Exists(preference)) Enum.TryParse(File.ReadAllText(preference), out selected); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Set(Enum.IsDefined(selected) ? selected : UiLanguage.German, false);
    }

    public static void Set(UiLanguage language, bool persist = true)
    {
        Current = language;
        var culture = CultureInfo.GetCultureInfo(language == UiLanguage.German ? "de-DE" : "en-US");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (persist)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preference)!);
            string temporary = preference + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, language.ToString());
            File.Move(temporary, preference, true);
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string text)
    {
        if (Current == UiLanguage.German) return ToGerman(text);
        if (forward.TryGetValue(text, out var translated)) return translated;
        foreach (var pattern in patterns)
        {
            var match = pattern.German.Match(text);
            if (match.Success) return Apply(pattern.En, match, pattern.Count);
        }
        return text;
    }

    internal static string ToGerman(string text)
    {
        if (reverse.TryGetValue(text, out var german)) return german;
        foreach (var pattern in patterns)
        {
            var match = pattern.English.Match(text);
            if (match.Success) return Apply(pattern.De, match, pattern.Count);
        }
        return text;
    }

    private static Regex ToRegex(string template)
    {
        string expression = Regex.Escape(template);
        expression = Regex.Replace(expression, @"\\\{(\d+)\}", m => "(?<p" + m.Groups[1].Value + ">.*?)");
        return new("\\A" + expression + "\\z", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
    private static string Apply(string template, Match match, int count) => string.Format(CultureInfo.CurrentCulture, template, Enumerable.Range(0, count).Select(i => (object)match.Groups["p" + i].Value).ToArray());

    // Track UI-owned text, never bound filenames, editable text, or user data. Both static XAML text
    // and changing progress/status text are translated; property descriptors preserve existing bindings.
    public static IDisposable Attach(Window window) => new WindowTranslations(window);

    private sealed class WindowTranslations : IDisposable
    {
        private sealed class Watched(DependencyObject target, DependencyProperty property, DependencyPropertyDescriptor descriptor)
        {
            public DependencyObject Target { get; } = target;
            public DependencyProperty Property { get; } = property;
            public DependencyPropertyDescriptor Descriptor { get; } = descriptor;
            public string Original = "";
            public bool Updating;
            public EventHandler Handler = null!;
        }
        private readonly List<Watched> watched = [];
        private readonly Window window;
        public WindowTranslations(Window window)
        {
            this.window = window;
            var visited = new HashSet<DependencyObject>();
            Visit(window);
            Changed += Refresh;
            window.Closed += Closed;

            void Visit(DependencyObject target)
            {
                if (!visited.Add(target)) return;
                if (target is TextBlock) Watch(target, TextBlock.TextProperty);
                if (target is ContentControl) Watch(target, ContentControl.ContentProperty);
                if (target is HeaderedItemsControl) Watch(target, HeaderedItemsControl.HeaderProperty);
                if (target is Window) Watch(target, Window.TitleProperty);
                if (target is FrameworkElement)
                {
                    Watch(target, FrameworkElement.ToolTipProperty);
                    Watch(target, System.Windows.Automation.AutomationProperties.NameProperty);
                }
                if (target is DataGrid grid) foreach (var column in grid.Columns) Watch(column, DataGridColumn.HeaderProperty);
                foreach (var child in LogicalTreeHelper.GetChildren(target).OfType<DependencyObject>()) Visit(child);
            }
        }
        private void Watch(DependencyObject target, DependencyProperty property)
        {
            if (BindingOperations.IsDataBound(target, property)) return;
            var descriptor = DependencyPropertyDescriptor.FromProperty(property, target.GetType());
            if (descriptor == null) return;
            var item = new Watched(target, property, descriptor);
            item.Handler = (_, _) => Read(item);
            descriptor.AddValueChanged(target, item.Handler);
            watched.Add(item);
            Read(item);
        }
        private static void Read(Watched item)
        {
            if (item.Updating || BindingOperations.IsDataBound(item.Target, item.Property) || item.Target.GetValue(item.Property) is not string text) return;
            item.Original = ToGerman(text);
            Write(item);
        }
        private static void Write(Watched item)
        {
            if (BindingOperations.IsDataBound(item.Target, item.Property) || item.Target.GetValue(item.Property) is not string) return;
            item.Updating = true;
            try { item.Target.SetCurrentValue(item.Property, T(item.Original)); }
            finally { item.Updating = false; }
        }
        private void Refresh(object? sender, EventArgs e) { foreach (var item in watched) Write(item); }
        private void Closed(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            Changed -= Refresh;
            window.Closed -= Closed;
            foreach (var item in watched) item.Descriptor.RemoveValueChanged(item.Target, item.Handler);
            watched.Clear();
        }
    }
}

public static class LocalizedDialog
{
    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        => ShowCore(null, message, caption, buttons, defaultResult);
    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        => ShowCore(owner, message, caption, buttons, defaultResult);
    private static MessageBoxResult ShowCore(Window? owner, string message, string caption, MessageBoxButton buttons, MessageBoxResult defaultResult)
    {
        var dialog = new Window { Title = L10n.T(caption), Width = 550, SizeToContent = SizeToContent.Height, MaxHeight = SystemParameters.WorkArea.Height * .85, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen, ShowInTaskbar = owner?.IsVisible != true, FontFamily = new FontFamily("Segoe UI"), FontSize = 14, Background = Brushes.White };
        if (owner?.IsVisible == true) dialog.Owner = owner;
        var root = new DockPanel { Margin = new Thickness(26) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var text = new TextBlock { Text = L10n.T(message), TextWrapping = TextWrapping.Wrap, LineHeight = 23, Foreground = new SolidColorBrush(Color.FromRgb(34, 50, 47)) };
        root.Children.Add(new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height * .65 });
        dialog.Content = root;
        MessageBoxResult result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
        var choices = buttons switch { MessageBoxButton.YesNo => new[] { MessageBoxResult.Yes, MessageBoxResult.No }, MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel], MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel], _ => [MessageBoxResult.OK] };
        foreach (var choice in choices)
        {
            string label = choice switch { MessageBoxResult.Yes => "Ja", MessageBoxResult.No => "Nein", MessageBoxResult.Cancel => "Abbrechen", _ => "OK" };
            var button = new Button { Content = L10n.T(label), MinWidth = 88, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(8, 0, 0, 0), IsDefault = choice == defaultResult || defaultResult == MessageBoxResult.None && choice == choices[0], IsCancel = choice == MessageBoxResult.Cancel || choice == MessageBoxResult.No };
            button.Click += (_, _) => { result = choice; dialog.DialogResult = true; };
            actions.Children.Add(button);
        }
        dialog.ShowDialog();
        return result;
    }
}
