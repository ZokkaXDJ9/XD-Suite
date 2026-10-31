using System.Buffers.Binary;
using System.IO;

namespace XDCDRipper;
internal static class SelfTest
{
    public static async Task Run(string engine, string report)
    {
        var lines = new List<string> { "XD CD Ripper beta checks" }; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        string root = Path.Combine(Path.GetTempPath(), "XDCDRipper-Tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var toc = new byte[28]; BinaryPrimitives.WriteUInt16BigEndian(toc, 26); toc[2] = 1; toc[3] = 2; toc[6] = 1; toc[14] = 2; toc[22] = 0xaa; BinaryPrimitives.WriteInt32BigEndian(toc.AsSpan(16), 75); BinaryPrimitives.WriteInt32BigEndian(toc.AsSpan(24), 150);
            var tracks = WindowsDiscSource.ParseToc(toc); Check(tracks.Count == 2 && tracks[0].Sectors == 75 && tracks[1].StartSector == 75 && tracks[1].EndSector == 150, "LBA track boundaries and final leadout");
            var bad = toc.ToArray(); bad[5] = 4; Reject(() => WindowsDiscSource.ParseToc(bad), "Mixed mode"); bad[5] = 1; Reject(() => WindowsDiscSource.ParseToc(bad), "Pre-emphasis"); Reject(() => WindowsDiscSource.ParseToc(toc[..20]), "Truncated TOC");
            lines.Add("PASS: native TOC layout, track boundaries, last leadout, mixed mode/pre-emphasis/truncation rejection");
            using var source = new SyntheticDisc(); string wave = Path.Combine(root, "source.wav"); int lastProgress = 0;
            RipService.WriteWave(source, source.Tracks[0], wave, true, s => lastProgress = s, CancellationToken.None);
            var bytes = File.ReadAllBytes(wave); Check(bytes.Length == 44 + 75 * 2352 && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)) == 75 * 2352 && lastProgress == 75, "Complete WAV header and last sector"); Check(bytes.AsSpan(44).SequenceEqual(source.Read(0, 75)), "Exact PCM byte sequence"); Check(source.Reads >= 11, "Double reads");
            Reject(() => RipService.WriteWave(source, source.Tracks[0], wave, true, _ => { }, CancellationToken.None), "Existing source path"); Check(File.ReadAllBytes(wave).SequenceEqual(bytes), "Existing file retained");
            string inputHash = (await RipService.Run(engine, ["-v", "error", "-i", wave, "-f", "hash", "-hash", "sha256", "-"], CancellationToken.None)).Trim();
            foreach (var format in new[] { "WAV", "FLAC", "MP3" })
            {
                var options = new RipOptions(root, format, "Künstler", "Album", "2026", "Test"); var track = new RipTrack(source.Tracks[0], "Prüfstück");
                string output = await RipService.Export(engine, wave, track, options, CancellationToken.None);
                string info = await RipService.Run(Path.Combine(Path.GetDirectoryName(engine)!, "ffprobe.exe"), ["-v", "error", "-show_entries", "format_tags=title,artist,album:stream=sample_rate,channels", "-of", "json", output], CancellationToken.None);
                Check(info.Contains("Prüfstück") && info.Contains("44100") && info.Contains("Künstler"), "Encoded output metadata " + format + ": " + info);
                if (format != "MP3") Check((await RipService.Run(engine, ["-v", "error", "-i", output, "-f", "hash", "-hash", "sha256", "-"], CancellationToken.None)).Trim() == inputHash, "Lossless samples " + format);
                string second = await RipService.Export(engine, wave, track, options, CancellationToken.None); Check(output != second && File.Exists(output), "Collision-safe output " + format);
                lines.Add("PASS: actual " + format + " export, metadata, existing-file protection" + (format == "MP3" ? "" : ", exact decoded PCM"));
            }
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); string partial = Path.Combine(root, "cancel.wav"); Reject(() => RipService.WriteWave(source, source.Tracks[0], partial, true, _ => { }, canceled.Token), "Cancellation"); Check(!File.Exists(partial), "Canceled partial removed");
            using var faulty = new SyntheticDisc { Fail = true }; partial = Path.Combine(root, "failure.wav"); Reject(() => RipService.WriteWave(faulty, faulty.Tracks[0], partial, true, _ => { }, CancellationToken.None), "Persistent read failure"); Check(!File.Exists(partial), "Failed partial removed");
            using var mismatch = new SyntheticDisc { Mismatch = true }; partial = Path.Combine(root, "mismatch.wav"); Reject(() => RipService.WriteWave(mismatch, mismatch.Tracks[0], partial, true, _ => { }, CancellationToken.None), "Repeated read mismatch"); Check(!File.Exists(partial), "Mismatch partial removed");
            using var singles = new SyntheticDisc { SinglesOnly = true }; partial = Path.Combine(root, "singles.wav"); RipService.WriteWave(singles, singles.Tracks[0], partial, true, _ => { }, CancellationToken.None); Check(File.ReadAllBytes(partial).SequenceEqual(bytes), "Single-sector fallback exact output");
            Check(!Directory.EnumerateFiles(root, ".xd-rip-*").Any(), "Temporary export cleanup");
            lines.Add("PASS: cancellation, persistent errors, read mismatches, single-sector fallback and partial cleanup");
            lines.Add("NOTE: synthetic CD sectors and real FFmpeg encoding. Physical CD reading, drive compatibility and audible playback require manual hardware testing."); lines.Add("ALL CD RIPPER CHECKS PASSED");
        }
        catch (Exception e) { lines.Add("FAIL: " + e); throw; }
        finally { File.WriteAllLines(report, lines); Directory.Delete(root, true); }
    }
    sealed class SyntheticDisc : IDiscSource
    {
        public IReadOnlyList<CdTrack> Tracks { get; } = new[] { new CdTrack(1, 0, 75) }; public bool Fail, Mismatch, SinglesOnly; public int Reads;
        public byte[] Read(int start, int count)
        {
            Reads++; if (Fail || (SinglesOnly && count > 1)) throw new IOException("Injected read error"); var bytes = new byte[count * 2352];
            for (int i = 0; i < bytes.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i), (short)(Math.Sin((start * 588L + i / 4) * Math.PI * 2 * 440 / 44100) * 12000));
            if (Mismatch && Reads % 2 == 0) bytes[0] ^= 1; return bytes;
        }
        public void VerifyDisc() { } public void Dispose() { }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch { return; } throw new Exception("Expected rejection: " + message); }
}
