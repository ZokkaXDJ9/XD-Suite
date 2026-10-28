using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using XD.Common;

namespace XDSuite;

public sealed record LocalText(string De, string En)
{
    public override string ToString() => L10n.Current == UiLanguage.German ? De : En;
}
public sealed record AppPackage(string Id, LocalText Name, LocalText Description, LocalText Category, string Version,
    string InstallerUrl, string InstallerSha256, long InstallerSizeBytes, string Executable, string RegistryId,
    string InstallFolder, List<LocalText> Features);
public sealed record Catalog(int SchemaVersion, List<AppPackage> Apps);
public sealed record InstalledApp(string Executable, string Version);
public sealed record DownloadProgress(long Bytes, long Total, string Message)
{
    public double Percent => Total > 0 ? Math.Clamp(Bytes * 100d / Total, 0, 100) : 0;
}
public sealed class AppCard(AppPackage package, InstalledApp? installed)
{
    public AppPackage Package { get; } = package;
    public InstalledApp? Installed { get; } = installed;
    public string Name => Package.Name.ToString();
    public string Description => Package.Description.ToString();
    public string Category => Package.Category.ToString();
    public string Features => string.Join("\n", Package.Features.Select(f => "✓  " + f));
    public string VersionText => L10n.T("Version") + " " + Package.Version + " · Windows 10/11 · 64 Bit";
    public string Size => (Package.InstallerSizeBytes / 1048576d).ToString("0.0", L10n.Culture) + " MB";
    public bool HasUpdate => Installed != null && System.Version.TryParse(Installed.Version.Split('+')[0], out var current) && System.Version.TryParse(Package.Version, out var latest) && latest > current;
    public string Status => L10n.T(Installed == null ? "Noch nicht installiert" : HasUpdate ? "Update verfügbar" : "Installiert");
    public string Action => L10n.T(Installed == null ? "Herunterladen & installieren" : HasUpdate ? "Update installieren" : "Programm starten");
}

public static class PackageValidation
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static Catalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<Catalog>(json, JsonOptions) ?? throw new InvalidDataException("Der Programmkatalog ist leer.");
        if (catalog.SchemaVersion != 1 || catalog.Apps == null || catalog.Apps.Count > 100) throw new InvalidDataException("Dieser Programmkatalog wird nicht unterstützt.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog.Apps)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.Id) || !Regex.IsMatch(app.Id, "^[a-z0-9][a-z0-9-]{0,63}$") || !ids.Add(app.Id)) throw new InvalidDataException("Ungültige oder doppelte Programmkennung im Katalog.");
            if (!System.Version.TryParse(app.Version, out _) || app.Name == null || app.Description == null || app.Category == null || app.Features == null || app.Features.Count > 20 || string.IsNullOrWhiteSpace(app.Name.De) || string.IsNullOrWhiteSpace(app.Name.En)) throw new InvalidDataException("Der Programmkatalog enthält unvollständige Angaben.");
            if (!Uri.TryCreate(app.InstallerUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Setup-Downloads müssen eine gültige HTTPS-Adresse verwenden.");
            if (!Regex.IsMatch(app.InstallerSha256 ?? "", "^[a-fA-F0-9]{64}$") || app.InstallerSizeBytes is < 1 or > 1_073_741_824) throw new InvalidDataException("Prüfsumme oder Setup-Größe im Katalog ist ungültig.");
            if (!Regex.IsMatch(app.Executable ?? "", "^[A-Za-z0-9_-]+\\.exe$") || !Regex.IsMatch(app.InstallFolder ?? "", "^[A-Za-z0-9_-]+$") || !Regex.IsMatch(app.RegistryId ?? "", "^\\{[A-Fa-f0-9-]{36}\\}_is1$")) throw new InvalidDataException("Ungültiger Installationspfad im Programmkatalog.");
        }
        return catalog;
    }
}

public static class InstallationDiscovery
{
    public static InstalledApp? Find(AppPackage app)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + app.RegistryId);
            string? path = key?.GetValue("InstallLocation") as string ?? key?.GetValue("Inno Setup: App Path") as string;
            if (path != null && TryAt(Path.Combine(path, app.Executable)) is { } found) return found;
        }
        return TryAt(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", app.InstallFolder, app.Executable));
    }
    private static InstalledApp? TryAt(string path)
    {
        if (!File.Exists(path)) return null;
        var info = FileVersionInfo.GetVersionInfo(path);
        return new(Path.GetFullPath(path), info.ProductVersion ?? info.FileVersion ?? "0.0.0");
    }
    public static Process Launch(InstalledApp app) => Process.Start(new ProcessStartInfo(app.Executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(app.Executable)! }) ?? throw new IOException("Das Programm wurde nicht gefunden. Bitte neu installieren.");
}
