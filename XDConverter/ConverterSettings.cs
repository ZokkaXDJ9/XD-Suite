using System.IO;
using System.Text.Json;

namespace XDConverter;

public sealed class ConverterSettings
{
    public bool English { get; set; } = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName != "de";
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), BuildInfo.OutputFolder);
    public string Format { get; set; } = "MP3";
    public int Quality { get; set; }
    public int Rate { get; set; }
    public int Channels { get; set; }
    public bool Tags { get; set; } = true;
    public Dictionary<string, EncoderPreferences> Encoders { get; set; } = new();
    public int AutomaticDefaultsVersion { get; set; }
    public EncoderPreferences ForFormat(string format)
    {
        UpgradeAutomaticDefaults();
        Encoders ??= new();
        if (Encoders.TryGetValue(format, out var result)) return result;
        result = new();
        // Import preferences saved by the first local preview without changing folder or language.
        if (format == Format)
        {
            result.Rate = Rate; result.Channels = Channels;
            if (format == "WAV") result.Depth = Quality;
            else if (format != "FLAC")
            {
                result.Bitrate = Quality;
                if (Quality > 0) result.Mode = EncodingProfile.Modes(format)[0];
            }
        }
        Encoders[format] = result;
        return result;
    }
    private void UpgradeAutomaticDefaults()
    {
        if (AutomaticDefaultsVersion >= 1) return;
        // Old previews persisted their fixed defaults as if the user had selected them.
        // Translate those defaults once, retaining non-default manual selections and all other preferences.
        foreach (var pair in Encoders ?? new())
        {
            var p = pair.Value;
            if (!OutputFormat.All.Any(f => f.Id == pair.Key)) continue;
            if (p.Mode == EncodingProfile.Modes(pair.Key)[0] && p.Bitrate is 0 or 192 &&
                (p.VbrQuality == EncodingProfile.AutomaticQuality || p.VbrQuality == EncodingProfile.DefaultQuality(pair.Key))) p.Mode = EncodingMode.Automatic;
            if (p.Bitrate == 192) p.Bitrate = 0;
            if (p.VbrQuality == EncodingProfile.DefaultQuality(pair.Key)) p.VbrQuality = EncodingProfile.AutomaticQuality;
            if (p.Depth == 16) p.Depth = 0;
            if (p.Compression == 5) p.Compression = -1;
            if (p.Application == 0) p.Application = -1;
        }
        if (Format == "WAV" && Quality == 16 || Format != "WAV" && Quality == 192) Quality = 0;
        AutomaticDefaultsVersion = 1;
    }
    public void UseAutomatic(string format)
    {
        UpgradeAutomaticDefaults(); Encoders ??= new(); Encoders[format] = new();
    }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BuildInfo.DataFolder);
    public static ConverterSettings Load()
    {
        try { return JsonSerializer.Deserialize<ConverterSettings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))) ?? new(); }
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
    public EncodingMode Mode { get; set; } = EncodingMode.Automatic;
    public int Bitrate { get; set; }
    public int VbrQuality { get; set; } = EncodingProfile.AutomaticQuality;
    public int Rate { get; set; }
    public int Channels { get; set; }
    public int Depth { get; set; }
    public int Compression { get; set; } = -1;
    public int FlacDepth { get; set; }
    public int Application { get; set; } = -1;
}
