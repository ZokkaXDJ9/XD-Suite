using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XDBurner.Services;
using NAudio.Wave;

namespace XDBurner;

// Integration checks use the real Windows IMAPI/Media Foundation components; no disc is written or erased.
internal static class SelfTest
{
    public static void Run(string reportPath)
    {
        var lines = new List<string> { "XDBurner integration checks", DateTimeOffset.Now.ToString("O") };
        string directory = Path.Combine(Path.GetTempPath(), "XDBurner-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        try
        {
            var service = new DiscService();
            var recorders = service.GetRecorders();
            lines.Add($"PASS: IMAPI drive enumeration ({recorders.Count} recorder(s))");
            using (var scope = new ComScope())
            {
                object writer = scope.Create("IMAPI2.MsftDiscFormat2Data");
                using var dataEvents = new ProgressConnection(writer, "2735413C-7F64-5B0F-8F00-5D77AFBE261E", new Action<object, object>((_, _) => { }));
                ((IBurnVerification)writer).SetLevel(2);
                ((IBurnVerification)writer).GetLevel(out int level);
                Check(level == 2, "Full verification COM interface");
                object audio = scope.Create("IMAPI2.MsftDiscFormat2TrackAtOnce");
                using var audioEvents = new ProgressConnection(audio, "2735413F-7F64-5B0F-8F00-5D77AFBE261E", new Action<object, object>((_, _) => { }));
                object erase = scope.Create("IMAPI2.MsftDiscFormat2Erase");
                using var eraseEvents = new ProgressConnection(erase, "2735413A-7F64-5B0F-8F00-5D77AFBE261E", new Action<object, int, int>((_, _, _) => { }));
            }
            lines.Add("PASS: all three real COM event subscriptions and full verification setting");

            string source = Path.Combine(directory, "Erinnerungen");
            Directory.CreateDirectory(Path.Combine(source, "Unterordner", "Leer"));
            File.WriteAllText(Path.Combine(source, "Grüße.txt"), "Ein schöner Tag. Grüße von XDBurner!", Encoding.UTF8);
            File.WriteAllBytes(Path.Combine(source, "Unterordner", "Test.bin"), Enumerable.Range(0, 12345).Select(i => (byte)(i * 17)).ToArray());
            File.WriteAllText(Path.Combine(directory, "Notiz.txt"), "Datei direkt im Stammordner.");
            string[] sources = [source, Path.Combine(directory, "Notiz.txt")];
            var entries = ProjectFiles.Snapshot(sources);
            Check(entries.Count == 6, "Recursive snapshot including empty directory");
            Check(ProjectItem.FromData(source).Bytes == entries.Where(e => e.DiscPath.StartsWith("Erinnerungen/")).Sum(e => e.Length), "Folder size");
            ExpectFailure(() => ProjectFiles.Snapshot([source, source]), "Duplicate disc names");
            ExpectFailure(() => ProjectFiles.ValidateExportPath(Path.Combine(source, "bad.iso"), sources), "Export inside source folder");
            ExpectFailure(() => ProjectFiles.ValidateExportPath(sources[1], sources), "Export overwriting a source file");
            lines.Add("PASS: project traversal, empty directories, duplicate names and source protection");

            string iso = Path.Combine(directory, "test.iso");
            var progress = new InlineProgress();
            service.ExportIso(sources, "XD_BURNER_TEST", iso, progress, CancellationToken.None);
            byte[] image = File.ReadAllBytes(iso);
            Check(image.Length > 0 && image.Length % 2048 == 0, "ISO sector alignment");
            var files = ReadJoliet(image);
            foreach (var entry in entries)
            {
                Check(files.TryGetValue(entry.DiscPath, out var content), "ISO entry: " + entry.DiscPath);
                if (!entry.IsDirectory)
                    Check(content != null && SHA256.HashData(content).SequenceEqual(SHA256.HashData(File.ReadAllBytes(entry.SourcePath))), "ISO payload hash: " + entry.DiscPath);
            }
            using (var scope = new ComScope())
            {
                var stream = scope.Keep(Native.OpenRead(iso));
                DiscService.ValidateIso(stream, image.Length);
                byte[] start = new byte[16];
                stream.Read(start, start.Length, IntPtr.Zero);
                Check(start.All(b => b == 0), "ISO validation resets stream position");
            }
            lines.Add($"PASS: real IMAPI ISO export, Joliet Unicode paths, empty directories and SHA-256 content verification ({image.Length} bytes)");

            byte[] original = [4, 3, 2, 1];
            string existing = Path.Combine(directory, "existing.iso");
            File.WriteAllBytes(existing, original);
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                ExpectFailure(() => service.ExportIso(sources, "TEST", existing, progress, canceled.Token), "Export cancellation");
            }
            Check(File.ReadAllBytes(existing).SequenceEqual(original), "Canceled export preserves existing destination");
            ExpectFailure(() => service.ExportIso(sources, "INVALID/NAME", existing, progress, CancellationToken.None), "Invalid volume label");
            Check(File.ReadAllBytes(existing).SequenceEqual(original), "Failed export preserves existing destination");
            Check(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Temporary export cleanup");
            lines.Add("PASS: cancellation and invalid input preserve existing ISO files; temporary files cleaned up");

            string mono = Path.Combine(directory, "mono.wav");
            WriteSine(mono, 22050, 1, 1.25);
            Check(Math.Abs(AudioService.Inspect(mono).Duration.TotalSeconds - 1.25) < .02, "WAV inspection");
            string pcm = Path.Combine(directory, "mono.pcm");
            long sectors = AudioService.ConvertToPcm(mono, pcm, CancellationToken.None);
            byte[] samples = File.ReadAllBytes(pcm);
            Check(sectors == 300 && samples.Length == 300 * 2352, "Audio minimum 4 seconds and sector padding");
            Check(Encoding.ASCII.GetString(samples, 0, 4) != "RIFF", "No WAV header in raw CD samples");
            bool nonzero = false;
            for (int i = 0; i < 44100; i += 4)
            {
                short left = BinaryPrimitives.ReadInt16LittleEndian(samples.AsSpan(i));
                short right = BinaryPrimitives.ReadInt16LittleEndian(samples.AsSpan(i + 2));
                Check(left == right, "Mono becomes identical left/right channels");
                nonzero |= left != 0;
            }
            Check(nonzero, "Decoded audio is not silent");
            Check(samples.AsSpan(44100 * 4 * 2).ToArray().All(b => b == 0), "Short-track silence padding");
            string stereo = Path.Combine(directory, "stereo.wav");
            WriteSine(stereo, 48000, 2, 4.2);
            long stereoSectors = AudioService.ConvertToPcm(stereo, Path.Combine(directory, "stereo.pcm"), CancellationToken.None);
            Check(stereoSectors is >= 314 and <= 317, "48 kHz stereo resampling length");
            Check(ProjectFiles.AudioSectors([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)]) == 1350, "Audio capacity includes minimum track lengths and 2-second gaps");
            lines.Add("PASS: WAV decode, mono/stereo mapping, 22.05/48 kHz resampling, raw PCM, 4-second minimum, sector padding and gap budgeting");
            string fixture = Path.Combine(directory, "tone.mp3");
            using (var input = typeof(SelfTest).Assembly.GetManifestResourceStream("XDBurner.TestTone.mp3") ?? throw new InvalidOperationException("MP3 fixture missing"))
            using (var output = File.Create(fixture)) input.CopyTo(output);
            var mp3 = AudioService.Inspect(fixture);
            long mp3Sectors = AudioService.ConvertToPcm(fixture, Path.Combine(directory, "mp3.pcm"), CancellationToken.None);
            Check(mp3.Duration.TotalSeconds > 4 && mp3Sectors > 300, "MP3 decoder and PCM conversion");
            lines.Add("PASS: MP3 decoding through Windows Media Foundation and CD PCM conversion");
            lines.Add("NOT TESTED: physical CD-R/CD-RW writes, read-back verification, real burn progress/cancellation and erasure. Requires a burner and media.");
            lines.Add("ALL CHECKS PASSED");
        }
        catch (Exception ex)
        {
            lines.Add("FAIL: " + ex);
            throw;
        }
        finally
        {
            File.WriteAllLines(reportPath, lines);
            // Only paths created in this unique test directory are removed; never user files.
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) File.Delete(file);
            foreach (var folder in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length)) Directory.Delete(folder);
            Directory.Delete(directory);
        }
    }

    private static void WriteSine(string path, int rate, int channels, double seconds)
    {
        using var writer = new WaveFileWriter(path, new WaveFormat(rate, 16, channels));
        for (int i = 0; i < (int)(rate * seconds); i++)
            for (int channel = 0; channel < channels; channel++) writer.WriteSample((float)(.2 * Math.Sin(2 * Math.PI * 440 * i / rate)));
    }

    private static Dictionary<string, byte[]?> ReadJoliet(byte[] image)
    {
        int descriptor = 0;
        for (int sector = 16; sector < 32; sector++)
            if (image[sector * 2048] == 2 && Encoding.ASCII.GetString(image, sector * 2048 + 1, 5) == "CD001") { descriptor = sector * 2048; break; }
        Check(descriptor > 0, "Joliet volume descriptor");
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var root = image.AsSpan(descriptor + 156);
        ReadDirectory(Read32(root, 2), Read32(root, 10), "");
        return result;

        void ReadDirectory(int extent, int size, string prefix)
        {
            int start = extent * 2048, offset = 0;
            while (offset < size)
            {
                int length = image[start + offset];
                if (length == 0) { offset = (offset / 2048 + 1) * 2048; continue; }
                var record = image.AsSpan(start + offset, length);
                int nameLength = record[32];
                if (!(nameLength == 1 && record[33] <= 1))
                {
                    string name = Encoding.BigEndianUnicode.GetString(record.Slice(33, nameLength));
                    if (name.EndsWith(";1")) name = name[..^2];
                    string path = prefix + name;
                    int location = Read32(record, 2), bytes = Read32(record, 10);
                    if ((record[25] & 2) != 0) { result.Add(path, null); ReadDirectory(location, bytes, path + "/"); }
                    else result.Add(path, image.AsSpan(location * 2048, bytes).ToArray());
                }
                offset += length;
            }
        }
    }
    private static int Read32(ReadOnlySpan<byte> data, int offset) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]));
    private static void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); }
    private static void ExpectFailure(Action action, string description)
    {
        try { action(); } catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { return; }
        throw new InvalidOperationException(description + " did not reject invalid input");
    }
    private sealed class InlineProgress : IProgress<OperationProgress> { public void Report(OperationProgress value) { } }
}
