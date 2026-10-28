using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using XD.Common;

namespace XDSuite;

public sealed record SuiteUpdate(AppPackage Package, string CurrentVersion);

public static class SuiteUpdates
{
    public static Version CurrentVersion => typeof(SuiteUpdates).Assembly.GetName().Version ?? new Version(0, 0, 0);
    public static string VersionText => CurrentVersion.ToString(3);

    public static async Task<SuiteUpdate?> Check(PackageTransport transport, string repository, Version installed, CancellationToken token)
    {
        SuiteSettings.ValidateRepository(repository);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        string json = await ReadText(transport, new($"https://api.github.com/repos/{repository}/releases/latest"), 1_048_576, timeout.Token);
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        string version = (release.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(\.\d+)?$") || !Version.TryParse(version, out var available)) throw new InvalidDataException("Die Update-Versionsnummer ist ungültig.");
        if (Normalize(available) <= Normalize(installed)) return null;
        string filename = $"XD-Suite-Setup-{version}.exe";
        var assets = release.GetProperty("assets").EnumerateArray().ToArray();
        var setup = assets.SingleOrDefault(a => a.GetProperty("name").GetString() == filename);
        var checksums = assets.SingleOrDefault(a => a.GetProperty("name").GetString() == "SHA256SUMS.txt");
        if (setup.ValueKind == JsonValueKind.Undefined || checksums.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("Das Suite-Update enthält kein vollständiges Setup mit Prüfsumme.");
        string sums = await ReadText(transport, new(checksums.GetProperty("browser_download_url").GetString()!), 65536, timeout.Token);
        string? checksum = null;
        foreach (string line in sums.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*') == filename && Regex.IsMatch(parts[0], "^[A-Fa-f0-9]{64}$")) checksum = parts[0];
        }
        if (checksum == null) throw new InvalidDataException("Die Prüfsumme für das Suite-Update fehlt.");
        var package = new AppPackage("xd-suite", new("XD Suite", "XD Suite"), new("Programmverwaltung", "Application manager"), new("Programme", "Applications"), version,
            setup.GetProperty("browser_download_url").GetString()!, checksum, setup.GetProperty("size").GetInt64(), "XDSuite.exe", "{F80CC53F-43F1-4E46-B7F4-61435E75303D}_is1", "XDSuite", []);
        PackageValidation.Parse(JsonSerializer.Serialize(new Catalog(1, [package]), PackageValidation.JsonOptions));
        return new(package, installed.ToString(3));
    }

    public static void StartInstallerAfterExit(string installer, AppPackage package)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "XDUpdateHelper.exe");
        if (!File.Exists(source)) throw new FileNotFoundException("Die Updatekomponente fehlt. Bitte das aktuelle Suite-Setup verwenden.");
        string directory = Path.Combine(SuiteSettings.DataDirectory, "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string helper = Path.Combine(directory, "XDUpdateHelper.exe");
        File.Copy(source, helper);
        if (File.Exists(source + ".config")) File.Copy(source + ".config", helper + ".config");
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (string argument in new[] { "--parent", Environment.ProcessId.ToString(), "--setup", installer, "--target", AppContext.BaseDirectory, "--sha256", package.InstallerSha256, "--language", L10n.Current == UiLanguage.German ? "german" : "english" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Die Updatekomponente konnte nicht gestartet werden.");
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
    private static async Task<string> ReadText(PackageTransport transport, Uri uri, int maximum, CancellationToken token)
    {
        using var response = await transport.Get(uri, token);
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Die Updatedaten sind zu groß.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        byte[] bytes = new byte[16384]; int read;
        while ((read = await input.ReadAsync(bytes, token)) > 0) { if (buffer.Length + read > maximum) throw new InvalidDataException("Die Updatedaten sind zu groß."); buffer.Write(bytes, 0, read); }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
