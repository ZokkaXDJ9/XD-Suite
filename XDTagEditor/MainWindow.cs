using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Preview;

namespace XDTagEditor;

public sealed partial class MainWindow : Window
{
    readonly Ui ui;
    readonly ObservableCollection<TagInfo> rows = new();
    readonly DataGrid grid = Ui.Grid();
    readonly Dictionary<string, (CheckBox Apply, TextBox Value)> fields = new();
    readonly Image cover = new() { Height = 145, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new(0, 5, 0, 8) };
    readonly TextBlock status;
    readonly TextBox template = Ui.Input("Rename template");
    readonly List<Control> commands = new();
    readonly CheckBox coverApply = new();
    byte[]? coverBytes; string coverMime = "image/jpeg";
    bool loading, busy;
    TagInfo[] selection = [];
    public MainWindow(string[] args)
    {
        ui = new("XDTagEditorBeta", args); Ui.Style(this);
        ui.Local(() => Title = ui.T("XD Tag-Editor Beta", "XD Tag Editor Beta"));
        var root = new DockPanel { Margin = new(25, 0, 25, 20) }; Content = root;
        var heading = new DockPanel(); var language = ui.Language(); DockPanel.SetDock(language, Dock.Right); heading.Children.Add(language);
        heading.Children.Add(ui.Text("XD Tag-Editor", "XD Tag Editor", 26, true)); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading); heading.Visibility = Visibility.Collapsed;
        var hint = ui.Text("Tags und Cover bearbeiten · Änderungen erst beim Speichern.", "Edit tags and covers · Changes are applied only when saved."); DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var toolbar = new WrapPanel();
        AddButton(toolbar, "Dateien hinzufügen", "Add files", AddFilesDialog);
        AddButton(toolbar, "Ordner hinzufügen", "Add folder", AddFolder);
        AddButton(toolbar, "Entfernen", "Remove", () => { foreach (var row in Selected()) rows.Remove(row); });
        AddButton(toolbar, "Neu laden", "Reload", async () => await Reload());
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        status = ui.Text("Dateien hinzufügen und auswählen. Sicherungen entstehen unter .xd-tag-backups neben der jeweiligen Datei.", "Add and select files. Backups are stored in .xd-tag-backups beside each file.");
        var footerContent = new DockPanel(); var save = ui.Button("Änderungen speichern", "Save changes", Save, true); commands.Add(save); DockPanel.SetDock(save, Dock.Right); footerContent.Children.Add(save); footerContent.Children.Add(status);
        var footer = Ui.Card(footerContent); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var rename = new DockPanel(); var renameButton = ui.Button("Umbenennung ansehen", "Preview rename", RenamePreview); commands.Add(renameButton); DockPanel.SetDock(renameButton, Dock.Right); rename.Children.Add(renameButton);
        template.Text = "{track} - {artist} - {title}"; rename.Children.Add(template);
        var renamePanel = new StackPanel(); renamePanel.Children.Add(ui.Text("Dateinamen aus Tags: {track}, {artist}, {title}, {album}, {disc}, {year}", "Filenames from tags: {track}, {artist}, {title}, {album}, {disc}, {year}")); renamePanel.Children.Add(rename);
        var renameCard = Ui.Card(renamePanel); DockPanel.SetDock(renameCard, Dock.Bottom); root.Children.Add(renameCard);
        var workspace = new Grid(); workspace.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); workspace.ColumnDefinitions.Add(new() { Width = new GridLength(355) }); root.Children.Add(workspace);
        grid.ItemsSource = rows; grid.IsReadOnly = true;
        ui.Column(grid, "Name", "Datei", "File", 1.6); ui.Column(grid, "Title", "Titel", "Title"); ui.Column(grid, "Artist", "Interpret", "Artist"); ui.Column(grid, "Album", "Album", "Album");
        grid.SelectionChanged += SelectionChanged;
        grid.AllowDrop = true; grid.DragOver += (_, e) => { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        grid.Drop += async (_, e) => { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] paths) await AddPaths(paths); };
        var gridCard = Ui.Card(grid); gridCard.Margin = new(0, 0, 15, 12); workspace.Children.Add(gridCard);
        var editor = new StackPanel(); editor.Children.Add(ui.Text("Ausgewählte Dateien", "Selected files", 17, true));
        editor.Children.Add(ui.Text("Nur angehakte Felder werden geändert. Ein leeres angehaktes Feld löscht den Wert. Mehrere Interpreten mit ; trennen.", "Only checked fields are changed. A checked empty field clears its value. Separate multiple artists with ;."));
        foreach (var (key, de, en) in new[] { ("Title", "Titel", "Title"), ("Artist", "Interpret", "Artist"), ("Album", "Album", "Album"), ("AlbumArtist", "Albuminterpret", "Album artist"), ("Genre", "Genre", "Genre"), ("Year", "Jahr", "Year"), ("Track", "Titelnummer", "Track number"), ("Disc", "CD-Nummer", "Disc number"), ("Comment", "Kommentar", "Comment") })
        {
            var check = new CheckBox { Margin = new(0, 7, 0, 4) }; ui.Local(() => check.Content = ui.T(de, en)); var box = Ui.Input(en);
            box.TextChanged += (_, _) => { if (!loading) check.IsChecked = true; }; fields.Add(key, (check, box)); editor.Children.Add(check); editor.Children.Add(box);
        }
        editor.Children.Add(cover); ui.Local(() => coverApply.Content = ui.T("Cover ersetzen oder entfernen", "Replace or remove cover")); editor.Children.Add(coverApply);
        var coverTools = new WrapPanel(); AddButton(coverTools, "Cover wählen", "Choose cover", ChooseCover); AddButton(coverTools, "Entfernen", "Remove", () => { coverBytes = null; cover.Source = null; coverApply.IsChecked = true; }); editor.Children.Add(coverTools);
        AddButton(editor, "Aus Dateinamen übernehmen", "Import from filenames", ImportFilenames);
        var editCard = Ui.Card(new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(editCard, 1); workspace.Children.Add(editCard);
        XD.Design.BetaChrome.Attach(this, () => ui.English, english => language.SelectedIndex = english ? 1 : 0);
        var fileMenu = XD.Design.BetaChrome.Group(this, "_Datei", "_File");
        foreach (Button button in toolbar.Children) XD.Design.BetaChrome.Button(this, fileMenu, button);
        XD.Design.BetaChrome.Button(this, fileMenu, save);
        XD.Design.BetaChrome.Add(this, fileMenu, "Beenden", "Exit", Close);
        var editMenu = XD.Design.BetaChrome.Group(this, "_Bearbeiten", "_Edit");
        XD.Design.BetaChrome.Button(this, editMenu, renameButton);
        foreach (Button button in coverTools.Children) XD.Design.BetaChrome.Button(this, editMenu, button);
        ui.Local(() => XD.Design.BetaChrome.Refresh(this));
        Closing += (_, e) => { if (busy || (Dirty && !ui.Confirm(this, "Ungespeicherte Eingaben verwerfen?", "Discard unsaved input?"))) e.Cancel = true; };
    }
    bool Dirty => coverApply.IsChecked == true || fields.Values.Any(v => v.Apply.IsChecked == true);
    TagInfo[] Selected() => grid.SelectedItems.Cast<TagInfo>().ToArray();
    void AddButton(Panel panel, string de, string en, Action action, bool primary = false) { var b = ui.Button(de, en, action, primary); commands.Add(b); panel.Children.Add(b); }
    async void AddFilesDialog() { var dialog = new OpenFileDialog { Multiselect = true, Filter = "Audio|*.mp3;*.flac;*.m4a;*.m4b;*.ogg;*.opus;*.wav;*.aif;*.aiff;*.wma" }; if (dialog.ShowDialog(this) == true) await AddPaths(dialog.FileNames); }
    async void AddFolder() { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) await AddPaths([dialog.FolderName]); }
    public async Task AddPaths(IEnumerable<string> paths)
    {
        if (busy) return; SetBusy(true);
        var errors = new List<string>();
        try
        {
            var added = await Task.Run(() =>
            {
                var result = new List<TagInfo>(); var seen = rows.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    try
                    {
                        foreach (var file in Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(p => !p.Contains(".xd-tag-backups", StringComparison.OrdinalIgnoreCase)) : [path])
                            if (TagService.Extensions.Contains(Path.GetExtension(file)) && seen.Add(Path.GetFullPath(file))) { try { result.Add(TagService.Read(file)); } catch (Exception e) { errors.Add(Path.GetFileName(file) + ": " + e.Message); } }
                    }
                    catch (Exception e) { errors.Add(e.Message); }
                }
                return result;
            });
            foreach (var row in added) rows.Add(row); if (grid.SelectedItem == null && rows.Count > 0) grid.SelectedIndex = 0;
            status.Text = ui.T($"{rows.Count} Dateien geladen.", $"{rows.Count} files loaded.") + (errors.Count > 0 ? "\n" + string.Join("\n", errors.Take(4)) : "");
        }
        catch (Exception error) { ui.Error(this, error); }
        finally { SetBusy(false); }
    }
    void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        if (Dirty && !ui.Confirm(this, "Ungespeicherte Eingaben verwerfen und Auswahl wechseln?", "Discard unsaved input and change selection?"))
        { loading = true; grid.SelectedItems.Clear(); foreach (var row in selection.Where(rows.Contains)) grid.SelectedItems.Add(row); loading = false; return; }
        selection = Selected(); LoadSelection();
    }
    void LoadSelection()
    {
        loading = true;
        foreach (var (key, field) in fields)
        {
            var values = selection.Select(row => key switch { "Title" => row.Title, "Artist" => row.Artist, "Album" => row.Album, "AlbumArtist" => row.AlbumArtist, "Genre" => row.Genre, "Year" => row.Year == 0 ? "" : row.Year.ToString(), "Track" => row.Track == 0 ? "" : row.Track.ToString(), "Disc" => row.Disc == 0 ? "" : row.Disc.ToString(), _ => row.Comment }).Distinct().ToArray();
            field.Value.Text = values.Length == 1 ? values[0] : ""; field.Value.ToolTip = values.Length > 1 ? ui.T("Verschiedene Werte", "Mixed values") : null; field.Apply.IsChecked = false;
        }
        coverBytes = selection.FirstOrDefault()?.Cover; coverMime = selection.FirstOrDefault()?.CoverMime ?? "image/jpeg"; ShowCover(); coverApply.IsChecked = false; loading = false;
    }
    void ShowCover()
    {
        cover.Source = null;
        if (coverBytes == null) return;
        try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = new MemoryStream(coverBytes); image.DecodePixelWidth = 300; image.EndInit(); image.Freeze(); cover.Source = image; } catch { cover.Source = null; }
    }
    void ChooseCover()
    {
        var dialog = new OpenFileDialog { Filter = "Cover|*.jpg;*.jpeg;*.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { if (new FileInfo(dialog.FileName).Length > 20 * 1024 * 1024) throw new IOException(ui.T("Cover maximal 20 MB.", "Cover maximum 20 MB.")); var bytes = File.ReadAllBytes(dialog.FileName); var frame = BitmapFrame.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad); coverMime = frame.Decoder is PngBitmapDecoder ? "image/png" : frame.Decoder is JpegBitmapDecoder ? "image/jpeg" : throw new IOException("PNG / JPEG"); coverBytes = bytes; ShowCover(); coverApply.IsChecked = true; } catch (Exception e) { ui.Error(this, e); }
    }
    async Task Reload()
    {
        if (Dirty && !ui.Confirm(this, "Ungespeicherte Eingaben verwerfen?", "Discard unsaved input?")) return;
        foreach (var field in fields.Values) field.Apply.IsChecked = false; coverApply.IsChecked = false;
        await WriteSelected(row => TagService.Read(row.Path), false);
    }
    async void Save()
    {
        var patch = new TagPatch(fields.Where(p => p.Value.Apply.IsChecked == true).ToDictionary(p => p.Key, p => p.Value.Value.Text), coverApply.IsChecked == true, coverBytes, coverMime);
        try { TagService.Validate(patch); } catch (Exception e) { ui.Error(this, e); return; }
        if (!Dirty || Selected().Length == 0) return;
        if (ui.Confirm(this, $"Änderungen in {Selected().Length} Dateien speichern? Vorherige Dateien bleiben als Sicherung erhalten.", $"Save changes to {Selected().Length} files? Previous files are retained as backups.")) await WriteSelected(row => TagService.Save(row, patch), true);
    }
    async Task WriteSelected(Func<TagInfo, TagInfo> operation, bool writing)
    {
        var selected = Selected(); SetBusy(true); loading = true; var errors = new List<string>(); int done = 0;
        try
        {
            foreach (var row in selected)
            {
                try { var updated = await Task.Run(() => operation(row)); rows[rows.IndexOf(row)] = updated; done++; }
                catch (Exception e) { errors.Add(row.Name + ": " + e.Message); }
            }
            grid.SelectedItems.Clear(); foreach (var old in selected) { var current = rows.FirstOrDefault(r => r.Path == old.Path); if (current != null) grid.SelectedItems.Add(current); }
            selection = Selected(); LoadSelection();
            status.Text = ui.T($"{done} Dateien {(writing ? "gespeichert" : "geladen")}, {errors.Count} Fehler.", $"{done} files {(writing ? "saved" : "loaded")}, {errors.Count} errors.") + "\n" + string.Join("\n", errors.Take(3));
        }
        finally { loading = false; SetBusy(false); }
    }
    async void ImportFilenames()
    {
        var selected = Selected(); if (selected.Length == 0) return;
        try
        {
            var patches = selected.ToDictionary(r => r.Path, r => TagService.FromFilename(r.Path)); foreach (var p in patches.Values) TagService.Validate(p);
            string preview = string.Join("\n", selected.Take(8).Select(r => r.Name + " → " + patches[r.Path].Values["Artist"] + " / " + patches[r.Path].Values["Title"]));
            if (ui.Confirm(this, "Tags aus Dateinamen speichern? Sicherungen bleiben erhalten.\n\n" + preview, "Save tags from filenames? Backups are retained.\n\n" + preview)) await WriteSelected(row => TagService.Save(row, patches[row.Path]), true);
        }
        catch (Exception e) { ui.Error(this, e); }
    }
    void RenamePreview()
    {
        if (Selected().Length == 0) return;
        try
        {
            var selected = Selected(); var plan = TagService.PlanRename(selected, template.Text);
            if (plan.Count == 0) { status.Text = ui.T("Keine Dateinamen zu ändern.", "No filenames to change."); return; }
            var dialog = new Window { Owner = this, Title = ui.T("Umbenennung prüfen", "Review rename"), Width = 880, Height = 460, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var panel = new DockPanel { Margin = new(20) }; dialog.Content = panel;
            var apply = ui.Button("Jetzt umbenennen", "Rename now", () => { try { foreach (var row in selected) if (TagService.Fingerprint(row.Path) != row.Fingerprint) throw new IOException("Datei geändert / File changed"); TagService.Rename(plan); loading = true; foreach (var item in plan) { var index = rows.ToList().FindIndex(r => r.Path == item.Source); rows[index] = TagService.Read(item.Destination); } selection = []; grid.SelectedItems.Clear(); LoadSelection(); loading = false; dialog.DialogResult = true; status.Text = ui.T($"{plan.Count} Dateien umbenannt.", $"Renamed {plan.Count} files."); } catch (Exception e) { loading = false; ui.Error(dialog, e); } }, true);
            DockPanel.SetDock(apply, Dock.Bottom); panel.Children.Add(apply);
            var preview = Ui.Grid(); preview.IsReadOnly = true; ui.Column(preview, "Before", "Bisher", "Before"); ui.Column(preview, "After", "Danach", "After"); preview.ItemsSource = plan; panel.Children.Add(preview); dialog.ShowDialog();
        }
        catch (Exception e) { ui.Error(this, e); }
    }
    void SetBusy(bool value) { busy = value; foreach (var command in commands) command.IsEnabled = !value; grid.IsEnabled = !value; foreach (var field in fields.Values) { field.Value.IsEnabled = !value; field.Apply.IsEnabled = !value; } coverApply.IsEnabled = !value; template.IsEnabled = !value; }
}
