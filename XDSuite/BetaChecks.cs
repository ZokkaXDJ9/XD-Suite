using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Controls;
using XD.Common;

namespace XDSuite;
internal static class BetaChecks
{
    public static AppPackage Example() => new("xd-tag-editor-beta", new("XD Tag-Editor Beta", "XD Tag Editor Beta"), new("Tags bearbeiten", "Edit tags"), new("Audio · Beta", "Audio · Beta"), "0.5.2", "https://github.com/ZokkaXDJ9/XD-Suite/releases/download/beta-test/setup.exe", new string('a', 64), 16, "XDTagEditor.Beta.exe", "{180FF9A1-26B7-4B1B-A501-9ACD282923F0}_is1", "XDTagEditorBeta", [new("Getrennte Installation", "Separate installation")], "0.5.2-Beta+tag-editor.1", "app/xd-tag-editor", "beta");
    public static async Task Run(string report)
    {
        var package = Example(); string Json(AppPackage p) => JsonSerializer.Serialize(new Catalog(1, [p]), PackageValidation.JsonOptions);
        BetaReleases.Parse(Json(package), SuiteSettings.Repository, "beta-test");
        Reject(() => BetaReleases.Parse(Json(package with { Channel = null }), SuiteSettings.Repository, "beta-test"));
        Reject(() => BetaReleases.Parse(Json(package with { Executable = "XDConverter.exe" }), SuiteSettings.Repository, "beta-test"));
        Reject(() => BetaReleases.Parse(Json(package with { RegistryId = "{3d477d64-7020-48f0-a621-66cf2958722b}_is1" }), SuiteSettings.Repository, "beta-test"));
        Reject(() => BetaReleases.Parse(Json(package with { Branch = "main" }), SuiteSettings.Repository, "beta-test"));
        Reject(() => BetaReleases.Parse(Json(package with { InstallerUrl = "https://example.test/setup.exe" }), SuiteSettings.Repository, "beta-test"));
        Check(!new AppCard(package, new("unused", package.Build!)).HasUpdate, "Same beta build is current");
        Check(new AppCard(package, new("unused", "0.5.2-Beta+tag-editor.0")).HasUpdate, "New beta build detected independently of stable version");
        string Release(bool draft, bool beta, string tag, string manifest, string date) => JsonSerializer.Serialize(new { draft, prerelease = beta, tag_name = tag, published_at = date, assets = new object[] { new { name = "beta-catalog.json", browser_download_url = manifest, size = 100 }, new { name = "setup.exe", browser_download_url = package.InstallerUrl, size = 16, digest = "sha256:" + package.InstallerSha256 } } });
        string releases = "[" + string.Join(',', Release(false, false, "stable", "https://example.test/never", "2026-10-31T12:00:00Z"), Release(true, true, "draft", "https://example.test/never", "2026-10-31T12:00:00Z"), Release(false, true, "beta-test", "https://example.test/beta-catalog.json", "2026-10-31T10:00:00Z")) + "]";
        using var transport = new PackageTransport(new Handler(request => request.RequestUri!.Host == "api.github.com" ? releases : request.RequestUri.AbsolutePath == "/beta-catalog.json" ? Json(package) : throw new Exception("Stable or draft release was fetched")));
        var fetched = await BetaReleases.Fetch(transport, SuiteSettings.Repository, CancellationToken.None);
        Check(fetched.Apps.Count == 1 && fetched.Apps[0].Build == package.Build, "Only published prerelease is listed");
        var window = new MainWindow(preview: true);
        try
        {
            var list = (ListBox)window.FindName("AppList"); Check(list.Items.Cast<AppCard>().All(c => !c.IsBeta), "Stable tab excludes beta packages");
            window.ShowBetaPreview(fetched); Check(list.Items.Count == 1 && ((AppCard)list.Items[0]).IsBeta, "Beta tab is separate");
            var search = (TextBox)window.FindName("SearchBox"); search.Text = "Tag";
            L10n.Set(UiLanguage.English, false); Check(((RadioButton)window.FindName("BetaNav")).Content.ToString() == "Beta releases" && search.Text == "Tag", "English beta UI preserves input");
            L10n.Set(UiLanguage.German, false); Check(((RadioButton)window.FindName("BetaNav")).Content.ToString() == "Beta-Releases" && search.Text == "Tag", "German beta UI preserves input");
        }
        finally { window.Close(); }
        File.AppendAllText(report, "PASS: prerelease-only discovery, beta identity/version/branch validation, stable registration protection, separate beta tab, German/English and input preservation\n");
    }
    static void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
    static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid beta was accepted"); }
    sealed class Handler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request)) });
    }
}
