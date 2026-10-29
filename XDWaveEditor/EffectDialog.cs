using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XDWaveEditor;

internal sealed class ParameterInput : Grid
{
    private readonly EffectParameter definition;
    internal TextBox Entry { get; } = new() { MinWidth = 90, Padding = new(7, 5, 7, 5), VerticalContentAlignment = VerticalAlignment.Center };
    internal Slider Slider { get; }
    internal ParameterInput(EffectParameter definition)
    {
        this.definition = definition; Margin = new(0, 0, 0, 17);
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new() { Height = GridLength.Auto });
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new TextBlock { Text = Texts.Current[definition.Key] + (definition.Unit.Length > 0 ? " (" + definition.Unit + ")" : ""), Margin = new(0, 0, 0, 7) }; SetColumnSpan(label, 2); Children.Add(label);
        Slider = new() { Minimum = definition.Minimum, Maximum = definition.Maximum, Value = definition.Default, TickFrequency = definition.Step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 16, 0) };
        SetRow(Slider, 1); Children.Add(Slider); SetRow(Entry, 1); SetColumn(Entry, 1); Children.Add(Entry);
        Slider.ValueChanged += (_, _) => { if (!Entry.IsKeyboardFocused) Entry.Text = Slider.Value.ToString("0.##", Texts.Current.Culture); };
        Entry.TextChanged += (_, _) =>
        {
            bool valid = TryValue(out var value);
            Entry.BorderBrush = valid ? Brushes.Gray : Brushes.Firebrick;
            if (valid && Entry.IsKeyboardFocused) Slider.Value = value;
        };
        Value = definition.Default;
        System.Windows.Automation.AutomationProperties.SetName(Entry, Texts.Current[definition.Key]);
        System.Windows.Automation.AutomationProperties.SetName(Slider, Texts.Current[definition.Key]);
    }
    internal double Value { get => TryValue(out var value) ? value : double.NaN; set { Slider.Value = value; Entry.Text = value.ToString("0.##", Texts.Current.Culture); } }
    internal bool TryValue(out double value) => double.TryParse(Entry.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value >= definition.Minimum && value <= definition.Maximum;
}

