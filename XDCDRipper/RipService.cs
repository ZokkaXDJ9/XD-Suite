using System.Diagnostics;
using System.IO;
using System.Text;

namespace XDCDRipper;
public sealed record RipTrack(CdTrack Track, string Title);
public sealed record RipOptions(string Directory, string Format, string Artist, string Album, string Year, string Genre, bool DoubleRead = true, int Mp3Bitrate = 192);
public static class RipService
{
    public static string SafeName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        value = value.Trim().TrimEnd('.'); if (value.Length > 150) value = value[..150]; return value.Length == 0 ? "Track" : value;
    }
    public static void WriteWave(IDiscSource source, CdTrack track, string path, bool doubleRead, Action<int> progress, CancellationToken token)
    {
        if (track.Sectors <= 0 || track.StartSector < 0 || track.Sectors > (uint.MaxValue - 36L) / 2352) throw new ArgumentException("Ungültige Titellänge / Invalid track length.");
        bool created = false;
        try
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); using var writer = new BinaryWriter(file, Encoding.ASCII);
            created = true;
            uint size = checked((uint)(track.Sectors * 2352L));
            writer.Write("RIFF"u8); writer.Write(size + 36); writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(44100); writer.Write(176400); writer.Write((ushort)4); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(size);
            for (int sector = track.StartSector; sector < track.EndSector;)
            {
                token.ThrowIfCancellationRequested(); int count = Math.Min(16, track.EndSector - sector);
                byte[] data;
                try { data = ReadChecked(source, sector, count, doubleRead, token); }
                catch (IOException) when (count > 1)
                {
                    data = new byte[count * 2352]; for (int i = 0; i < count; i++) ReadChecked(source, sector + i, 1, doubleRead, token).CopyTo(data, i * 2352);
                }
                writer.Write(data); sector += count; progress(sector - track.StartSector);
            }
            token.ThrowIfCancellationRequested(); source.VerifyDisc(); writer.Flush(); file.Flush(true);
        }
        catch { if (created && File.Exists(path)) File.Delete(path); throw; }
    }
    static byte[] ReadChecked(IDiscSource source, int start, int count, bool doubleRead, CancellationToken token)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { var data = source.Read(start, count); if (data.Length != count * 2352) throw new IOException("Unvollständiger Block / Incomplete block."); token.ThrowIfCancellationRequested(); if (!doubleRead || data.SequenceEqual(source.Read(start, count))) return data; last = new IOException("Abweichende Lesedaten / Read mismatch."); }
            catch (IOException error) { last = error; }
        }
        throw new IOException("Lesefehler bei Sektor / Read error at sector " + start, last);
    }
    public static async Task<string> Export(string engine, string wave, RipTrack track, RipOptions options, CancellationToken token)
    {
        if (options.Format is not ("WAV" or "FLAC" or "MP3") || options.Mp3Bitrate is not (128 or 192 or 256 or 320)) throw new ArgumentException("Ungültiges Ausgabeformat / Invalid output format.");
        string extension = "." + options.Format.ToLowerInvariant(); string temporary = Path.Combine(options.Directory, ".xd-rip-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            var args = new List<string> { "-nostdin", "-v", "error", "-xerror", "-n", "-i", wave, "-map", "0:a:0", "-map_metadata", "-1", "-c:a", options.Format switch { "WAV" => "pcm_s16le", "FLAC" => "flac", _ => "libmp3lame" } };
            if (options.Format == "MP3") args.AddRange(["-b:a", options.Mp3Bitrate + "k", "-id3v2_version", "3"]);
            foreach (var (key, value) in new[] { ("title", track.Title), ("artist", options.Artist), ("album", options.Album), ("date", options.Year), ("genre", options.Genre), ("track", track.Track.Number.ToString()) }) { args.Add("-metadata"); args.Add(key + "=" + value); }
            args.Add(temporary); await Run(engine, args, token); token.ThrowIfCancellationRequested();
            string stem = track.Track.Number.ToString("00") + " - " + SafeName(track.Title);
            for (int suffix = 0; suffix < 10000; suffix++)
            {
                string output = Path.Combine(options.Directory, stem + (suffix == 0 ? "" : " (" + suffix + ")") + extension);
                try { File.Move(temporary, output, false); return output; } catch (IOException) when (File.Exists(output)) { }
            }
            throw new IOException("Kein freier Dateiname / No available filename.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task<string> Run(string engine, IEnumerable<string> args, CancellationToken token)
    {
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 }; foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Encoder konnte nicht gestartet werden / Could not start encoder.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); } catch { try { process.Kill(true); } catch (InvalidOperationException) { } await process.WaitForExitAsync(); throw; }
        string details = await error; if (process.ExitCode != 0) throw new IOException(details); return await output;
    }
}
