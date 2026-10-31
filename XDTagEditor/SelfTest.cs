using System.Diagnostics;
using System.IO;

namespace XDTagEditor;
internal static class SelfTest
{
    public static void Run(string engine, string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        var lines = new List<string> { "XD Tag Editor preview checks" };
        string root = Path.Combine(Path.GetTempPath(), "XDTagEditor-Tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var cover = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            foreach (var (extension, codec) in new[] { ("mp3", "libmp3lame"), ("flac", "flac"), ("m4a", "aac"), ("ogg", "libvorbis"), ("opus", "libopus"), ("wav", "pcm_s16le") })
            {
                string file = Path.Combine(root, "01 - Künstler - Prüfstück." + extension);
                Process(engine, "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.4", "-c:a", codec, file);
                string audioBefore = AudioHash(engine, file); string original = TagService.Fingerprint(file);
                var info = TagService.Read(file);
                var patch = new TagPatch(new() { ["Title"] = "Prüfstück ✓", ["Artist"] = "Künstler", ["Album"] = "Album", ["Year"] = "2026", ["Track"] = "1", ["Comment"] = "Kommentar" });
                var saved = TagService.Save(info, patch);
                Check(saved.Title == "Prüfstück ✓" && saved.Artist == "Künstler" && saved.Track == 1, "Round trip " + extension);
                Check(AudioHash(engine, file) == audioBefore, "Audio unchanged " + extension);
                Check(Directory.EnumerateFiles(Path.Combine(root, ".xd-tag-backups")).Any(p => TagService.Fingerprint(p) == original), "Byte-identical backup " + extension);
                Reject(() => TagService.Save(info, patch), "Stale snapshot " + extension);
                if (extension is "mp3" or "flac" or "m4a" or "ogg" or "opus")
                {
                    saved = TagService.Save(saved, new(new(), true, cover, "image/png"));
                    Check(saved.Cover!.SequenceEqual(cover), "Cover " + extension);
                    saved = TagService.Save(saved, new(new() { ["Album"] = "Changed" }));
                    Check(saved.Cover!.SequenceEqual(cover) && saved.Title == "Prüfstück ✓", "Unselected metadata retained " + extension);
                    saved = TagService.Save(saved, new(new(), true)); Check(saved.Cover == null, "Remove cover " + extension);
                }
                Check(AudioHash(engine, file) == audioBefore, "Audio preserved after all edits " + extension);
                lines.Add("PASS: " + extension + " tags, unchanged decoded audio, backup, stale-write rejection" + (extension == "wav" ? "" : ", cover add/retain/remove"));
            }
            var source = TagService.Read(Directory.GetFiles(root, "*.mp3").Single());
            var parsed = TagService.FromFilename(source.Path); Check(parsed.Values["Track"] == "01" && parsed.Values["Artist"] == "Künstler", "Filename parsing");
            Reject(() => TagService.FromFilename("invalid.mp3"), "Invalid filename pattern");
            Reject(() => TagService.Validate(new(new() { ["Track"] = "-2" })), "Invalid track");
            Reject(() => TagService.Validate(new(new() { ["Year"] = "10000" })), "Invalid year");
            string hash = TagService.Fingerprint(source.Path);
            File.SetAttributes(source.Path, FileAttributes.ReadOnly);
            try { Reject(() => TagService.Save(source, new(new() { ["Title"] = "must fail" })), "Read-only original"); Check(TagService.Fingerprint(source.Path) == hash, "Read-only source unchanged"); }
            finally { File.SetAttributes(source.Path, FileAttributes.Normal); }
            var plan = TagService.PlanRename([source], "{track} - {title}"); Check(plan.Count == 1, "Rename preview");
            File.WriteAllText(plan[0].Destination, "collision"); Reject(() => TagService.PlanRename([source], "{track} - {title}"), "Rename collision"); File.Delete(plan[0].Destination);
            Reject(() => TagService.PlanRename([source, source], "{title}"), "Duplicate target"); Reject(() => TagService.PlanRename([source], "CON"), "Reserved filename");
            TagService.Rename(plan); Check(File.Exists(plan[0].Destination) && !File.Exists(source.Path) && TagService.Fingerprint(plan[0].Destination) == hash, "Rename preserves bytes");
            Check(!Directory.EnumerateFiles(root, ".xd-tag-*").Any(), "Temporary file cleanup");
            lines.Add("PASS: filename parsing, numeric validation, read-only protection, collision and reserved-name rejection, rename, temporary cleanup");
            lines.Add("ALL TAG EDITOR CHECKS PASSED");
        }
        catch (Exception e) { lines.Add("FAIL: " + e); throw; }
        finally { File.WriteAllLines(report, lines); Directory.Delete(root, true); }
    }
    static string AudioHash(string engine, string path) => Process(engine, "-v", "error", "-i", path, "-map", "0:a:0", "-f", "hash", "-hash", "sha256", "-").Trim();
    static string Process(string engine, params string[] args)
    {
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in args) start.ArgumentList.Add(arg);
        using var p = System.Diagnostics.Process.Start(start)!; var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync(); if (!p.WaitForExit(30000)) { p.Kill(true); throw new TimeoutException(); } Task.WaitAll(stdout, stderr); if (p.ExitCode != 0) throw new IOException(stderr.Result); return stdout.Result;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch { return; } throw new Exception("Expected rejection: " + message); }
}
