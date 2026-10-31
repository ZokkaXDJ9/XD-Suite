using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Preview;

internal sealed class Ui
{
    readonly List<Action> translations = new();
    readonly string preference;
    public bool English { get; private set; }
    public string T(string de, string en) => English ? en : de;
    public Ui(string folder, string[] args)
    {
        preference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), folder, "language.txt");
        English = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName != "de";
        if (File.Exists(preference)) English = File.ReadAllText(preference).Trim() == "en";
        if (args.Contains("--english")) English = true;
        if (args.Contains("--german")) English = false;
    }
    public void Local(Action update) { translations.Add(update); update(); }
    public ComboBox Language()
    {
        var box = new ComboBox { ItemsSource = new[] { "Deutsch", "English" }, SelectedIndex = English ? 1 : 0, Width = 125, Margin = new(8, 0, 0, 0) };
        box.SelectionChanged += (_, _) => { English = box.SelectedIndex == 1; foreach (var update in translations) update(); try { Directory.CreateDirectory(Path.GetDirectoryName(preference)!); File.WriteAllText(preference, English ? "en" : "de"); } catch (IOException) { } };
        return box;
    }
    public TextBlock Text(string de, string en, double size = 13, bool bold = false)
    {
        var text = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 5), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        Local(() => text.Text = T(de, en)); return text;
    }
    public Button Button(string de, string en, Action action, bool primary = false)
    {
        var button = new Button { Margin = new(0, 3, 8, 3), Padding = new(13, 9, 13, 9), MinHeight = 36, BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromRgb(205, 211, 225)), Background = primary ? new SolidColorBrush(Color.FromRgb(76, 64, 166)) : Brushes.White, Foreground = primary ? Brushes.White : Brushes.Black };
        Local(() => button.Content = T(de, en)); button.Click += (_, _) => action(); return button;
    }
    public static Border Card(UIElement child, Thickness? padding = null) => new() { Child = child, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 236)), BorderThickness = new(1), CornerRadius = new(10), Padding = padding ?? new Thickness(16), Margin = new(0, 0, 0, 12) };
    public static TextBox Input(string name = "")
    {
        var box = new TextBox { Padding = new(7), MinHeight = 31, Margin = new(0, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(box, name); return box;
    }
    public static DataGrid Grid() => new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, RowHeight = 34, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(235, 237, 244)), BorderThickness = new(0), Background = Brushes.White, SelectionMode = DataGridSelectionMode.Extended };
    public void Column(DataGrid grid, string property, string de, string en, double width = 1, bool editable = false)
    {
        var col = new DataGridTextColumn { Binding = new System.Windows.Data.Binding(property), Width = new DataGridLength(width, DataGridLengthUnitType.Star), IsReadOnly = !editable };
        Local(() => col.Header = T(de, en)); grid.Columns.Add(col);
    }
    public bool Confirm(Window window, string de, string en) => MessageBox.Show(window, T(de, en), window.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public void Error(Window window, Exception error) => MessageBox.Show(window, error.Message, T("Vorgang fehlgeschlagen", "Operation failed"), MessageBoxButton.OK, MessageBoxImage.Error);
    public static void Style(Window window)
    {
        window.Width = 1200; window.Height = 820; window.MinWidth = 960; window.MinHeight = 650;
        window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 13;
        window.Background = new SolidColorBrush(Color.FromRgb(244, 245, 250)); window.Foreground = new SolidColorBrush(Color.FromRgb(34, 40, 61)); window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/App.ico"));
    }
}
