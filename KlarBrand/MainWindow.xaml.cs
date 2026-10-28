using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using XDBurner.Services;

namespace XDBurner;

public partial class MainWindow : Window
{
    private readonly DiscService discs = new();
    private readonly ObservableCollection<ProjectItem> dataItems = [];
    private readonly ObservableCollection<ProjectItem> audioItems = [];
    private ProjectKind kind;
    private MediaInfo? media;
    private string? isoPath;
    private bool busy, refreshing, readingMedia;
    private int mediaGeneration;
    private CancellationTokenSource? cancellation;
    private readonly bool preview;
    private ObservableCollection<ProjectItem> Items => kind == ProjectKind.Audio ? audioItems : dataItems;
    private RecorderInfo? Recorder => DriveCombo.SelectedItem is RecorderInfo r && r.Id.Length > 0 ? r : null;

    public MainWindow(bool preview = false)
    {
        this.preview = preview;
        InitializeComponent();
        L10n.Attach(this);
        L10n.Changed += LanguageChanged;
        Closed += (_, _) => L10n.Changed -= LanguageChanged;
        DataNav.IsChecked = true;
        GermanMenu.IsChecked = L10n.Current == UiLanguage.German;
        EnglishMenu.IsChecked = L10n.Current == UiLanguage.English;
        Loaded += async (_, _) => { if (!preview) await RefreshDrives(); };
    }

    private void German_Click(object sender, RoutedEventArgs e) => ChangeLanguage(UiLanguage.German);
    private void English_Click(object sender, RoutedEventArgs e) => ChangeLanguage(UiLanguage.English);
    private void ChangeLanguage(UiLanguage language)
    {
        try { L10n.Set(language); } catch (Exception ex) { ShowError(ex); }
    }
    private void LanguageChanged(object? sender, EventArgs e)
    {
        GermanMenu.IsChecked = L10n.Current == UiLanguage.German;
        EnglishMenu.IsChecked = L10n.Current == UiLanguage.English;
        FileGrid.Items.Refresh();
        DriveCombo.Items.Refresh();
        UpdateProject();
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (PageTitle == null || sender is not RadioButton button) return;
        kind = Enum.Parse<ProjectKind>((string)button.Tag);
        bool audio = kind == ProjectKind.Audio, data = kind == ProjectKind.Data;
        FilesPanel.Visibility = Show(data || audio);
        IsoPanel.Visibility = Show(kind == ProjectKind.Iso);
        ErasePanel.Visibility = Show(kind == ProjectKind.Erase);
        VolumePanel.Visibility = Show(data);
        WriteSettings.Visibility = Show(kind != ProjectKind.Erase);
        VerifyCheck.Visibility = Show(data || kind == ProjectKind.Iso);
        FullEraseCheck.Visibility = Show(kind == ProjectKind.Erase);
        ModeHint.Visibility = InfoSeparator.Visibility = Show(audio || kind == ProjectKind.Erase);
        AddFolderButton.Visibility = Show(data);
        UpButton.Visibility = DownButton.Visibility = Show(audio);
        CapacityPanel.Visibility = Show(kind != ProjectKind.Erase);
        ExportButton.Visibility = Show(data);
        AddFilesButton.Content = audio ? "+ Musikdateien" : "+ Dateien";
        SizeColumn.Header = audio ? "LÄNGE" : "GRÖSSE";
        FileGrid.ItemsSource = Items;
        ListTitle.Text = audio ? "Deine Titelliste" : "Deine Zusammenstellung";
        EmptyTitle.Text = audio ? "Dein nächster Lieblingsmix." : "Was kommt auf deine CD?";
        EmptyHint.Text = audio ? "MP3 oder WAV hierher ziehen.\nDie Reihenfolge bestimmst du." : "Dateien hierher ziehen oder unten hinzufügen.";
        (PageTitle.Text, PageDescription.Text, ModeHint.Text) = kind switch
        {
            ProjectKind.Audio => ("Musik, die mitkommt.", "Deine Titel als Audio-CD – für Anlage, Auto und CD-Player.", "MP3/WAV · ca. 2 s Pause pro Titel. CD-R für ältere CD-Player."),
            ProjectKind.Iso => ("Dein Abbild auf CD.", "Eine vorhandene ISO-Datei direkt auf einen Rohling brennen.", "Leere CD-R oder CD-RW erforderlich. Die CD wird abgeschlossen."),
            ProjectKind.Erase => ("Noch einmal von vorn.", "Eine CD-RW leeren und für neue Inhalte vorbereiten.", "Das Löschen lässt sich nach dem Start nicht abbrechen. Bitte den Brenner angeschlossen lassen."),
            _ => ("Eine CD für deine Dateien.", "Fotos, Dokumente und Erinnerungen. Sicher auf einer CD.", "Ordner bleiben erhalten. Die CD wird abgeschlossen.")
        };
        StartButton.Content = kind == ProjectKind.Erase ? "CD-RW löschen" : "CD brennen";
        StatusTitle.Text = "Bereit, wenn du es bist.";
        StatusText.Text = kind switch { ProjectKind.Audio => "Füge Musik hinzu und ordne deine Titel.", ProjectKind.Iso => "Wähle eine ISO-Datei und lege einen leeren Rohling ein.", ProjectKind.Erase => "Lege die CD-RW ein, die du löschen möchtest.", _ => "Füge Dateien hinzu. Dann kannst du deine CD brennen." };
        OperationBar.Visibility = Visibility.Collapsed;
        UpdateProject();
    }

