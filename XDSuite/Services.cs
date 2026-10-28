using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using XD.Common;

namespace XDSuite;

public sealed class SuiteSettings
{
    public string Repository { get; set; } = "ZokkaXDJ9/XD-Suite";
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDSuite");
    public static SuiteSettings Load()
    {
        try { var settings = JsonSerializer.Deserialize<SuiteSettings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))) ?? new(); ValidateRepository(settings.Repository); return settings; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return new(); }
    }
    public void Save()
    {
        ValidateRepository(Repository);
        Directory.CreateDirectory(DataDirectory);
        string path = Path.Combine(DataDirectory, "settings.json"), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, true);
    }
    public static void ValidateRepository(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$") || value.Contains("..")) throw new InvalidOperationException("Bitte ein GitHub-Repository als Besitzer/Repository eingeben.");
    }
    public Uri CatalogUri { get { ValidateRepository(Repository); return new($"https://github.com/{Repository}/releases/latest/download/catalog.json"); } }
}

public sealed class PackageTransport : IDisposable
{
    private readonly HttpClient client;
    public PackageTransport(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XD-Suite/0.3.0");
    }
    internal async Task<HttpResponseMessage> Get(Uri uri, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Unsichere Download-Weiterleitung wurde abgewiesen.");
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                uri = new Uri(uri, location);
                response.Dispose();
                continue;
            }
            try { response.EnsureSuccessStatusCode(); } catch { response.Dispose(); throw; }
            return response;
        }
        throw new HttpRequestException("Zu viele Download-Weiterleitungen.");
    }
    public async Task<Catalog> FetchCatalog(Uri uri, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        token = timeout.Token;
        using var response = await Get(uri, token);
        if (response.Content.Headers.ContentLength > 1_048_576) throw new InvalidDataException("Der Programmkatalog ist zu groß.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[16384];
        int read;
        while ((read = await input.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > 1_048_576) throw new InvalidDataException("Der Programmkatalog ist zu groß.");
            buffer.Write(chunk, 0, read);
        }
        return PackageValidation.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
    public async Task<string> Download(AppPackage app, string cacheDirectory, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        PackageValidation.Parse(JsonSerializer.Serialize(new Catalog(1, [app]), PackageValidation.JsonOptions));
        Directory.CreateDirectory(cacheDirectory);
        string destination = Path.Combine(cacheDirectory, $"{app.Id}-{app.Version}-{app.InstallerSha256[..12]}.exe");
        if (File.Exists(destination) && await Verify(destination, app, token)) return destination;
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var response = await Get(new(app.InstallerUrl), token);
            if (response.Content.Headers.ContentLength is long announced && announced != app.InstallerSizeBytes) throw new InvalidDataException("Die Setup-Größe stimmt nicht mit dem Katalog überein.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] bytes = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(bytes, token)) > 0)
                {
                    total += read;
                    if (total > app.InstallerSizeBytes) throw new InvalidDataException("Das heruntergeladene Setup ist größer als erwartet.");
                    await output.WriteAsync(bytes.AsMemory(0, read), token);
                    hash.AppendData(bytes, 0, read);
                    progress.Report(new(total, app.InstallerSizeBytes, "Setup wird heruntergeladen …"));
                }
                if (total != app.InstallerSizeBytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(app.InstallerSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Die Setup-Prüfsumme stimmt nicht. Die Datei wird nicht ausgeführt.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task<bool> Verify(string path, AppPackage app, CancellationToken token)
    {
        if (new FileInfo(path).Length != app.InstallerSizeBytes) return false;
        await using var input = File.OpenRead(path);
        byte[] actual = await SHA256.HashDataAsync(input, token);
        return Convert.ToHexString(actual).Equals(app.InstallerSha256, StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() => client.Dispose();
}

public static class PackageInstaller
{
    public static async Task Install(string path, AppPackage app, Action started, CancellationToken token, string? testInstallDirectory = null)
    {
        if (!await PackageTransport.Verify(path, app, token)) throw new InvalidDataException("Die Setup-Prüfsumme stimmt nicht. Die Datei wird nicht ausgeführt.");
        token.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = new ProcessStartInfo(path) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden } };
        foreach (string argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/TASKS=\"\"" }) process.StartInfo.ArgumentList.Add(argument);
        process.StartInfo.ArgumentList.Add(L10n.Current == UiLanguage.German ? "/LANG=german" : "/LANG=english");
        if (testInstallDirectory != null) process.StartInfo.ArgumentList.Add("/DIR=" + Path.GetFullPath(testInstallDirectory));
        if (!process.Start()) throw new IOException("Das Setup konnte nicht gestartet werden.");
        started();
        // Once installation starts, wait for the installer. Killing it could leave a partial installation.
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 && process.ExitCode != 3010) throw new IOException($"Das Setup wurde mit Fehlercode {process.ExitCode} beendet.");
        if (InstallationDiscovery.Find(app) == null) throw new IOException("Das Setup wurde beendet, aber die Anwendung wurde noch nicht gefunden. Bitte die Installation prüfen.");
    }
}
