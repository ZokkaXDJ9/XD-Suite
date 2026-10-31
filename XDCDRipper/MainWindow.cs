using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Preview;

namespace XDCDRipper;
public sealed class TrackRow
{
    public bool Selected { get; set; } = true;
    public CdTrack Track { get; init; } = null!;
    public int Number => Track.Number;
    public string Duration => Track.Duration;
    public string Title { get; set; } = "";
}
public sealed class MainWindow : Window
{
    readonly Ui ui;
    readonly ObservableCollection<TrackRow> rows = new();
    readonly DataGrid grid = Ui.Grid();
    readonly ComboBox drives = new() { Width = 130, Margin = new(0, 4, 10, 4) };
    readonly ComboBox format = new() { ItemsSource = new[] { "FLAC", "WAV", "MP3" }, SelectedIndex = 0, MinHeight = 32 };
    readonly ComboBox bitrate = new() { ItemsSource = new[] { 128, 192, 256, 320 }, SelectedItem = 192, MinHeight = 32 };
    readonly TextBox artist = Ui.Input("Artist"), album = Ui.Input("Album"), year = Ui.Input("Year"), genre = Ui.Input("Genre"), destination = Ui.Input("Output folder");
    readonly CheckBox doubleRead = new() { IsChecked = true, Margin = new(0, 12, 0, 8) };
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 6, Margin = new(0, 12, 0, 3) };
    readonly TextBlock status;
    readonly List<Control> commands = new();
    readonly Button cancel;
    readonly string engine;
    IReadOnlyList<CdTrack> loaded = [];
    CancellationTokenSource? cancellation;
    bool busy;
    public MainWindow(string[] args)
    {
        ui = new("XDCDRipperBeta", args); engine = Path.Combine(AppContext.BaseDirectory, "engine", "ffmpeg.exe"); Ui.Style(this);
        ui.Local(() => Title = "XD CD Ripper · 0.5.2-Beta / Build 1");
        var root = new DockPanel { Margin = new(25) }; Content = root;
        var heading = new DockPanel(); var language = ui.Language(); DockPanel.SetDock(language, Dock.Right); heading.Children.Add(language); heading.Children.Add(ui.Text("XD CD-Ripper", "XD CD Ripper", 26, true)); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var hint = ui.Text("Audio-CDs lokal als FLAC, WAV oder MP3 speichern · 0.5.2-Beta", "Save audio CDs locally as FLAC, WAV or MP3 · 0.5.2-Beta"); DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var toolbar = new WrapPanel(); toolbar.Children.Add(drives); AddButton(toolbar, "Laufwerke suchen", "Refresh drives", RefreshDrives); AddButton(toolbar, "CD einlesen", "Read disc", LoadDisc); AddButton(toolbar, "Alle auswählen", "Select all", () => { foreach (var row in rows) row.Selected = true; grid.Items.Refresh(); }); AddButton(toolbar, "Auswahl aufheben", "Select none", () => { foreach (var row in rows) row.Selected = false; grid.Items.Refresh(); }); DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        status = ui.Text("Audio-CD einlegen und CD einlesen wählen. Titel und Albumangaben können anschließend bearbeitet werden.", "Insert an audio CD and choose Read disc. You can then edit track titles and album information.");
        var footer = new StackPanel(); footer.Children.Add(status); footer.Children.Add(progress); var controls = new WrapPanel();
        AddButton(controls, "Zielordner öffnen", "Open output folder", () => { try { Directory.CreateDirectory(destination.Text); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(destination.Text) { UseShellExecute = true }); } catch (Exception e) { ui.Error(this, e); } });
        cancel = ui.Button("Abbrechen", "Cancel", () => { cancellation?.Cancel(); cancel!.IsEnabled = false; }); cancel.IsEnabled = false; controls.Children.Add(cancel); AddButton(controls, "Ausgewählte Titel auslesen", "Rip selected tracks", StartRip, true); footer.Children.Add(controls);
        var footerCard = Ui.Card(footer); DockPanel.SetDock(footerCard, Dock.Bottom); root.Children.Add(footerCard);
        var output = new DockPanel(); var browse = ui.Button("Durchsuchen", "Browse", () => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) destination.Text = dialog.FolderName; }); commands.Add(browse); DockPanel.SetDock(browse, Dock.Right); output.Children.Add(browse); destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "XD CD Ripper"); output.Children.Add(destination);
        var outputPanel = new StackPanel(); outputPanel.Children.Add(ui.Text("Zielordner — vorhandene Dateien werden nicht überschrieben", "Output folder — existing files are never overwritten", 13, true)); outputPanel.Children.Add(output); var outputCard = Ui.Card(outputPanel); DockPanel.SetDock(outputCard, Dock.Bottom); root.Children.Add(outputCard);
        var workspace = new Grid(); workspace.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); workspace.ColumnDefinitions.Add(new() { Width = new GridLength(310) }); root.Children.Add(workspace);
        grid.ItemsSource = rows; grid.Columns.Add(new DataGridCheckBoxColumn { Header = "✓", Binding = new System.Windows.Data.Binding("Selected") { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged }, Width = 45 });
        ui.Column(grid, "Number", "Nr.", "No.", .35); ui.Column(grid, "Title", "Titel (bearbeitbar)", "Title (editable)", 2, true); ui.Column(grid, "Duration", "Laufzeit", "Duration", .55);
        var gridCard = Ui.Card(grid); gridCard.Margin = new(0, 0, 15, 12); workspace.Children.Add(gridCard);
        var options = new StackPanel(); options.Children.Add(ui.Text("Album und Ausgabe", "Album and output", 17, true));
        foreach (var (de, en, box) in new[] { ("Interpret", "Artist", artist), ("Album", "Album", album), ("Jahr", "Year", year), ("Genre", "Genre", genre) }) { options.Children.Add(ui.Text(de, en)); options.Children.Add(box); }
        options.Children.Add(ui.Text("Format", "Format")); options.Children.Add(format); options.Children.Add(ui.Text("MP3-Bitrate (kbit/s)", "MP3 bitrate (kbit/s)")); options.Children.Add(bitrate); bitrate.IsEnabled = false;
        format.SelectionChanged += (_, _) => bitrate.IsEnabled = !busy && (string?)format.SelectedItem == "MP3";
        ui.Local(() => doubleRead.Content = ui.T("Audioblöcke doppelt lesen und vergleichen", "Read audio blocks twice and compare")); options.Children.Add(doubleRead);
        options.Children.Add(ui.Text("Bei Lesefehlern wird erneut gelesen. Dauerhafte Fehler brechen den Titel ab. Fertige Titel bleiben erhalten.", "Read errors are retried. Persistent errors stop the current track. Completed tracks are retained."));
        options.Children.Add(ui.Text("Titelangaben werden manuell eingetragen. Keine Online-Datenbank und keine AccurateRip-Prüfung in dieser Beta.", "Enter track information manually. No online database or AccurateRip verification in this beta."));
        var optionCard = Ui.Card(new ScrollViewer { Content = options, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(optionCard, 1); workspace.Children.Add(optionCard);
        Closing += (_, e) => { if (busy) { cancellation?.Cancel(); status.Text = ui.T("Abbruch läuft. Danach kann das Fenster geschlossen werden.", "Cancelling. Close the window after cancellation finishes."); e.Cancel = true; } };
        RefreshDrives();
        if (args.Contains("--render")) { rows.Add(new() { Track = new(1, 0, 14250), Title = "Erster Titel / First track" }); rows.Add(new() { Track = new(2, 14250, 29325), Title = "Zweiter Titel / Second track" }); artist.Text = "Interpret / Artist"; album.Text = "Album"; }
    }
    void AddButton(Panel panel, string de, string en, Action action, bool primary = false) { var button = ui.Button(de, en, action, primary); commands.Add(button); panel.Children.Add(button); }
    void RefreshDrives() { if (busy) return; drives.ItemsSource = WindowsDiscSource.Drives(); drives.SelectedIndex = drives.Items.Count > 0 ? 0 : -1; if (drives.Items.Count == 0) status.Text = ui.T("Kein optisches Laufwerk gefunden. Laufwerk anschließen und erneut suchen.", "No optical drive found. Connect a drive and refresh."); }
    async void LoadDisc()
    {
        if (busy || drives.SelectedItem is not string drive) return; SetBusy(true);
        try { loaded = await Task.Run(() => { using var source = new WindowsDiscSource(drive); return source.Tracks; }); rows.Clear(); foreach (var track in loaded) rows.Add(new() { Track = track, Title = ui.T("Titel ", "Track ") + track.Number.ToString("00") }); status.Text = ui.T($"{rows.Count} Audiotitel. Titel und Albumangaben prüfen.", $"{rows.Count} audio tracks. Review titles and album information."); }
        catch (Exception e) { loaded = []; rows.Clear(); ui.Error(this, e); }
        finally { SetBusy(false); }
    }
    async void StartRip()
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
        var tracks = rows.Where(r => r.Selected).Select(r => new RipTrack(r.Track, r.Title.Trim())).ToArray();
        if (busy || drives.SelectedItem is not string drive || tracks.Length == 0) return;
        if (tracks.Any(t => t.Title.Length == 0) || (year.Text.Length > 0 && (!uint.TryParse(year.Text, out var number) || number > 9999))) { status.Text = ui.T("Titel und Jahr prüfen (0–9999).", "Review titles and year (0–9999)."); return; }
        var options = new RipOptions(destination.Text.Trim(), (string)format.SelectedItem, artist.Text, album.Text, year.Text, genre.Text, doubleRead.IsChecked == true, (int)bitrate.SelectedItem);
        if (!File.Exists(engine)) { status.Text = ui.T("Audio-Engine fehlt. Den vollständigen Programmordner verwenden.", "Audio engine missing. Use the complete application folder."); return; }
        try { Directory.CreateDirectory(options.Directory); } catch (Exception e) { ui.Error(this, e); return; }
        SetBusy(true); cancellation = new(); cancel.IsEnabled = true; var token = cancellation.Token;
        int completed = 0, total = tracks.Sum(t => t.Track.Sectors), previous = 0;
        try
        {
            using var source = await Task.Run(() => new WindowsDiscSource(drive));
            if (!source.Tracks.SequenceEqual(loaded)) throw new IOException(ui.T("CD wurde gewechselt. Erneut einlesen.", "Disc changed. Read the disc again."));
            foreach (var track in tracks)
            {
                token.ThrowIfCancellationRequested(); string temporary = Path.Combine(options.Directory, ".xd-rip-" + Guid.NewGuid().ToString("N") + ".wav");
                try
                {
                    status.Text = ui.T("Lese: ", "Reading: ") + track.Title;
                    int offset = previous; var update = new Progress<int>(sectors => progress.Value = 100d * (offset + sectors) / total);
                    await Task.Run(() => RipService.WriteWave(source, track.Track, temporary, options.DoubleRead, s => ((IProgress<int>)update).Report(s), token));
                    status.Text = ui.T("Speichere: ", "Saving: ") + track.Title;
                    await RipService.Export(engine, temporary, track, options, token); completed++; previous += track.Track.Sectors;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            progress.Value = 100; status.Text = ui.T($"{completed} Titel gespeichert.", $"Saved {completed} tracks.");
        }
        catch (OperationCanceledException) { status.Text = ui.T($"Abgebrochen. {completed} fertige Titel bleiben erhalten.", $"Cancelled. {completed} completed tracks retained."); }
        catch (Exception e) { status.Text = ui.T($"{completed} Titel fertig. Fehler: ", $"{completed} tracks completed. Error: ") + e.Message; ui.Error(this, e); }
        finally { SetBusy(false); cancel.IsEnabled = false; cancellation.Dispose(); cancellation = null; }
    }
    void SetBusy(bool value) { busy = value; foreach (var c in commands) c.IsEnabled = !value; foreach (Control c in new Control[] { drives, grid, artist, album, year, genre, destination, format, doubleRead }) c.IsEnabled = !value; bitrate.IsEnabled = !value && (string?)format.SelectedItem == "MP3"; }
}
