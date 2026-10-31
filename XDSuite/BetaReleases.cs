using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XDSuite;

public static class BetaReleases
{
    public static Catalog Parse(string json, string repository, string releaseTag)
    {
        var catalog = PackageValidation.Parse(json);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog.Apps)
        {
            if (app.Channel != "beta" || !app.Id.EndsWith("-beta", StringComparison.Ordinal) || !app.Executable.EndsWith(".Beta.exe", StringComparison.Ordinal) || !app.InstallFolder.EndsWith("Beta", StringComparison.Ordinal)
                || app.Build == null || !Regex.IsMatch(app.Build, "^" + Regex.Escape(app.Version) + @"-Beta\+[a-z0-9.-]+$")
                || app.Branch == null || !Regex.IsMatch(app.Branch, @"^app/[a-z0-9-]+$")
                || new[] { "{B5AF424C-85D1-47D4-AF62-30A5A15C5094}_is1", "{3D477D64-7020-48F0-A621-66CF2958722B}_is1", "{F80CC53F-43F1-4E46-B7F4-61435E75303D}_is1" }.Contains(app.RegistryId, StringComparer.OrdinalIgnoreCase)
                || !ids.Add(app.RegistryId)) throw new InvalidDataException("Ungültiges Beta-Paket / Invalid beta package.");
            string prefix = "https://github.com/" + repository + "/releases/download/" + Uri.EscapeDataString(releaseTag) + "/";
            if (!app.InstallerUrl.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Beta-Setup gehört nicht zur Vorabversion / Beta installer does not belong to the prerelease.");
        }
        return catalog;
    }
    public static async Task<Catalog> Fetch(PackageTransport transport, string repository, CancellationToken token)
    {
        SuiteSettings.ValidateRepository(repository);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45)); token = timeout.Token;
        var found = new Dictionary<string, AppPackage>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<JsonElement>();
        for (int page = 1; page <= 20; page++)
        {
            string json = await SuiteUpdates.ReadText(transport, new Uri($"https://api.github.com/repos/{repository}/releases?per_page=100&page={page}"), 8_388_608, token);
            using var document = JsonDocument.Parse(json); var releases = document.RootElement.EnumerateArray().ToArray();
            candidates.AddRange(releases.Where(r => !r.GetProperty("draft").GetBoolean() && r.GetProperty("prerelease").GetBoolean()).Select(r => r.Clone()));
            if (releases.Length < 100) break;
        }
        // Finishing order is independent of push order. Skip superseded CI manifests.
        var ci = new Dictionary<string, (Version Version, System.Numerics.BigInteger Build, string Tag)>(StringComparer.OrdinalIgnoreCase);
        foreach (var release in candidates)
        {
            string tag = release.GetProperty("tag_name").GetString()!;
            var match = Regex.Match(tag, @"^(?<id>xd-[a-z0-9-]+)-(?<version>\d+\.\d+\.\d+)-Beta-ci-(?<build>\d+)$");
            if (!match.Success) continue;
            string id = match.Groups["id"].Value;
            var version = Version.Parse(match.Groups["version"].Value); var build = System.Numerics.BigInteger.Parse(match.Groups["build"].Value);
            if (!ci.TryGetValue(id, out var previous) || version > previous.Version || version == previous.Version && build > previous.Build) ci[id] = (version, build, tag);
        }
        var selectedTags = ci.Values.Select(c => c.Tag).ToHashSet(StringComparer.Ordinal);
        foreach (var release in candidates)
        {
            string tag = release.GetProperty("tag_name").GetString()!;
            if (Regex.IsMatch(tag, @"-Beta-ci-\d+$") && !selectedTags.Contains(tag)) continue;
            var asset = release.GetProperty("assets").EnumerateArray().SingleOrDefault(a => a.GetProperty("name").GetString() == "beta-catalog.json");
            if (asset.ValueKind == JsonValueKind.Undefined) continue;
            string text = await SuiteUpdates.ReadText(transport, new Uri(asset.GetProperty("browser_download_url").GetString()!), 1_048_576, token);
            var catalog = Parse(text, repository, tag);
            foreach (var app in catalog.Apps)
            {
                var setup = release.GetProperty("assets").EnumerateArray().SingleOrDefault(a => a.GetProperty("browser_download_url").GetString() == app.InstallerUrl);
                if (setup.ValueKind == JsonValueKind.Undefined || setup.GetProperty("size").GetInt64() != app.InstallerSizeBytes) throw new InvalidDataException("Beta-Setup fehlt oder hat eine andere Größe / Beta installer missing or size differs.");
                if (setup.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String && digest.GetString() != "sha256:" + app.InstallerSha256.ToLowerInvariant()) throw new InvalidDataException("Beta-Prüfsumme widerspricht GitHub / Beta checksum differs from GitHub.");
                if (!found.TryGetValue(app.Id, out var previous) || new AppCard(app, new("unused", previous.Build!)).HasUpdate) found[app.Id] = app;
            }
        }
        return new(1, found.Values.ToList());
    }
}
