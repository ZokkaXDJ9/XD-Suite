using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace XDWaveEditor;

public sealed class Texts : INotifyPropertyChanged
{
    public static Texts Current { get; } = new();
    public bool English { get; private set; }
    public CultureInfo Culture => CultureInfo.GetCultureInfo(English ? "en-US" : "de-DE");
    public string this[string key] => Values.TryGetValue(key, out var value) ? English ? value.En : value.De : key;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Set(bool english, bool persist = false)
    {
        English = english; CultureInfo.CurrentCulture = Culture; CultureInfo.CurrentUICulture = Culture;
        PropertyChanged?.Invoke(this, new("Item[]"));
        if (persist) { Directory.CreateDirectory(DataDirectory); File.WriteAllText(Path.Combine(DataDirectory, "language.txt"), english ? "en" : "de"); }
    }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDWaveEditorPreview");
    public void Load() { try { Set(File.ReadAllText(Path.Combine(DataDirectory, "language.txt")) == "en"); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    public string Error(Exception error) => error is EditorError e ? this[e.Key] + (string.IsNullOrWhiteSpace(e.Message) ? "" : "\n\n" + e.Message) : this["Failed"] + "\n\n" + error.Message;
    public static string Time(long frames, int rate) => TimeSpan.FromSeconds(frames / (double)rate).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    private static readonly Dictionary<string, (string De, string En)> Values = new()
    {
        ["Title"] = ("XD Wave Editor · Vorschau", "XD Wave Editor · Preview"), ["Preview"] = ("Vorschau · 0.5.1 · Teststand 1", "Preview · 0.5.1 · Build 1"),
        ["File"] = ("Datei", "File"), ["Edit"] = ("Bearbeiten", "Edit"), ["Open"] = ("Audio öffnen …", "Open audio …"), ["Export"] = ("Exportieren …", "Export …"), ["Exit"] = ("Beenden", "Exit"),
        ["Undo"] = ("Rückgängig", "Undo"), ["Redo"] = ("Wiederholen", "Redo"), ["Delete"] = ("Auswahl löschen", "Delete selection"), ["Trim"] = ("Nur Auswahl behalten", "Keep selection only"), ["SelectAll"] = ("Alles auswählen", "Select all"),
        ["Cut"] = ("Ausschneiden", "Cut"), ["Copy"] = ("Kopieren", "Copy"), ["Paste"] = ("Einfügen", "Paste"),
        ["EditHint"] = ("Bereich markieren und bearbeiten. Als neue Datei exportieren.", "Select a region to edit. Export to a new file."),
        ["ClipboardEmpty"] = ("Noch kein Audio kopiert.", "No audio copied yet."), ["Clipboard"] = ("Kopiertes Audio: {0:0.000} s", "Copied audio: {0:0.000} s"),
        ["Play"] = ("Wiedergabe", "Play"), ["Pause"] = ("Pause", "Pause"), ["Stop"] = ("Stopp", "Stop"), ["PlayHint"] = ("Auswahl oder ab Cursor abspielen · Leertaste", "Play selection or from cursor · Space"),
        ["ZoomIn"] = ("Vergrößern", "Zoom in"), ["ZoomOut"] = ("Verkleinern", "Zoom out"), ["Fit"] = ("Ganze Datei", "Fit audio"), ["WaveHint"] = ("Ziehen: Bereich auswählen · Klicken: Cursor setzen · Mausrad: Zoom", "Drag to select · Click to place cursor · Mouse wheel to zoom"),
        ["Empty"] = ("Audiodatei öffnen oder hier ablegen", "Open an audio file or drop it here"), ["Formats"] = ("MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA, AIFF · Mono / Stereo", "MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA, AIFF · Mono / stereo"),
        ["Ready"] = ("Bereit", "Ready"), ["Loading"] = ("Audio wird geladen …", "Loading audio …"), ["Exporting"] = ("Audio wird exportiert …", "Exporting audio …"), ["Preparing"] = ("Wiedergabe wird vorbereitet …", "Preparing playback …"),
        ["Cancelled"] = ("Vorgang abgebrochen.", "Operation cancelled."), ["Saved"] = ("Export gespeichert: ", "Export saved: "), ["Cancel"] = ("Abbrechen", "Cancel"),
        ["Duration"] = ("Länge", "Duration"), ["Selection"] = ("Auswahl", "Selection"), ["Channels"] = ("Kanäle", "channels"), ["Changed"] = ("Bearbeitet", "Edited"), ["Unchanged"] = ("Unverändert", "Unchanged"),
        ["Unsaved"] = ("Es gibt nicht exportierte Änderungen. Trotzdem fortfahren?", "There are changes that have not been exported. Continue anyway?"), ["Busy"] = ("Bitte den laufenden Vorgang zuerst abschließen oder abbrechen.", "Please complete or cancel the current operation first."),
        ["Failed"] = ("Der Vorgang konnte nicht abgeschlossen werden.", "The operation could not be completed."), ["EngineMissing"] = ("Die Audio-Engine fehlt. Bitte die Vorschau vollständig entpacken oder neu installieren.", "The audio engine is missing. Please extract the complete preview or reinstall it."),
        ["InvalidAudio"] = ("Die Datei enthält keine lesbaren Audiodaten.", "The file contains no readable audio."), ["AudioOnly"] = ("Bitte eine unterstützte Audiodatei auswählen.", "Please choose a supported audio file."), ["StereoOnly"] = ("Diese Vorschau unterstützt Mono- und Stereodateien.", "This preview supports mono and stereo files."),
        ["OriginalProtected"] = ("Die Originaldatei wird nicht überschrieben. Bitte einen anderen Namen wählen.", "The original file will not be overwritten. Please choose another name."),
        ["TargetExists"] = ("Die Zieldatei existiert bereits.", "The output file already exists."), ["UnsupportedExport"] = ("Bitte WAV, MP3, FLAC, OGG, M4A oder Opus als Dateiendung verwenden.", "Please use WAV, MP3, FLAC, OGG, M4A or Opus as the file extension."),
        ["EmptySelection"] = ("Es sind keine Audiodaten zum Exportieren vorhanden.", "There is no audio to export."), ["PreviewTooLong"] = ("Diese Datei ist zu groß für die Wiedergabevorschau. Bitte zuerst kürzen.", "This file is too large for playback preview. Please trim it first."),
        ["EngineFailed"] = ("Die Audio-Engine konnte die Datei nicht verarbeiten.", "The audio engine could not process the file."), ["OneFile"] = ("Bitte jeweils eine Audiodatei öffnen.", "Please open one audio file at a time."),
        ["PlaybackFailed"] = ("Die Wiedergabe konnte nicht gestartet werden. Bitte das Windows-Ausgabegerät prüfen.", "Playback could not start. Please check the Windows output device.")
    };
}
