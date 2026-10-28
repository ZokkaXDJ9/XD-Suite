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
    private bool refreshing;
    private string activeFormat = "";
    private EncoderPreferences Preferences => settings.ForFormat(activeFormat);
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
        activeFormat = Format.Id;
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
        if (!initial) StoreOptions();
        RefreshOptions();
        foreach (var row in rows) row.Refresh();
        RefreshRows(); RefreshStatus();
    }
    private static void SetChoices(ComboBox combo, Choice[] choices, int selected)
    {
        combo.ItemsSource = choices; combo.SelectedItem = choices.FirstOrDefault(c => c.Value == selected) ?? choices[0];
    }
    private void StoreOptions()
    {
        if (!ready || refreshing) return;
        var p = Preferences;
        p.Mode = (EncodingMode)Value(ModeCombo); p.Rate = Value(RateCombo); p.Channels = Value(ChannelCombo);
        StoreQuality();
        p.FlacDepth = Value(DepthCombo); p.Application = Value(ApplicationCombo);
    }
    private void StoreQuality()
    {
        if (refreshing || !ready) return;
        var p = Preferences;
        if (activeFormat == "WAV") p.Depth = Value(QualityCombo);
        else if (activeFormat == "FLAC") p.Compression = Value(QualityCombo);
        else if (EncodingProfile.UsesQuality(activeFormat, p.Mode)) p.VbrQuality = Value(QualityCombo);
        else p.Bitrate = Value(QualityCombo);
    }
    private void RefreshOptions()
    {
        refreshing = true;
        var p = Preferences;
        var modes = EncodingProfile.Modes(activeFormat);
        if (!modes.Contains(p.Mode)) p.Mode = modes[0];
        SetChoices(ModeCombo, modes.Select(m => new Choice(T[m.ToString()], (int)m)).ToArray(), (int)p.Mode);
        SetChoices(RateCombo, [new(T["Automatic"], 0), .. EncodingProfile.Rates(activeFormat, p.Mode).Select(r => new Choice(r.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo(T.English ? "en-US" : "de-DE")) + " Hz", r))], p.Rate);
        p.Rate = Value(RateCombo);
        SetChoices(ChannelCombo, [new(T["Automatic"], 0), new(T["Mono"], 1), new(T["Stereo"], 2)], p.Channels);
        SetChoices(DepthCombo, [new(T["Automatic"], 0), new(T["Depth16"], 16), new(T["Depth24"], 24)], p.FlacDepth);
        SetChoices(ApplicationCombo, [new(T["Music"], 0), new(T["Speech"], 1), new(T["LowDelay"], 2)], p.Application);
        FlacDepthPanel.Visibility = activeFormat == "FLAC" ? Visibility.Visible : Visibility.Collapsed;
        ApplicationPanel.Visibility = activeFormat == "Opus" ? Visibility.Visible : Visibility.Collapsed;
        Choice[] choices;
        int selected;
        if (activeFormat == "WAV")
        {
            QualityLabel.Text = T["BitDepth"];
            choices = [new(T["Depth8"], 8), new(T["Depth16"], 16), new(T["Depth24"], 24), new(T["Depth32"], 33), new(T["Float"], 32), new(T["Float64"], 64)]; selected = p.Depth;
        }
        else if (activeFormat == "FLAC")
        {
            QualityLabel.Text = T["Compression"];
            choices = Enumerable.Range(0, 13).Select(n => new Choice(n + (n == 0 ? T["Fastest"] : n == 5 ? " · " + T["Default"] : n == 12 ? T["Smallest"] : ""), n)).ToArray(); selected = p.Compression;
        }
        else if (EncodingProfile.UsesQuality(activeFormat, p.Mode))
        {
            QualityLabel.Text = T["VbrQuality"];
            choices = EncodingProfile.Qualities(activeFormat).Select(n => new Choice((activeFormat == "MP3" ? "V" : "Q ") + n, n)).ToArray(); selected = p.VbrQuality;
        }
        else
        {
            QualityLabel.Text = T[p.Mode == EncodingMode.Cbr ? "Bitrate" : "TargetBitrate"];
            choices = EncodingProfile.Bitrates(activeFormat, p.Rate, p.Channels).Select(n => new Choice(n + " kbit/s", n)).ToArray(); selected = p.Bitrate;
        }
        SetChoices(QualityCombo, choices, choices.MinBy(c => Math.Abs(c.Value - selected))!.Value);
        ModeCombo.IsEnabled = modes.Length > 1;
        ModePanel.Visibility = modes.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        EncodingHint.Text = T[activeFormat == "FLAC" ? "FlacHint" : activeFormat == "WAV" ? "PcmHint" : EncodingProfile.UsesQuality(activeFormat, p.Mode) ? activeFormat == "MP3" ? "Mp3VbrHint" : "VbrHint" : "BitrateHint"];
        if (activeFormat == "Opus") EncodingHint.Text += "\n" + T["OpusHint"];
        refreshing = false;
        StoreOptions();
    }
    private void Quality_Changed(object sender, SelectionChangedEventArgs e) => StoreQuality();
    private void Options_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || refreshing) return;
        Preferences.Mode = (EncodingMode)Value(ModeCombo); Preferences.Rate = Value(RateCombo); Preferences.Channels = Value(ChannelCombo);
        RefreshOptions();
    }
    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        StoreOptions(); activeFormat = Format.Id; RefreshOptions();
    }
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
        StoreOptions();
        var p = Preferences;
        var options = new ConvertOptions(Format, OutputBox.Text.Trim(), activeFormat == "WAV" ? p.Depth : p.Bitrate, p.Rate, p.Channels, TagsCheck.IsChecked == true,
            p.Mode, p.VbrQuality, p.Compression, p.FlacDepth, p.Application switch { 1 => "voip", 2 => "lowdelay", _ => "audio" });
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
        StoreOptions();
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