    private async Task RefreshDrives()
    {
        if (busy || refreshing) return;
        refreshing = true;
        mediaGeneration++;
        string? previous = Recorder?.Id;
        MediaText.Text = "Brenner werden gesucht …";
        UpdateButtons();
        try
        {
            var recorders = await Task.Run(discs.GetRecorders);
            if (recorders.Count == 0) recorders.Add(new("", "Kein CD-Brenner gefunden"));
            DriveCombo.ItemsSource = recorders;
            DriveCombo.SelectedItem = recorders.FirstOrDefault(r => r.Id == previous) ?? recorders[0];
        }
        catch (Exception ex)
        {
            DriveCombo.ItemsSource = new[] { new RecorderInfo("", "Brenner nicht verfügbar") };
            DriveCombo.SelectedIndex = 0;
            ShowError(ex);
        }
        finally { refreshing = false; }
        await ReadMedia();
    }

    private async Task ReadMedia()
    {
        if (busy || refreshing) return;
        int generation = ++mediaGeneration;
        media = null;
        var recorder = Recorder;
        if (recorder == null)
        {
            readingMedia = false;
            MediaText.Text = "USB-CD-Brenner anschließen und aktualisieren. ISO-Export ist auch ohne Brenner möglich.";
            UpdateProject();
            return;
        }
        readingMedia = true;
        MediaText.Text = "CD wird eingelesen …";
        UpdateButtons();
        try
        {
            var info = await Task.Run(() => discs.GetMedia(recorder.Id));
            if (generation == mediaGeneration) { media = info; MediaText.Text = info.Description; }
        }
        catch (Exception ex)
        {
            if (generation == mediaGeneration) MediaText.Text = DiscService.DescribeError(ex);
        }
        finally
        {
            if (generation == mediaGeneration) { readingMedia = false; UpdateProject(); }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshDrives();
    private async void Drive_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!preview) await ReadMedia(); }
    private async void Eject_Click(object sender, RoutedEventArgs e)
    {
        if (Recorder is not { } recorder || busy) return;
        await RunOperation((_, _) => discs.Eject(recorder.Id), "CD ausgeworfen.", "Du kannst die CD entnehmen.", false);
        await ReadMedia();
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = L10n.T(kind == ProjectKind.Audio ? "Musik für die Audio-CD auswählen" : "Dateien zur Daten-CD hinzufügen"), Filter = L10n.T(kind == ProjectKind.Audio ? "Musikdateien (*.mp3;*.wav)|*.mp3;*.wav|Alle Dateien (*.*)|*.*" : "Alle Dateien (*.*)|*.*") };
        if (dialog.ShowDialog(this) == true) await AddPaths(dialog.FileNames);
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = L10n.T("Ordner zur Daten-CD hinzufügen") };
        if (dialog.ShowDialog(this) == true) await AddPaths(dialog.FolderNames);
    }

    private async Task AddPaths(IEnumerable<string> paths)
    {
        if (busy) return;
        bool audio = kind == ProjectKind.Audio;
        var selected = paths.ToArray();
        var target = Items;
        await RunOperation((progress, token) =>
        {
            var additions = new List<ProjectItem>();
            foreach (string path in selected)
            {
                token.ThrowIfCancellationRequested();
                if (target.Any(i => i.SourcePath.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))) continue;
                progress.Report(new($"Wird hinzugefügt: {Path.GetFileName(path)}"));
                if (audio && Directory.Exists(path)) throw new InvalidOperationException("Für eine Audio-CD bitte einzelne MP3- oder WAV-Dateien hinzufügen.");
                var item = audio ? AudioService.Inspect(path) : ProjectItem.FromData(path);
                if (!audio && target.Concat(additions).Any(i => i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Der Name „{item.Name}“ ist bereits auf der CD vorhanden. Bitte vorher umbenennen.");
                if (!additions.Any(i => i.SourcePath.Equals(item.SourcePath, StringComparison.OrdinalIgnoreCase))) additions.Add(item);
            }
            if (audio && target.Count + additions.Count > 99) throw new InvalidOperationException("Auf eine Audio-CD passen höchstens 99 Titel.");
            token.ThrowIfCancellationRequested();
            Dispatcher.Invoke(() => { foreach (var item in additions) target.Add(item); });
        }, "Zusammenstellung aktualisiert.", audio ? "Mit ↑ und ↓ kannst du die Reihenfolge ändern." : "Die Originaldateien bleiben an ihrem bisherigen Ort.", true);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in FileGrid.SelectedItems.Cast<ProjectItem>().ToArray()) Items.Remove(item);
        UpdateProject();
    }
    private void Clear_Click(object sender, RoutedEventArgs e) { Items.Clear(); UpdateProject(); }
    private void Up_Click(object sender, RoutedEventArgs e) => MoveTrack(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => MoveTrack(1);
    private void MoveTrack(int delta)
    {
        if (kind != ProjectKind.Audio || FileGrid.SelectedItems.Count != 1 || FileGrid.SelectedItem is not ProjectItem item) return;
        int index = audioItems.IndexOf(item), next = index + delta;
        if (next < 0 || next >= audioItems.Count) return;
        audioItems.Move(index, next);
        FileGrid.SelectedItem = item;
        FileGrid.ScrollIntoView(item);
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void Files_Drop(object sender, DragEventArgs e)
    {
        if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        if (kind == ProjectKind.Iso)
        {
            if (paths.Length == 1) SetIso(paths[0]);
            else ShowError(new InvalidOperationException("Bitte genau eine ISO-Datei auswählen."));
        }
        else await AddPaths(paths);
    }

    private void ChooseIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = L10n.T("ISO-Abbild auswählen"), Filter = L10n.T("ISO-Abbilder (*.iso)|*.iso") };
        if (dialog.ShowDialog(this) == true) SetIso(dialog.FileName);
    }
    private void SetIso(string path)
    {
        try
        {
            if (!File.Exists(path) || !Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bitte eine vorhandene .iso-Datei auswählen.");
            isoPath = Path.GetFullPath(path);
            IsoPathText.Text = isoPath + "\n" + Format.Bytes(new FileInfo(isoPath).Length);
            UpdateProject();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (dataItems.Count == 0 || busy) return;
        var dialog = new SaveFileDialog { Title = L10n.T("Daten-CD als ISO-Datei speichern"), FileName = L10n.T("Meine_CD.iso"), Filter = L10n.T("ISO-Abbild (*.iso)|*.iso"), DefaultExt = ".iso", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        string[] paths = dataItems.Select(i => i.SourcePath).ToArray();
        string name = VolumeNameBox.Text.Trim();
        await RunOperation((progress, token) => discs.ExportIso(paths, name, dialog.FileName, progress, token), "Deine ISO-Datei ist fertig.", dialog.FileName, true);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (busy || Recorder is not { } recorder) return;
        ProjectKind operation = kind;
        var options = new BurnOptions(recorder.Id, VolumeNameBox.Text.Trim(), VerifyCheck.IsChecked == true, EjectCheck.IsChecked == true, SpeedCombo.SelectedIndex == 1, FullEraseCheck.IsChecked == true);
        string[] paths = Items.Select(i => i.SourcePath).ToArray();
        string image = isoPath ?? "";
        string question = operation == ProjectKind.Erase
            ? $"Alle Inhalte auf der eingelegten CD-RW werden gelöscht.\n\nLaufwerk: {recorder.Name}\nMethode: {L10n.T(options.FullErase ? "Vollständig" : "Schnell")}\n\nCD-RW jetzt löschen?"
            : $"{(operation == ProjectKind.Audio ? L10n.T($"{paths.Length} Audiotitel") : operation == ProjectKind.Iso ? Path.GetFileName(image) : L10n.T($"{paths.Length} Dateien/Ordner"))} auf die eingelegte CD brennen?\n\nLaufwerk: {recorder.Name}\nDie CD wird danach abgeschlossen. Ein CD-R-Rohling lässt sich nicht löschen.";
        if (LocalizedDialog.Show(this, question, operation == ProjectKind.Erase ? "CD-RW löschen?" : "Bereit zum Brennen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RunOperation((progress, token) =>
        {
            switch (operation)
            {
                case ProjectKind.Data: discs.BurnData(paths, options, progress, token); break;
                case ProjectKind.Audio: discs.BurnAudio(paths, options, progress, token); break;
                case ProjectKind.Iso: discs.BurnIso(image, options, progress, token); break;
                case ProjectKind.Erase: discs.Erase(options, progress); break;
            }
        }, operation == ProjectKind.Erase ? "Die CD-RW ist wieder frei." : "Deine CD ist fertig.", operation == ProjectKind.Erase ? "Du kannst neue Dateien oder Musik auf die CD brennen." : operation == ProjectKind.Audio ? "Die Audio-CD wurde gebrannt und abgeschlossen." : options.Verify ? "Die Daten wurden gebrannt, abgeschlossen und von Windows vollständig zurückgelesen und geprüft." : "Die Daten wurden gebrannt und die CD wurde abgeschlossen.", operation != ProjectKind.Erase, true);
    }

    private async Task RunOperation(Action<IProgress<OperationProgress>, CancellationToken> work, string success, string detail, bool allowCancel, bool burns = false)
    {
        if (busy) return;
        busy = true;
        cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        Navigation.IsEnabled = Editor.IsEnabled = DriveCombo.IsEnabled = false;
        StartButton.Visibility = ExportButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Show(allowCancel);
        CancelButton.IsEnabled = true;
        OperationBar.Visibility = Visibility.Visible;
        OperationBar.IsIndeterminate = true;
        StatusTitle.Text = "Einen Moment …";
        StatusText.Text = "Der Vorgang wird vorbereitet.";
        UpdateButtons();
        var progress = new Progress<OperationProgress>(p =>
        {
            StatusText.Text = p.Message;
            OperationBar.IsIndeterminate = !p.Percent.HasValue;
            if (p.Percent.HasValue) OperationBar.Value = Math.Clamp(p.Percent.Value, 0, 100);
        });
        try
        {
            await Task.Run(() => work(progress, token));
            StatusTitle.Text = success;
            StatusText.Text = detail;
            OperationBar.IsIndeterminate = false;
            OperationBar.Value = 100;
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is COMException com && unchecked((uint)com.HResult) == 0xC0AA0002)
        {
            StatusTitle.Text = "Vorgang abgebrochen.";
            StatusText.Text = burns ? "Eine CD-R ist eventuell nicht mehr verwendbar. Eine CD-RW kannst du erneut löschen." : "Der Vorgang wurde beendet. Deine Zusammenstellung bleibt erhalten.";
            OperationBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { ShowError(ex); OperationBar.Visibility = Visibility.Collapsed; }
        finally
        {
            cancellation.Dispose(); cancellation = null;
            busy = false;
            Navigation.IsEnabled = Editor.IsEnabled = DriveCombo.IsEnabled = true;
            StartButton.Visibility = Visibility.Visible;
            ExportButton.Visibility = Show(kind == ProjectKind.Data);
            CancelButton.Visibility = Visibility.Collapsed;
            UpdateProject();
            if (burns) await ReadMedia();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        cancellation?.Cancel();
        CancelButton.IsEnabled = false;
        StatusTitle.Text = "Abbruch angefordert …";
        StatusText.Text = "Bitte warten, bis das Laufwerk den aktuellen Schritt sicher beendet hat.";
    }

    private void UpdateProject()
    {
        if (FileGrid == null) return;
        EmptyState.Visibility = Show(Items.Count == 0);
        ItemCount.Text = Items.Count == 0 ? "Noch keine " + (kind == ProjectKind.Audio ? "Titel" : "Dateien") : $"{Items.Count} " + (kind == ProjectKind.Audio ? "Titel · Reihenfolge von oben nach unten" : "Dateien / Ordner");
        bool audio = kind == ProjectKind.Audio;
        long used = audio ? ProjectFiles.AudioSectors(audioItems.Select(i => i.Duration)) : kind == ProjectKind.Iso ? SafeIsoLength() : dataItems.Sum(i => i.Bytes);
        long available = audio ? media?.AudioSectors ?? 360000 : media?.FreeBytes ?? DiscService.DefaultCdBytes;
        CapacityText.Text = audio ? $"{Format.Duration(TimeSpan.FromSeconds(used / 75d))} / {(int)(available / 75 / 60)} min" : $"{Format.Bytes(used)} / {Format.Bytes(available)}";
        CapacityBar.Value = available > 0 ? Math.Min(100, used * 100d / available) : used > 0 ? 100 : 0;
        bool over = used > available;
        CapacityBar.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(over ? "#B23A28" : "#7B9567")!;
        CapacityHint.Text = over ? "Zu viel für diese CD" : media == null ? audio ? "Richtwert inkl. Pausen · 80-Minuten-CD" : "Richtwert ohne Rohling · zzgl. Dateisystem" : audio ? "Inklusive ca. 2 s Pause je Titel" : "Zuzüglich Platz für das Dateisystem";
        UpdateButtons();
    }

    private long SafeIsoLength() { try { return isoPath == null ? 0 : new FileInfo(isoPath).Length; } catch (IOException) { return 0; } }
    private void UpdateButtons()
    {
        if (StartButton == null) return;
        bool ready = !busy && !refreshing && !readingMedia;
        RefreshButton.IsEnabled = ready;
        EjectButton.IsEnabled = ready && Recorder != null;
        ExportButton.IsEnabled = ready && dataItems.Count > 0;
        StartButton.IsEnabled = ready && Recorder != null && media != null && (kind == ProjectKind.Erase ? media.Type == 3 : media.Type is 2 or 3 && media.IsBlank && (kind == ProjectKind.Data ? dataItems.Count > 0 : kind == ProjectKind.Audio ? audioItems.Count > 0 : isoPath != null));
    }

    private void ShowError(Exception ex)
    {
        StatusTitle.Text = "Das hat noch nicht geklappt.";
        StatusText.Text = DiscService.DescribeError(ex);
        App.WriteLog(ex);
        if (IsVisible) LocalizedDialog.Show(this, StatusText.Text, "XD Brenner", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Help_Click(object sender, RoutedEventArgs e) => LocalizedDialog.Show(this,
        "XD Brenner 0.2 – freie Software unter der MIT-Lizenz\n\nDaten-CD: Dateien und Ordner hinzufügen und brennen. „Als ISO speichern“ funktioniert auch ohne Brenner.\n\nAudio-CD: MP3/WAV hinzufügen, Titel mit ↑/↓ ordnen und brennen. Ca. 2 Sekunden Pause zwischen Titeln; kein CD-Text oder Gapless.\n\nISO brennen: Eine Daten-ISO auswählen und brennen.\n\nCD-RW löschen: Schnell oder vollständig leeren. CD-Rs sind nicht löschbar.\n\nNach dem Einlegen einer CD bitte „Aktualisieren“ wählen. Es werden leere CD-R/CD-RW verwendet und abgeschlossene CDs erstellt. Kein Multisession, keine Kopierschutzumgehung.\n\nBrenner und Rohling müssen die gewählte Brennart unterstützen. Einen echten Brenntest am Zielgerät bitte zunächst mit CD-RW durchführen.\n\nKein Konto, keine Werbung, keine Telemetrie. Audioumwandlung mit NAudio (MIT). Fehlerprotokolle liegen lokal unter %LOCALAPPDATA%\\XDBurner\\Logs.\n\nQuellcode, Lizenz und Bauanleitung liegen im Projektordner.", "Über XD Brenner", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true;
        LocalizedDialog.Show(this, "Bitte den laufenden Vorgang beenden oder abbrechen, bevor du XD Brenner schließt.", "Vorgang läuft", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    internal void SetPreview(ProjectKind mode, bool populated)
    {
        var nav = mode switch { ProjectKind.Audio => AudioNav, ProjectKind.Iso => IsoNav, ProjectKind.Erase => EraseNav, _ => DataNav };
        nav.IsChecked = true;
        DriveCombo.ItemsSource = new[] { new RecorderInfo("", "Kein CD-Brenner gefunden") };
        DriveCombo.SelectedIndex = 0;
        MediaText.Text = "USB-CD-Brenner anschließen. ISO-Export ist auch ohne Brenner möglich.";
        if (populated && mode == ProjectKind.Data)
        {
            dataItems.Add(new("Vorschau/Urlaubsfotos", "Urlaubsfotos", true, 218_500_000, TimeSpan.Zero));
            dataItems.Add(new("Vorschau/Familienrezepte.pdf", "Familienrezepte.pdf", false, 3_200_000, TimeSpan.Zero));
            dataItems.Add(new("Vorschau/Geburtstag.mp4", "Geburtstag.mp4", false, 126_000_000, TimeSpan.Zero));
        }
        if (populated && mode == ProjectKind.Audio)
        {
            audioItems.Add(new("Vorschau/Mein Lieblingslied.mp3", "Mein Lieblingslied", false, 4_000_000, TimeSpan.FromSeconds(216)));
            audioItems.Add(new("Vorschau/Ein schöner Tag.wav", "Ein schöner Tag", false, 28_000_000, TimeSpan.FromSeconds(184)));
        }
        UpdateProject();
    }
    private static Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
