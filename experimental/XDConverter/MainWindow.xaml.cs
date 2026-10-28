using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XDConverter;

public partial class MainWindow : Window
{
    private readonly AudioEngine engine;
    private readonly PreviewSettings settings;
    private readonly bool preview;
    private readonly ObservableCollection<AudioRow> rows = [];
    private CancellationTokenSource? cancellation;
    private bool ready, busy, closeAfterCancel;
    private string statusKey = "Ready";
    private string? detailKey;
    private object[] detailArgs = [];
    private Exception? lastError;
    private static Texts T => Texts.Current;
    private sealed record Choice(string Label, int Value) { public override string ToString() => Label; }
    private OutputFormat Format => FormatCombo.SelectedItem as OutputFormat ?? OutputFormat.All[0];
    private static int Value(ComboBox combo) => (combo.SelectedItem as Choice)?.Value ?? 0;

    public MainWindow(AudioEngine engine, PreviewSettings settings, bool preview = false)
    {
        this.engine = engine; this.settings = settings; this.preview = preview;
        InitializeComponent();
        if (preview) { ShowActivated = false; ShowInTaskbar = false; }
        FilesGrid.ItemsSource = rows;
        FormatCombo.ItemsSource = OutputFormat.All;
        FormatCombo.SelectedItem = OutputFormat.All.FirstOrDefault(f => f.Id == settings.Format) ?? OutputFormat.All[0];
        OutputBox.Text = settings.OutputDirectory;
        TagsCheck.IsChecked = settings.Tags;
        ready = true; RefreshLanguage(true);
        Texts.Current.Changed += Language_Changed;
        Closed += (_, _) => Texts.Current.Changed -= Language_Changed;
        Loaded += async (_, _) =>
        {
            if (preview) return;
            SetBusy(true);
            try { await engine.Check(cancellation!.Token); SetStatus("Ready"); }
            catch (OperationCanceledException) { SetStatus("Canceled"); }
            catch (Exception error) { ShowError(error); }
            finally { SetBusy(false); }
        };
    }
    private void Language_Changed(object? sender, EventArgs args) => RefreshLanguage(false);
    private void RefreshLanguage(bool initial)
    {
        GermanMenu.IsChecked = !T.English; EnglishMenu.IsChecked = T.English;
        NameColumn.Header = T["Name"]; LengthColumn.Header = T["Duration"]; SourceColumn.Header = T["Source"]; StateColumn.Header = T["Status"];
        int rate = initial ? settings.Rate : Value(RateCombo), channels = initial ? settings.Channels : Value(ChannelCombo), quality = initial ? settings.Quality : Value(QualityCombo);
        SetChoices(RateCombo, [new(T["Automatic"], 0), new(T.English ? "44,100 Hz" : "44.100 Hz", 44100), new(T.English ? "48,000 Hz" : "48.000 Hz", 48000)], rate);
        SetChoices(ChannelCombo, [new(T["Automatic"], 0), new(T["Mono"], 1), new(T["Stereo"], 2)], channels);
        RefreshQuality(quality);
        foreach (var row in rows) row.Refresh();
        RefreshRows(); RefreshStatus();
    }
    private static void SetChoices(ComboBox combo, Choice[] choices, int selected)
    {
        combo.ItemsSource = choices; combo.SelectedItem = choices.FirstOrDefault(c => c.Value == selected) ?? choices[0];
    }
    private void RefreshQuality(int previous)
    {
        Choice[] choices = Format.Id switch
        {
            "WAV" => [new(T["Depth16"], 16), new(T["Depth24"], 24), new(T["Float"], 32)],
            "FLAC" => [new(T["Default"], 5)],
            _ => [new("128 kbit/s", 128), new("192 kbit/s", 192), new("256 kbit/s", 256), new("320 kbit/s", 320)]
        };
        SetChoices(QualityCombo, choices, choices.Any(c => c.Value == previous) ? previous : Format.Id is "WAV" or "FLAC" ? choices[0].Value : 192);
        QualityCombo.IsEnabled = !busy && Format.Id != "FLAC";
    }
    private void Format_Changed(object sender, SelectionChangedEventArgs e) { if (ready) RefreshQuality(Value(QualityCombo)); }
    private void German_Click(object sender, RoutedEventArgs e) => Texts.Current.Set(false);
    private void English_Click(object sender, RoutedEventArgs e) => Texts.Current.Set(true);
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = T["ChooseAudio"], Filter = T["AudioFilter"], Multiselect = true };
        if (dialog.ShowDialog(this) == true) await AddPaths(dialog.FileNames);
    }
    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T["ChooseFolder"], Multiselect = true };
        if (dialog.ShowDialog(this) == true) await AddPaths(dialog.FolderNames);
    }
    private void Files_DragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void Files_Drop(object sender, DragEventArgs e) { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] paths) { e.Handled = true; await AddPaths(paths); } }
    private async Task AddPaths(IEnumerable<string> paths)
    {
        if (busy) return;
        SetBusy(true); SetStatus("Checking"); ProgressBar.IsIndeterminate = true;
        int added = 0, skipped = 0;
        try
        {
            var token = cancellation!.Token;
            var files = await Task.Run(() =>
            {
                var result = new List<string>();
                foreach (string path in paths)
                {
                    IEnumerable<string> entries = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }) : [path];
                    foreach (string item in entries) { token.ThrowIfCancellationRequested(); result.Add(item); if (result.Count > 10000) throw new ConversionError("FolderLimit"); }
                }
                return result;
            }, token);
            var existing = rows.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();
                if (!AudioEngine.Extensions.Contains(Path.GetExtension(file)) || !existing.Add(Path.GetFullPath(file))) { skipped++; continue; }
                var row = new AudioRow { Path = Path.GetFullPath(file) };
                try { row.Info = await engine.Inspect(row.Path, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) { row.State = "Failed"; row.Failure = error; }
                rows.Add(row); added++; RefreshRows();
            }
            SetStatus("Ready", "Added", added, skipped);
        }
        catch (OperationCanceledException) { SetStatus("Canceled", "Added", added, skipped); }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); }
    }
    private void Remove_Click(object sender, RoutedEventArgs e) { foreach (var row in FilesGrid.SelectedItems.Cast<AudioRow>().ToArray()) rows.Remove(row); RefreshRows(); }
    private void Clear_Click(object sender, RoutedEventArgs e) { rows.Clear(); SetStatus("Ready"); RefreshRows(); }
    private void RefreshRows()
    {
        CountLabel.Text = T.F("Count", rows.Count);
        EmptyPanel.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StartButton.IsEnabled = !busy && rows.Any(r => r.Info != null);
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T["ChooseOutput"] };
        if (Directory.Exists(OutputBox.Text)) dialog.InitialDirectory = OutputBox.Text;
        if (dialog.ShowDialog(this) == true) OutputBox.Text = dialog.FolderName;
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (string.IsNullOrWhiteSpace(OutputBox.Text)) { ShowError(new ConversionError("OutputRequired")); return; }
        var options = new ConvertOptions(Format, OutputBox.Text.Trim(), Value(QualityCombo), Value(RateCombo), Value(ChannelCombo), TagsCheck.IsChecked == true);
        var pending = rows.Where(r => r.Info != null).ToArray();
        SetBusy(true); ProgressBar.IsIndeterminate = false; ProgressBar.Value = 0;
        int success = 0, failed = 0, canceled = 0;
        try
        {
            for (int i = 0; i < pending.Length; i++)
            {
                var row = pending[i];
                if (cancellation!.IsCancellationRequested) { row.State = "Canceled"; row.Refresh(); canceled++; continue; }
                row.State = "Converting"; row.Failure = null; row.Error = ""; row.Output = ""; row.Refresh();
                SetStatus("Converting", "Current", i + 1, pending.Length, row.Name);
                FilesGrid.ScrollIntoView(row);
                int index = i;
                var progress = new Progress<double>(p => ProgressBar.Value = (index + p) * 100d / pending.Length);
                try
                {
                    row.Output = await engine.Convert(row.Info!, options, p => ((IProgress<double>)progress).Report(p), cancellation.Token);
                    row.State = "Done"; success++;
                }
                catch (OperationCanceledException) { row.State = "Canceled"; canceled++; }
                catch (Exception error) { row.State = "Failed"; row.Failure = error; App.Log(error); failed++; }
                row.Refresh();
            }
            SetStatus(canceled > 0 ? "Canceled" : failed > 0 ? "Failed" : "Done", "Summary", success, failed, canceled);
            if (canceled == 0) ProgressBar.Value = 100;
        }
        finally { SetBusy(false); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { cancellation?.Cancel(); CancelButton.IsEnabled = false; SetStatus("Canceling"); }
    private void SetBusy(bool value)
    {
        busy = value;
        if (value) cancellation = new(); else { cancellation?.Dispose(); cancellation = null; }
        OptionsPanel.IsEnabled = AddButton.IsEnabled = FolderButton.IsEnabled = RemoveButton.IsEnabled = ClearButton.IsEnabled = !value;
        DestinationPanel.IsEnabled = !value;
        if (!value) QualityCombo.IsEnabled = Format.Id != "FLAC";
        CancelButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; CancelButton.IsEnabled = true;
        StartButton.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        ProgressBar.Visibility = value || ProgressBar.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshRows();
        if (!value && closeAfterCancel) Close();
    }
    private void SetStatus(string title, string? detail = null, params object[] args) { statusKey = title; detailKey = detail; detailArgs = args; lastError = null; RefreshStatus(); }
    private void RefreshStatus() { StatusTitle.Text = T[statusKey]; StatusText.Text = lastError != null ? T.ErrorText(lastError) : detailKey != null ? T.F(detailKey, detailArgs) : T["ReadyHint"]; }
    private void ShowError(Exception error) { statusKey = "Error"; lastError = error; App.Log(error); RefreshStatus(); }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OutputBox.Text)) throw new ConversionError("OutputRequired");
            string folder = Path.GetFullPath(OutputBox.Text);
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception error) { ShowError(error); }
    }
    private void Help_Click(object sender, RoutedEventArgs e) => PreviewDialog.Show(this, T["HelpText"], false);
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; if (closeAfterCancel || !PreviewDialog.Show(this, T["CloseQuestion"], true)) return; closeAfterCancel = true; cancellation?.Cancel(); SetStatus("Canceling"); return; }
        if (preview) return;
        settings.English = T.English; settings.Format = Format.Id; settings.OutputDirectory = OutputBox.Text; settings.Quality = Value(QualityCombo); settings.Rate = Value(RateCombo); settings.Channels = Value(ChannelCombo); settings.Tags = TagsCheck.IsChecked == true;
        try { settings.Save(); } catch (Exception error) { App.Log(error); }
    }
    internal void SetPreview()
    {
        rows.Add(new() { Path = @"Vorschau\Sprachaufnahme.wav", Info = new("", TimeSpan.FromSeconds(142), 44100, 2, "pcm_s16le", 25_000_000) });
        rows.Add(new() { Path = @"Vorschau\Musikstück.flac", Info = new("", TimeSpan.FromSeconds(228), 48000, 2, "flac", 18_000_000) });
        RefreshRows();
    }
}

internal static class PreviewDialog
{
    public static bool Show(Window owner, string message, bool question)
    {
        var dialog = new Window { Title = Texts.Current["Title"], Owner = owner, Width = 540, SizeToContent = SizeToContent.Height, MaxHeight = SystemParameters.WorkArea.Height * .85, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var root = new DockPanel { Margin = new Thickness(24) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        root.Children.Add(new ScrollViewer { MaxHeight = SystemParameters.WorkArea.Height * .65, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 23 } });
        bool accepted = false;
        foreach (bool yes in question ? new[] { true, false } : new[] { true })
        {
            var button = new Button { Content = question ? Texts.Current[yes ? "Yes" : "No"] : "OK", MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = question && !yes, IsDefault = !question || !yes };
            button.Click += (_, _) => { accepted = yes; dialog.DialogResult = true; }; actions.Children.Add(button);
        }
        dialog.Content = root; dialog.ShowDialog(); return accepted;
    }
}
