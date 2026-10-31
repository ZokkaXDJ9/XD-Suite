using System.ComponentModel;
using System.Globalization;

namespace XDConverter;

public sealed class Texts : INotifyPropertyChanged
{
    public static Texts Current { get; } = new();
    public bool English { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;
    private static readonly Dictionary<string, (string De, string En)> values = new()
    {
        ["Title"] = ("XD Konverter", "XD Converter"), ["Preview"] = ("Vorschau · {0} · Test: Automatik", "Preview · {0} · Test: automatic settings"),
        ["Advanced"] = ("Weitere Einstellungen", "More settings"),
        ["AutomaticSettings"] = ("Einstellungen: Automatisch", "Settings: Automatic"),
        ["ManualSettings"] = ("Einstellungen: Angepasst", "Settings: Custom"),
        ["AutomaticIntro"] = ("Zielformat wählen und konvertieren. Die Audioeinstellungen werden automatisch gewählt; Änderungen sind optional.", "Choose a format and convert. Audio settings are selected automatically; changes are optional."),
        ["ManualIntro"] = ("Manuell geänderte Werte werden verwendet. Alle übrigen Audioeinstellungen bleiben automatisch.", "Your manual choices are used. All other audio settings remain automatic."),
        ["UseAutomatic"] = ("Automatisch verwenden", "Use automatic settings"),
        ["EncodingMode"] = ("Codierung", "Encoding"), ["Cbr"] = ("CBR · konstante Bitrate", "CBR · constant bitrate"),
        ["Abr"] = ("ABR · mittlere Zielbitrate", "ABR · average target bitrate"), ["Vbr"] = ("VBR · variable Bitrate", "VBR · variable bitrate"),
        ["ConstrainedVbr"] = ("CVBR · begrenzte variable Bitrate", "CVBR · constrained variable bitrate"),
        ["Pcm"] = ("PCM · unkomprimiert", "PCM · uncompressed"), ["Lossless"] = ("Verlustfrei", "Lossless"),
        ["Bitrate"] = ("Bitrate", "Bitrate"), ["TargetBitrate"] = ("Zielbitrate", "Target bitrate"), ["VbrQuality"] = ("VBR-Qualität", "VBR quality"),
        ["BitDepth"] = ("Bittiefe", "Bit depth"), ["Compression"] = ("Kompressionsstufe", "Compression level"),
        ["Fastest"] = (" · schnell", " · fast"), ["Smallest"] = (" · stärkste Kompression", " · maximum compression"),
        ["Depth8"] = ("8 Bit PCM", "8-bit PCM"), ["Depth32"] = ("32 Bit PCM", "32-bit PCM"), ["Float64"] = ("64 Bit Float", "64-bit float"),
        ["Application"] = ("Verwendung", "Application"), ["Music"] = ("Musik / allgemein", "Music / general"), ["Speech"] = ("Sprache", "Speech"), ["LowDelay"] = ("Geringe Verzögerung", "Low latency"),
        ["Mp3VbrHint"] = ("V0 = höchste, V9 = niedrigste Qualität. Die Bitrate richtet sich nach dem Inhalt.", "V0 = highest, V9 = lowest quality. Bitrate depends on the content."),
        ["VbrHint"] = ("Höhere Q-Werte erhöhen die Qualität und meist die Dateigröße. Die Bitrate richtet sich nach dem Inhalt.", "Higher Q values increase quality and usually file size. Bitrate depends on the content."),
        ["BitrateHint"] = ("Höhere Bitraten benötigen mehr Speicher. Die Auswahl hängt von Abtastrate und Kanälen ab; für hohe AAC-/OGG-/Opus-Bitraten Stereo wählen. Automatisch passt die Abtastrate an den Encoder an.", "Higher bitrates use more space. Choices depend on sample rate and channels; select Stereo for high AAC/OGG/Opus bitrates. Automatic adapts the sample rate to the encoder."),
        ["PcmHint"] = ("Mehr Bit oder eine höhere Abtastrate stellen verlorene Quelldetails nicht wieder her.", "More bits or a higher sample rate cannot restore detail already lost in the source."),
        ["FlacHint"] = ("Alle Kompressionsstufen sind verlustfrei. Höhere Stufen benötigen mehr Rechenzeit. Eine niedrigere Bittiefe oder andere Abtastrate verändert die Audiodaten.", "All compression levels are lossless. Higher levels take longer. Reducing bit depth or changing the sample rate changes the audio data."),
        ["OpusHint"] = ("Die Abtastrate steuert den Encoder-Eingang. Opus wird beim Auslesen immer als 48 kHz angezeigt.", "Sample rate controls the encoder input. Opus is always reported as 48 kHz when inspected."),
        ["InvalidOptions"] = ("Diese Kombination von Audioeinstellungen wird vom Zielformat nicht unterstützt.", "The target format does not support this combination of audio settings."),
        ["Intro"] = ("Audiodateien in ein anderes Format umwandeln.", "Convert audio files to another format."),
        ["Files"] = ("Dateien", "Files"), ["Name"] = ("DATEI", "FILE"), ["Duration"] = ("LÄNGE", "DURATION"), ["Source"] = ("QUELLFORMAT", "SOURCE FORMAT"), ["Status"] = ("STATUS", "STATUS"),
        ["Add"] = ("Dateien hinzufügen", "Add files"), ["Folder"] = ("Ordner hinzufügen", "Add folder"), ["Remove"] = ("Entfernen", "Remove"), ["Clear"] = ("Leeren", "Clear"),
        ["Empty"] = ("Audiodateien hierher ziehen", "Drop audio files here"), ["InputFormats"] = ("MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA, AIFF …", "MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA, AIFF …"),
        ["Options"] = ("Ausgabe", "Output"), ["Format"] = ("Zielformat", "Target format"), ["Quality"] = ("Qualität", "Quality"), ["Rate"] = ("Abtastrate", "Sample rate"), ["Channels"] = ("Kanäle", "Channels"),
        ["Automatic"] = ("Automatisch", "Automatic"), ["Original"] = ("Wie Quelle", "Same as source"), ["Mono"] = ("Mono", "Mono"), ["Stereo"] = ("Stereo", "Stereo"), ["Default"] = ("Standard", "Standard"),
        ["Tags"] = ("Text-Tags übernehmen (ohne Coverbilder)", "Keep text tags (without cover art)"), ["OutputFolder"] = ("Zielordner", "Output folder"), ["Browse"] = ("Auswählen …", "Browse …"),
        ["Protect"] = ("Originaldateien bleiben erhalten. Bei gleichem Namen erhält das Ergebnis einen neuen Dateinamen.", "Original files are retained. A new name is used when the destination already exists."),
        ["Start"] = ("Konvertieren", "Convert"), ["Cancel"] = ("Abbrechen", "Cancel"), ["OpenFolder"] = ("Zielordner öffnen", "Open output folder"), ["Ready"] = ("Bereit", "Ready"),
        ["Checking"] = ("Dateien werden geprüft …", "Checking files …"), ["Converting"] = ("Konvertierung läuft …", "Converting …"), ["Canceling"] = ("Abbruch angefordert …", "Cancelling …"),
        ["Done"] = ("Abgeschlossen", "Complete"), ["Canceled"] = ("Abgebrochen", "Cancelled"), ["Failed"] = ("Fehlgeschlagen", "Failed"), ["Queued"] = ("Bereit", "Ready"), ["Skipped"] = ("Übersprungen", "Skipped"),
        ["Count"] = ("{0} Dateien", "{0} files"), ["Added"] = ("{0} hinzugefügt · {1} übersprungen", "{0} added · {1} skipped"), ["Summary"] = ("{0} fertig · {1} fehlgeschlagen · {2} abgebrochen", "{0} completed · {1} failed · {2} cancelled"),
        ["Current"] = ("Datei {0} von {1}: {2}", "File {0} of {1}: {2}"), ["EngineMissing"] = ("Die Konvertierungskomponenten fehlen. Bitte das Programm mit dem vollständigen Setup neu installieren.", "Conversion components are missing. Reinstall the application using the complete setup."),
        ["EncoderMissing"] = ("Ein benötigter Audio-Encoder fehlt.", "A required audio encoder is missing."), ["FileMissing"] = ("Die Quelldatei wurde nicht gefunden.", "The source file was not found."),
        ["AudioOnly"] = ("Es werden nur Audiodateien unterstützt.", "Only audio files are supported."), ["NoAudio"] = ("Die Datei enthält keine lesbare Audiospur.", "The file contains no readable audio track."),
        ["EmptyOutput"] = ("Es wurde keine vollständige Audiodatei erzeugt.", "No complete audio file was created."),
        ["NameCollision"] = ("Es konnte kein freier Dateiname gefunden werden.", "No unused output filename could be found."), ["EngineFailed"] = ("Die Datei konnte nicht verarbeitet werden.", "The file could not be processed."),
        ["Error"] = ("Vorgang fehlgeschlagen", "Operation failed"), ["CloseQuestion"] = ("Den laufenden Vorgang abbrechen und das Programm schließen? Bereits fertige Dateien bleiben erhalten.", "Cancel the current operation and close? Completed files will be retained."),
        ["Yes"] = ("Ja", "Yes"), ["No"] = ("Nein", "No"), ["ChooseAudio"] = ("Audiodateien auswählen", "Select audio files"), ["ChooseFolder"] = ("Ordner mit Audiodateien auswählen", "Select a folder containing audio files"),
        ["ChooseOutput"] = ("Zielordner auswählen", "Select output folder"), ["AudioFilter"] = ("Audiodateien|*.mp3;*.wav;*.wave;*.flac;*.ogg;*.m4a;*.aac;*.opus;*.wma;*.aif;*.aiff;*.aifc;*.ape;*.wv;*.mp2;*.amr;*.ac3;*.dts;*.caf;*.au|Alle Dateien|*.*", "Audio files|*.mp3;*.wav;*.wave;*.flac;*.ogg;*.m4a;*.aac;*.opus;*.wma;*.aif;*.aiff;*.aifc;*.ape;*.wv;*.mp2;*.amr;*.ac3;*.dts;*.caf;*.au|All files|*.*"),
        ["FolderLimit"] = ("Bitte höchstens 10.000 Dateien auf einmal hinzufügen.", "Add at most 10,000 files at a time."), ["OutputRequired"] = ("Bitte einen Zielordner auswählen.", "Select an output folder."),
        ["LocalPreview"] = ("Lokale Vorschau", "Local preview"), ["Details"] = ("Fehlerdetails stehen in der Statusspalte als Hinweistext.", "Hover over the status column to read error details."),
        ["Float"] = ("32 Bit Float", "32-bit float"), ["Help"] = ("Hinweise", "Notes"),
        ["Depth16"] = ("16 Bit PCM", "16-bit PCM"), ["Depth24"] = ("24 Bit PCM", "24-bit PCM"),
        ["ReadyHint"] = ("Dateien hinzufügen und Zielformat wählen.", "Add files and choose a target format."),
        ["HelpText"] = ("Audiodateien werden lokal verarbeitet. Für eine normale Konvertierung reichen Dateien, Zielformat und Zielordner. Weitere Einstellungen sind optional.\n\nFertige Dateien werden im Zielordner gespeichert. Vorhandene Dateien werden nicht überschrieben. Ein Abbruch behält fertige Ergebnisse und entfernt unvollständige Ausgabedateien.\n\nMP3, OGG, M4A und Opus verwenden verlustbehaftete Kompression. WAV bietet 8/16/24/32 Bit PCM und 32/64 Bit Float. Text-Tags werden soweit vom Zielformat unterstützt übernommen; Coverbilder werden derzeit nicht kopiert. Automatisch kann die Abtastrate für den gewählten Encoder anpassen. Mehrkanalton wird bei MP3/OGG/M4A/Opus automatisch auf Stereo gemischt, wenn keine Kanalzahl vorgegeben wurde.", "Audio is processed locally. Files, target format and output folder are sufficient for a normal conversion. More settings are optional.\n\nCompleted files are saved in the output folder. Existing files are never overwritten. Cancellation retains completed results and removes incomplete output.\n\nMP3, OGG, M4A and Opus use lossy compression. WAV offers 8/16/24/32-bit PCM and 32/64-bit float. Text tags are preserved where supported; cover art is not currently copied. Automatic sample rate may adapt to the encoder. Multichannel input is mixed to stereo for MP3/OGG/M4A/Opus when no channel count is specified.")
    };
    public string this[string key]
    {
        get
        {
            string value = values.TryGetValue(key, out var text) ? English ? text.En : text.De : key;
            if (BuildInfo.IsBeta && key == "Title") return value + " Beta";
            if (BuildInfo.IsBeta && key == "Preview") return "Beta · " + typeof(App).Assembly.GetName().Version!.ToString(3) + "-Beta";
            if (key == "Preview") return BuildInfo.IsPreview ? string.Format(value, typeof(App).Assembly.GetName().Version!.ToString(3)) : "Audio · " + typeof(App).Assembly.GetName().Version!.ToString(3);
            return value;
        }
    }
    public string F(string key, params object[] args) => string.Format(English ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("de-DE"), this[key], args);
    public void Set(bool english) { English = english; PropertyChanged?.Invoke(this, new("Item[]")); Changed?.Invoke(this, EventArgs.Empty); }
    public string ErrorText(Exception error) => error is ConversionError e ? this[e.Key] + (e.Message.Length > 0 ? "\n" + e.Message : "") : error.Message;
}

public sealed class AudioRow : INotifyPropertyChanged
{
    public required string Path { get; init; }
    public AudioInfo? Info { get; set; }
    public string State { get; set; } = "Queued";
    public string Error { get; set; } = "";
    public Exception? Failure { get; set; }
    public string Output { get; set; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Duration => Info == null ? "—" : Info.Duration.TotalHours >= 1 ? Info.Duration.ToString(@"h\:mm\:ss") : Info.Duration.ToString(@"mm\:ss");
    public string Source => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant() + (Info == null ? "" : " · " + (Info.SampleRate / 1000d).ToString("0.#", CultureInfo.GetCultureInfo(Texts.Current.English ? "en-US" : "de-DE")) + " kHz");
    public string Status => Texts.Current[State];
    public string Detail => Failure != null ? Texts.Current.ErrorText(Failure) : Error.Length > 0 ? Error : Output.Length > 0 ? Output : Path;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(""));
}