internal sealed class EffectDialog : Window
{
    private readonly AudioEngine engine;
    private readonly AudioDocument document;
    private readonly StackPanel parameters = new();
    private readonly TextBlock help = new() { Foreground = Brushes.SlateGray, Margin = new(0, 8, 0, 18), LineHeight = 20 };
    private readonly TextBlock scope = new() { Margin = new(0, 0, 0, 18), FontWeight = FontWeights.SemiBold };
    private readonly TextBlock status = new() { Foreground = Brushes.Firebrick, Margin = new(0, 12, 0, 10) };
    private readonly CheckBox allowClipping = new() { Margin = new(0, 3, 0, 8) };
    private readonly CheckBox removeDc = new() { Margin = new(0, 3, 0, 8), IsChecked = true };
    private readonly ProgressBar progress = new() { Height = 4, IsIndeterminate = true, Visibility = Visibility.Collapsed, Margin = new(0, 0, 0, 12) };
    private readonly Button apply = new();
    private readonly Button defaults = new();
    private CancellationTokenSource? operation;
    private bool busy, closeRequested;
    internal ComboBox Selector { get; } = new() { DisplayMemberPath = "Name", Margin = new(0, 8, 0, 0), Padding = new(10, 8, 10, 8) };
    internal List<ParameterInput> Inputs { get; } = [];
    internal EffectKind SelectedKind => ((EffectDefinition)Selector.SelectedItem).Kind;
    internal Task<bool>? Pending { get; private set; }
    internal Button ApplyButton => apply;
    internal string ResultText => status.Text;
    internal EffectDialog(AudioEngine engine, AudioDocument document, EffectKind initial = EffectKind.Normalize)
    {
        this.engine = engine; this.document = document;
        Title = Texts.Current["Tools"]; Width = 650; Height = 660; MinWidth = 540; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var root = new DockPanel { Margin = new(26) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(status); footer.Children.Add(progress);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        defaults.Content = Texts.Current["Defaults"];
        defaults.Click += (_, _) => { if (!busy) { removeDc.IsChecked = true; RefreshEffect(); } };
        var cancel = new Button { Content = Texts.Current["Cancel"], IsCancel = true };
        cancel.Click += (_, _) => { if (busy) { closeRequested = true; operation?.Cancel(); } else Close(); };
        apply.Content = Texts.Current["ApplyEffect"]; apply.Style = (Style)Application.Current.FindResource("Accent"); apply.IsDefault = true;
        apply.Click += async (_, _) => { Pending = Apply(); await Pending; };
        actions.Children.Add(defaults); actions.Children.Add(cancel); actions.Children.Add(apply); footer.Children.Add(actions);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(new TextBlock { Text = Path.GetFileName(document.Original), FontWeight = FontWeights.SemiBold, FontSize = 18 });
        body.Children.Add(Selector); body.Children.Add(help); body.Children.Add(scope); body.Children.Add(parameters);
        allowClipping.Content = Texts.Current["AllowClipping"]; removeDc.Content = Texts.Current["RemoveDc"];
        body.Children.Add(allowClipping); body.Children.Add(removeDc);
        Content = root;
        Selector.ItemsSource = Effects.ForRate(document.Source.Rate);
        Selector.SelectionChanged += (_, _) => RefreshEffect();
        Selector.SelectedItem = Selector.Items.Cast<EffectDefinition>().Single(d => d.Kind == initial);
        Closing += (_, e) => { if (busy) { closeRequested = true; operation?.Cancel(); e.Cancel = true; } };
    }
    private void RefreshEffect()
    {
        if (Selector.SelectedItem is not EffectDefinition definition) return;
        help.Text = definition.Help; parameters.Children.Clear(); Inputs.Clear(); status.Text = "";
        foreach (var parameter in definition.Parameters) { var input = new ParameterInput(parameter); Inputs.Add(input); parameters.Children.Add(input); }
        allowClipping.Visibility = definition.CanClip ? Visibility.Visible : Visibility.Collapsed; allowClipping.IsChecked = false;
        removeDc.Visibility = definition.Kind == EffectKind.Normalize ? Visibility.Visible : Visibility.Collapsed;
        scope.Text = definition.Kind == EffectKind.InsertSilence ? Texts.Current["InsertAtCursor"] + " " + Texts.Time(document.Cursor, document.Source.Rate) : document.Selection.Length == 0 ? Texts.Current["WholeAudio"] : Texts.Current["SelectedAudio"] + " " + Texts.Time(document.Selection.Start, document.Source.Rate) + " — " + Texts.Time(document.Selection.End, document.Source.Rate);
        apply.IsEnabled = document.Frames > 0 || definition.Kind == EffectKind.InsertSilence;
    }
    internal async Task<bool> Apply()
    {
        if (busy || Inputs.Any(p => !p.TryValue(out _))) { status.Text = Texts.Current["InvalidEffect"]; return false; }
        var settings = new EffectSettings(SelectedKind, Inputs.Select(p => p.Value).ToArray(), allowClipping.IsChecked == true, removeDc.IsChecked == true);
        FrameRange range = SelectedKind == EffectKind.InsertSilence ? new(document.Cursor, document.Cursor) : document.Selection.Length > 0 ? document.Selection : new(0, document.Frames);
        busy = true; operation = new(); apply.IsEnabled = false; Selector.IsEnabled = parameters.IsEnabled = allowClipping.IsEnabled = removeDc.IsEnabled = defaults.IsEnabled = false;
        progress.Visibility = Visibility.Visible; status.Text = Texts.Current["Processing"];
        bool success = false;
        try { await engine.ApplyEffect(document, range, settings, operation.Token); success = true; }
        catch (OperationCanceledException) { status.Text = Texts.Current["Cancelled"]; }
        catch (Exception error) { status.Text = Texts.Current.Error(error); }
        finally
        {
            operation.Dispose(); operation = null; busy = false;
            Selector.IsEnabled = parameters.IsEnabled = allowClipping.IsEnabled = removeDc.IsEnabled = defaults.IsEnabled = true;
            progress.Visibility = Visibility.Collapsed; apply.IsEnabled = true;
        }
        if (success || closeRequested) Close();
        return success;
    }
}
