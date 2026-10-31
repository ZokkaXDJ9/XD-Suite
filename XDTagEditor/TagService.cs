using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace XDTagEditor;

public sealed record TagInfo(string Path, string Title, string Artist, string Album, string AlbumArtist, string Genre, uint Year, uint Track, uint Disc, string Comment, byte[]? Cover, string CoverMime, string Fingerprint)
{
    public string Name => System.IO.Path.GetFileName(Path);
}
public sealed record TagPatch(Dictionary<string, string> Values, bool ChangeCover = false, byte[]? Cover = null, string CoverMime = "image/jpeg");
public sealed record RenameItem(string Source, string Destination)
{
    public string Before => System.IO.Path.GetFileName(Source);
    public string After => System.IO.Path.GetFileName(Destination);
}
public static class TagService
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".m4a", ".m4b", ".ogg", ".opus", ".wav", ".aif", ".aiff", ".wma" };
    public static string Fingerprint(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public static TagInfo Read(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!Extensions.Contains(System.IO.Path.GetExtension(path))) throw new InvalidDataException("Nicht unterstütztes Audioformat / Unsupported audio format.");
        using var file = TagLib.File.Create(path);
        var tag = file.Tag; var picture = tag.Pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? tag.Pictures.FirstOrDefault();
        return new(path, tag.Title ?? "", string.Join("; ", tag.Performers), tag.Album ?? "", string.Join("; ", tag.AlbumArtists), string.Join("; ", tag.Genres), tag.Year, tag.Track, tag.Disc, tag.Comment ?? "", picture?.Data.Data, picture?.MimeType ?? "", Fingerprint(path));
    }
    public static void Validate(TagPatch patch)
    {
        foreach (var pair in patch.Values)
        {
            if (!new[] { "Title", "Artist", "Album", "AlbumArtist", "Genre", "Year", "Track", "Disc", "Comment" }.Contains(pair.Key)) throw new ArgumentException("Unbekanntes Feld / Unknown field.");
            if (pair.Key is "Year" or "Track" or "Disc" && (!uint.TryParse(pair.Value.Length == 0 ? "0" : pair.Value, out var number) || number > 9999)) throw new ArgumentException("Jahr und Nummern: 0–9999 / Year and numbers: 0–9999.");
        }
        if (patch.Cover?.Length > 20 * 1024 * 1024) throw new ArgumentException("Cover maximal 20 MB / Cover maximum 20 MB.");
    }
    public static TagInfo Save(TagInfo original, TagPatch patch)
    {
        Validate(patch);
        if ((File.GetAttributes(original.Path) & FileAttributes.ReadOnly) != 0) throw new IOException("Datei ist schreibgeschützt / File is read-only.");
        if (Fingerprint(original.Path) != original.Fingerprint) throw new IOException("Datei wurde außerhalb des Editors geändert. Neu laden. / File changed outside the editor. Reload it.");
        var folder = System.IO.Path.GetDirectoryName(original.Path)!;
        string temporary = System.IO.Path.Combine(folder, ".xd-tag-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(original.Path));
        string backupFolder = System.IO.Path.Combine(folder, ".xd-tag-backups");
        Directory.CreateDirectory(backupFolder);
        string backup = System.IO.Path.Combine(backupFolder, System.IO.Path.GetFileNameWithoutExtension(original.Path) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + System.IO.Path.GetExtension(original.Path));
        try
        {
            File.Copy(original.Path, temporary);
            using (var file = TagLib.File.Create(temporary))
            {
                var tag = file.Tag;
                foreach (var (key, value) in patch.Values)
                {
                    string[] parts = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    uint number = uint.TryParse(value, out var n) ? n : 0;
                    switch (key)
                    {
                        case "Title": tag.Title = value; break; case "Artist": tag.Performers = parts; break;
                        case "Album": tag.Album = value; break; case "AlbumArtist": tag.AlbumArtists = parts; break;
                        case "Genre": tag.Genres = parts; break; case "Year": tag.Year = number; break;
                        case "Track": tag.Track = number; break; case "Disc": tag.Disc = number; break; case "Comment": tag.Comment = value; break;
                    }
                }
                if (patch.ChangeCover) tag.Pictures = patch.Cover == null ? [] : [new TagLib.Picture(new TagLib.ByteVector(patch.Cover)) { Type = TagLib.PictureType.FrontCover, MimeType = patch.CoverMime, Description = "Cover" }];
                file.Save();
            }
            var verified = Read(temporary);
            foreach (var pair in patch.Values)
            {
                string actual = pair.Key switch { "Title" => verified.Title, "Artist" => verified.Artist, "Album" => verified.Album, "AlbumArtist" => verified.AlbumArtist, "Genre" => verified.Genre, "Year" => verified.Year.ToString(), "Track" => verified.Track.ToString(), "Disc" => verified.Disc.ToString(), _ => verified.Comment };
                string expected = pair.Key is "Year" or "Track" or "Disc" ? (uint.TryParse(pair.Value, out var number) ? number : 0).ToString() : pair.Key is "Artist" or "AlbumArtist" or "Genre" ? string.Join("; ", pair.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : pair.Value;
                if (actual != expected) throw new IOException("Dieses Format speichert das Feld nicht zuverlässig: / Format cannot reliably store: " + pair.Key);
            }
            if (patch.ChangeCover && !(verified.Cover ?? []).SequenceEqual(patch.Cover ?? [])) throw new IOException("Cover konnte nicht gespeichert werden / Cover could not be saved.");
            if (Fingerprint(original.Path) != original.Fingerprint) throw new IOException("Datei inzwischen geändert / File changed during save.");
            File.Replace(temporary, original.Path, backup);
            return Read(original.Path);
        }
        finally { if (File.Exists(temporary)) { File.SetAttributes(temporary, FileAttributes.Normal); File.Delete(temporary); } }
    }
    public static List<RenameItem> PlanRename(IEnumerable<TagInfo> files, string template)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("Leere Vorlage / Empty template.");
        var result = new List<RenameItem>(); var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var values = new Dictionary<string, string> { ["title"] = file.Title, ["artist"] = file.Artist, ["album"] = file.Album, ["track"] = file.Track.ToString("00"), ["disc"] = file.Disc.ToString(), ["year"] = file.Year.ToString() };
            var name = Regex.Replace(template, @"\{([a-z]+)\}", m => values.TryGetValue(m.Groups[1].Value, out var v) && v.Length > 0 ? v : throw new ArgumentException("Fehlender oder unbekannter Wert: / Missing or unknown value: " + m.Value));
            if (name.Contains('{') || name.Contains('}')) throw new ArgumentException("Ungültige Vorlage / Invalid template.");
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            if (name.Length == 0 || name.Length > 180 || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)) throw new ArgumentException("Ungültiger Dateiname / Invalid filename.");
            var target = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path)!, name + System.IO.Path.GetExtension(file.Path));
            if (!targets.Add(target) || (!target.Equals(file.Path, StringComparison.OrdinalIgnoreCase) && (File.Exists(target) || Directory.Exists(target)))) throw new IOException("Namenskonflikt: / Name collision: " + target);
            if (!target.Equals(file.Path, StringComparison.OrdinalIgnoreCase)) result.Add(new(file.Path, target));
        }
        return result;
    }
    public static void Rename(IReadOnlyList<RenameItem> plan)
    {
        var completed = new List<RenameItem>();
        try { foreach (var item in plan) { File.Move(item.Source, item.Destination, false); completed.Add(item); } }
        catch { foreach (var item in completed.AsEnumerable().Reverse()) File.Move(item.Destination, item.Source, false); throw; }
    }
    public static TagPatch FromFilename(string path)
    {
        var match = Regex.Match(System.IO.Path.GetFileNameWithoutExtension(path), @"^(?:(?<track>\d+)\s*-\s*)?(?<artist>.+?)\s+-\s+(?<title>.+)$");
        if (!match.Success) throw new ArgumentException("Erwartet: 01 - Interpret - Titel / Expected: 01 - Artist - Title");
        var values = new Dictionary<string, string> { ["Artist"] = match.Groups["artist"].Value, ["Title"] = match.Groups["title"].Value };
        if (match.Groups["track"].Success) values["Track"] = match.Groups["track"].Value;
        return new(values);
    }
}
