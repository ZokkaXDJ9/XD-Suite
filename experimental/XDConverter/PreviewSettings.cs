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
