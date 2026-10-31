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
    string InstallFolder, List<LocalText> Features, string? Build = null, string? Branch = null, string? Channel = null);
public sealed record Catalog(int SchemaVersion, List<AppPackage> Apps);
public sealed record InstalledApp(string Executable, string Version, string? Uninstaller = null);
public sealed record DownloadProgress(long Bytes, long Total, string Message)
{
    public double Percent => Total > 0 ? Math.Clamp(Bytes * 100d / Total, 0, 100) : 0;
}
public sealed class AppCard(AppPackage package, InstalledApp? installed)
{
    public AppPackage Package { get; } = package;
    public InstalledApp? Installed { get; } = installed;
    public string Name => Package.Name.ToString();
    public string Icon => "pack://application:,,,/XDSuite;component/Assets/" + (Package.Id.Contains("wave-editor") ? "XDWaveEditor.ico" : Package.Id.Contains("tag-editor") ? "XDTagEditor.ico" : Package.Id.Contains("cd-ripper") ? "XDCDRipper.ico" : Package.Id.Contains("converter") ? "XDConverter.ico" : "XDBurner.ico").Replace(".ico", IsBeta ? "Beta.ico" : ".ico");
    public string Description => Package.Description.ToString();
    public string Category => Package.Category.ToString();
    public string Features => string.Join("\n", Package.Features.Select(f => "✓  " + f));
    public bool IsBeta => Package.Channel == "beta";
    public string VersionText => L10n.T("Version") + " " + Package.Version + (IsBeta ? "-Beta\n" + Package.Branch + "\n" + Package.Build : "") + " · Windows 10/11 · 64 Bit";
    public string Size => (Package.InstallerSizeBytes / 1048576d).ToString("0.0", L10n.Culture) + " MB";
    public bool HasUpdate => Installed != null && (IsBeta ? HasBetaUpdate() : System.Version.TryParse(Installed.Version.Split('+')[0], out var current) && System.Version.TryParse(Package.Version, out var latest) && latest > current);
    private bool HasBetaUpdate()
    {
        var installed = Regex.Match(Installed!.Version, @"^(?<version>\d+\.\d+\.\d+)-Beta\+(?<stem>[a-z0-9-]+)\.(?<build>\d+)$");
        var available = Regex.Match(Package.Build ?? "", @"^(?<version>\d+\.\d+\.\d+)-Beta\+(?<stem>[a-z0-9-]+)\.(?<build>\d+)$");
        if (!installed.Success || !available.Success) return !string.Equals(Installed.Version, Package.Build, StringComparison.OrdinalIgnoreCase);
        int comparison = Version.Parse(available.Groups["version"].Value).CompareTo(Version.Parse(installed.Groups["version"].Value));
        if (comparison != 0) return comparison > 0;
        return available.Groups["stem"].Value == installed.Groups["stem"].Value && System.Numerics.BigInteger.Parse(available.Groups["build"].Value) > System.Numerics.BigInteger.Parse(installed.Groups["build"].Value);
    }
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
            if (!Regex.IsMatch(app.Executable ?? "", "^[A-Za-z0-9_-]+(?:\\.Beta)?\\.exe$") || !Regex.IsMatch(app.InstallFolder ?? "", "^[A-Za-z0-9_-]+$") || !Regex.IsMatch(app.RegistryId ?? "", "^\\{[A-Fa-f0-9-]{36}\\}_is1$")) throw new InvalidDataException("Ungültiger Installationspfad im Programmkatalog.");
        }
        return catalog;
    }
}

public static class InstallationDiscovery
{
    public static IReadOnlyList<AppPackage> FindInstalledBetas()
    {
        var packages = new Dictionary<string, AppPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (string id in uninstall.GetSubKeyNames().Where(id => Regex.IsMatch(id, @"^\{[A-Fa-f0-9-]{36}\}_is1$")))
            {
                try
                {
                    using var key = uninstall.OpenSubKey(id);
                    string name = key?.GetValue("DisplayName") as string ?? "";
                    string path = key?.GetValue("InstallLocation") as string ?? "";
                    if (!name.StartsWith("XD ", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path)) continue;
                    foreach (string exe in Directory.EnumerateFiles(path, "XD*.Beta.exe", SearchOption.TopDirectoryOnly))
                    {
                        var info = FileVersionInfo.GetVersionInfo(exe);
                        var match = Regex.Match(info.ProductVersion ?? "", @"^(?<version>\d+\.\d+\.\d+)-Beta\+(?<stem>[a-z0-9-]+)\.(?<build>\d+)$");
                        if (!match.Success || !(info.ProductName ?? "").StartsWith("XD ", StringComparison.OrdinalIgnoreCase)) continue;
                        string stem = match.Groups["stem"].Value, version = match.Groups["version"].Value;
                        string display = Regex.Replace(name, @"\s+\d+\.\d+\.\d+-Beta$", "");
                        packages.TryAdd(id, new("xd-" + stem + "-beta", new(display, info.ProductName!),
                            new("Installierte Beta-Version. Eigenständig nutzbar.", "Installed beta build. Usable independently."), new("Beta", "Beta"),
                            version, "", "", 0, Path.GetFileName(exe), id, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
                            [new("Getrennte Beta-Installation", "Separate beta installation")], info.ProductVersion, "app/xd-" + stem, "beta"));
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { App.Log(e); }
            }
        }
        return packages.Values.ToList();
    }
    public static InstalledApp? Find(AppPackage app)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + app.RegistryId);
            string? path = key?.GetValue("InstallLocation") as string ?? key?.GetValue("Inno Setup: App Path") as string;
            if (path != null && TryAt(Path.Combine(path, app.Executable)) is { } found)
                return found with { Uninstaller = PackageUninstaller.Resolve(key?.GetValue("UninstallString") as string, found.Executable) };
        }
        return TryAt(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XD Suite", app.InstallFolder, app.Executable))
            ?? TryAt(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), app.InstallFolder, app.Executable))
            ?? TryAt(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", app.InstallFolder, app.Executable));
    }
    private static InstalledApp? TryAt(string path)
    {
        if (!File.Exists(path)) return null;
        var info = FileVersionInfo.GetVersionInfo(path);
        return new(Path.GetFullPath(path), info.ProductVersion ?? info.FileVersion ?? "0.0.0");
    }
    public static Process Launch(InstalledApp app)
    {
        var start = new ProcessStartInfo(app.Executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(app.Executable)! };
        if (Path.GetFileName(app.Executable).Equals("XDConverter.exe", StringComparison.OrdinalIgnoreCase) || app.Executable.EndsWith(".Beta.exe", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(L10n.Current == UiLanguage.German ? "--german" : "--english");
        return Process.Start(start) ?? throw new IOException("Das Programm wurde nicht gefunden. Bitte neu installieren.");
    }
}
