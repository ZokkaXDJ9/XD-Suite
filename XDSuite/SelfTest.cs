using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XDSuite;

internal static class SelfTest
{
    public static async Task Run(string reportPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string> { "XD Suite integration checks", DateTimeOffset.Now.ToString("O") };
        string temporary = Path.Combine(Path.GetTempPath(), "XDSuite-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var catalog = PackageValidation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));
            Check(catalog.Apps.Count > 0, "Bundled catalog");
            var burner = catalog.Apps.Single(p => p.Id == "xd-burner");
            Check(burner.Name.De == "XD Brenner" && burner.Name.En == "XD Burner", "Branding in both languages");
            var bad = burner with { Executable = "../outside.exe" };
            await Reject(() => Task.Run(() => PackageValidation.Parse(JsonSerializer.Serialize(new Catalog(1, [bad]), PackageValidation.JsonOptions))), "Catalog path traversal");
            await Reject(() => Task.Run(() => PackageValidation.Parse(JsonSerializer.Serialize(new Catalog(1, [burner with { InstallerUrl = "http://example.com/a.exe" }]), PackageValidation.JsonOptions))), "HTTP installer URL");
            await Reject(() => Task.Run(() => PackageValidation.Parse(JsonSerializer.Serialize(new Catalog(1, [burner, burner]), PackageValidation.JsonOptions))), "Duplicate app IDs");
            SuiteSettings.ValidateRepository("ZokkaXDJ9/XD-Suite");
            await Reject(() => Task.Run(() => SuiteSettings.ValidateRepository("../bad/repo")), "Invalid repository");
            lines.Add("PASS: catalog parsing, bilingual app names, duplicate IDs, executable traversal, HTTPS and repository validation");

            byte[] payload = Enumerable.Range(0, 210123).Select(i => (byte)(i * 7)).ToArray();
            string hash = Convert.ToHexString(SHA256.HashData(payload));
            var package = burner with { InstallerUrl = "https://downloads.example.test/setup.exe", InstallerSha256 = hash, InstallerSizeBytes = payload.Length };
            var handler = new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
            using var transport = new PackageTransport(handler);
            var progress = new InlineProgress();
            string path = await transport.Download(package, temporary, progress, CancellationToken.None);
            Check(File.ReadAllBytes(path).SequenceEqual(payload) && progress.Count > 0, "Download bytes and progress");
            Check(await PackageTransport.Verify(path, package, CancellationToken.None), "Downloaded hash verification");
            int requests = handler.Count;
            Check(await transport.Download(package, temporary, progress, CancellationToken.None) == path && handler.Count == requests, "Valid cached setup reused");
            File.WriteAllBytes(path, new byte[payload.Length]);
            await transport.Download(package, temporary, progress, CancellationToken.None);
            Check(handler.Count == requests + 1 && File.ReadAllBytes(path).SequenceEqual(payload), "Corrupted cache is downloaded again");
            lines.Add("PASS: streamed download, progress, SHA-256, cache reuse and replacement of a tampered cache file");

            using var wrong = new PackageTransport(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload.Select(b => (byte)(b ^ 1)).ToArray()) }));
            await Reject(() => wrong.Download(package with { Id = "bad-hash" }, temporary, progress, CancellationToken.None), "Wrong payload hash");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Reject(() => transport.Download(package with { Id = "cancel-test" }, temporary, progress, canceled.Token), "Canceled download");
            Check(!Directory.EnumerateFiles(temporary, "*.partial").Any(), "Partial download cleanup");
            using var redirect = new PackageTransport(new FakeHandler(_ => { var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new("http://unsafe.example.test/a.exe"); return r; }));
            await Reject(() => redirect.Download(package with { Id = "redirect-test" }, temporary, progress, CancellationToken.None), "HTTPS downgrade redirect");
            using var invalidCatalog = new PackageTransport(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"schemaVersion\":999,\"apps\":[]}") }));
            await Reject(() => invalidCatalog.FetchCatalog(new("https://example.test/catalog.json"), CancellationToken.None), "Unsupported catalog");
            lines.Add("PASS: bad hashes, cancellation, partial cleanup, unsafe redirects and invalid online catalogs rejected");
            lines.Add("NOTE: HTTP responses were injected. A real GitHub Release download requires publishing the release.");
            lines.Add("ALL SUITE CHECKS PASSED");
        }
        catch (Exception ex) { lines.Add("FAIL: " + ex); throw; }
        finally
        {
            File.WriteAllLines(reportPath, lines);
            foreach (string file in Directory.EnumerateFiles(temporary)) File.Delete(file);
            Directory.Delete(temporary);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task Reject(Func<Task> action, string message) { try { await action(); } catch (Exception ex) when (ex is InvalidDataException or OperationCanceledException or InvalidOperationException) { return; } throw new InvalidOperationException(message + " was not rejected"); }
    private sealed class InlineProgress : IProgress<DownloadProgress> { public int Count; public void Report(DownloadProgress value) => Count++; }
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Count++; return Task.FromResult(respond(request)); }
    }
}
