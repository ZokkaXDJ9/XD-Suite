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
        ["Title"] = ("XD Wave Editor · Vorschau", "XD Wave Editor · Preview"), ["Preview"] = ("Vorschau · 0.5.1 · Teststand 2", "Preview · 0.5.1 · Build 2"),
        ["Tools"] = ("Effekte & Werkzeuge …", "Effects & tools …"), ["EffectsMenu"] = ("Effekte", "Effects"), ["ApplyEffect"] = ("Anwenden", "Apply"), ["Defaults"] = ("Standardwerte", "Defaults"),
        ["Processing"] = ("Audio wird bearbeitet …", "Processing audio …"), ["AllowClipping"] = ("Übersteuerung zulassen", "Allow clipping"), ["RemoveDc"] = ("Gleichspannungsanteil entfernen", "Remove DC offset"),
        ["WholeAudio"] = ("Wirkt auf die gesamte Datei.", "Applies to all audio."), ["SelectedAudio"] = ("Wirkt auf die Auswahl:", "Applies to selection:"), ["InsertAtCursor"] = ("Einfügen am Cursor:", "Insert at cursor:"),
        ["WouldClip"] = ("Diese Einstellung würde übersteuern. Bitte die Verstärkung reduzieren oder Übersteuerung ausdrücklich zulassen.", "These settings would cause clipping. Reduce the gain or explicitly allow clipping."),
        ["InvalidEffect"] = ("Bitte gültige Werte innerhalb der angezeigten Bereiche eingeben.", "Please enter valid values within the displayed ranges."), ["DocumentChanged"] = ("Die Audiodatei wurde während der Bearbeitung geändert. Bitte erneut versuchen.", "The audio changed during processing. Please try again."),
        ["InsertAudio"] = ("Audiodatei einfügen …", "Insert audio file …"), ["PreciseSelection"] = ("Auswahl genau festlegen …", "Set precise selection …"), ["SetSelection"] = ("Auswahl setzen", "Set selection"),
        ["TimeHint"] = ("Zeit als hh:mm:ss.fff oder in Sekunden eingeben.", "Enter time as hh:mm:ss.fff or in seconds."), ["StartTime"] = ("Anfang", "Start"), ["EndTime"] = ("Ende", "End"),
        ["InvalidTime"] = ("Bitte einen gültigen Bereich innerhalb der Audiodatei eingeben. Das Ende muss nach dem Anfang liegen oder ihm entsprechen.", "Enter a valid range within the audio. The end must be at or after the start."), ["Loop"] = ("Wiedergabe wiederholen", "Loop playback"),
        ["FxNormalize"] = ("Normalisieren", "Normalize"), ["FxNormalizeHelp"] = ("Passt den Spitzenpegel an. Das Lautstärkeverhältnis der Stereokanäle bleibt erhalten. Der Gleichspannungsanteil kann entfernt werden.", "Adjusts the peak level while preserving stereo balance. DC offset can also be removed."),
        ["FxGain"] = ("Lauter / leiser", "Amplify / reduce volume"), ["FxGainHelp"] = ("Ändert die Lautstärke um den eingestellten Wert. Positive Werte machen lauter, negative leiser.", "Changes volume by the chosen amount. Positive values increase volume; negative values reduce it."),
        ["FxFadeIn"] = ("Einblenden", "Fade in"), ["FxFadeInHelp"] = ("Blendet über den gesamten gewählten Bereich von Stille auf die vorhandene Lautstärke ein.", "Fades from silence to the existing level across the selected region."),
        ["FxFadeOut"] = ("Ausblenden", "Fade out"), ["FxFadeOutHelp"] = ("Blendet über den gesamten gewählten Bereich bis zur Stille aus.", "Fades the selected region from its existing level to silence."),
        ["FxMute"] = ("Stummschalten", "Silence selection"), ["FxMuteHelp"] = ("Ersetzt den Ton durch Stille. Die Länge des Bereichs bleibt erhalten.", "Replaces audio with silence without changing its duration."),
        ["FxReverse"] = ("Rückwärts abspielen", "Reverse"), ["FxReverseHelp"] = ("Dreht die Reihenfolge der Audiodaten um. Links und rechts bleiben ihren Kanälen zugeordnet.", "Reverses the audio while keeping left and right channels in place."),
        ["FxInvert"] = ("Polarität umkehren", "Invert polarity"), ["FxInvertHelp"] = ("Kehrt die Polarität beider Kanäle um. Für sich allein bleibt der Klang normalerweise gleich.", "Inverts the polarity of both channels. Heard on its own, the sound usually stays the same."),
        ["FxRepeat"] = ("Bereich wiederholen", "Repeat region"), ["FxRepeatHelp"] = ("Wiederholt den Bereich direkt hintereinander. Die Anzahl schließt das erste Vorkommen ein.", "Repeats the region consecutively. The count includes the first occurrence."),
        ["FxInsertSilence"] = ("Stille einfügen", "Insert silence"), ["FxInsertSilenceHelp"] = ("Fügt am Cursor eine Pause ein. Vorhandenes Audio wird nach hinten verschoben.", "Inserts a pause at the cursor and moves following audio later."),
        ["FxTempo"] = ("Tempo ändern", "Change tempo"), ["FxTempoHelp"] = ("100 % ist das ursprüngliche Tempo. Größere Werte machen schneller und kürzer; die Tonhöhe bleibt erhalten.", "100% is the original tempo. Higher values make audio faster and shorter while preserving pitch."),
        ["FxPitch"] = ("Tonhöhe ändern", "Change pitch"), ["FxPitchHelp"] = ("Verschiebt die Tonhöhe in Halbtönen. Die Länge bleibt erhalten; große Änderungen können hörbare Artefakte verursachen.", "Shifts pitch in semitones while preserving duration. Large shifts can cause audible artifacts."),
        ["FxEqualizer"] = ("Equalizer: Bass, Mitten, Höhen", "Equalizer: bass, mids, treble"), ["FxEqualizerHelp"] = ("Hebt tiefe, mittlere oder hohe Frequenzen an oder senkt sie ab. 0 dB lässt das jeweilige Band unverändert.", "Boosts or cuts low, mid or high frequencies. 0 dB leaves a band unchanged."),
        ["FxHighPass"] = ("Hochpass: tiefe Frequenzen absenken", "High-pass: reduce low frequencies"), ["FxHighPassHelp"] = ("Senkt tiefe Frequenzen unterhalb der Grenzfrequenz ab, etwa Trittschall und Rumpeln.", "Reduces frequencies below the cutoff, such as handling noise and rumble."),
        ["FxLowPass"] = ("Tiefpass: hohe Frequenzen absenken", "Low-pass: reduce high frequencies"), ["FxLowPassHelp"] = ("Senkt hohe Frequenzen oberhalb der Grenzfrequenz ab.", "Reduces frequencies above the cutoff."),
        ["FxDenoise"] = ("Rauschminderung", "Noise reduction"), ["FxDenoiseHelp"] = ("Verringert gleichmäßiges Hintergrundrauschen. Starke Einstellungen können den Klang verändern; mit niedrigen Werten beginnen.", "Reduces steady background noise. Strong settings can alter the sound; start with low values."),
        ["FxCompressor"] = ("Kompressor", "Compressor"), ["FxCompressorHelp"] = ("Verringert den Abstand zwischen lauten und leisen Stellen, indem Pegel oberhalb der Schwelle abgesenkt werden.", "Reduces level differences by turning down audio above the threshold."),
        ["TargetPeak"] = ("Ziel-Spitzenpegel", "Target peak"), ["GainAmount"] = ("Lautstärkeänderung", "Volume change"), ["Copies"] = ("Anzahl insgesamt", "Total copies"), ["SilenceLength"] = ("Dauer der Pause", "Pause duration"),
        ["TempoPercent"] = ("Tempo", "Tempo"), ["Semitones"] = ("Halbtöne", "Semitones"), ["Bass"] = ("Bass", "Bass"), ["Mids"] = ("Mitten", "Mids"), ["Treble"] = ("Höhen", "Treble"),
        ["Cutoff"] = ("Grenzfrequenz", "Cutoff frequency"), ["NoiseReduction"] = ("Rauschminderung", "Noise reduction"), ["NoiseFloor"] = ("Geschätzter Rauschpegel", "Estimated noise floor"),
        ["Threshold"] = ("Schwelle", "Threshold"), ["Ratio"] = ("Verhältnis", "Ratio"), ["Attack"] = ("Ansprechzeit", "Attack"), ["Release"] = ("Rücklaufzeit", "Release"),
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
