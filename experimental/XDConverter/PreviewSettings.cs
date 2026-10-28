using System.IO;
using System.Text.Json;

namespace XDConverter;

public sealed class PreviewSettings
{
    public bool English { get; set; } = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName != "de";
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "XD Konverter Vorschau");
    public string Format { get; set; } = "MP3";
    public int Quality { get; set; } = 192;
    public int Rate { get; set; }
    public int Channels { get; set; }
    public bool Tags { get; set; } = true;
    public Dictionary<string, EncoderPreferences> Encoders { get; set; } = new();
    public EncoderPreferences ForFormat(string format)
    {
        Encoders ??= new();
        if (Encoders.TryGetValue(format, out var result)) return result;
        result = new() { Mode = EncodingProfile.Modes(format)[0], VbrQuality = EncodingProfile.DefaultQuality(format) };
        // Import preferences saved by the first local preview without changing folder or language.
        if (format == Format)
        {
            result.Rate = Rate; result.Channels = Channels;
            if (format == "WAV") result.Depth = Quality;
            else if (format != "FLAC") result.Bitrate = Quality;
        }
        Encoders[format] = result;
        return result;
    }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDConverterPreview");
    public static PreviewSettings Load()
    {
        try { return JsonSerializer.Deserialize<PreviewSettings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))) ?? new(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        string temporary = Path.Combine(DataDirectory, "settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, Path.Combine(DataDirectory, "settings.json"), true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class EncoderPreferences
{
    public EncodingMode Mode { get; set; }
    public int Bitrate { get; set; } = 192;
    public int VbrQuality { get; set; } = 2;
    public int Rate { get; set; }
    public int Channels { get; set; }
    public int Depth { get; set; } = 16;
    public int Compression { get; set; } = 5;
    public int FlacDepth { get; set; }
    public int Application { get; set; }
}
