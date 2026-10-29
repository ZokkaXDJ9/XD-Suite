using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace XDWaveEditor;

public sealed class EditorError(string key, string detail = "") : Exception(detail) { public string Key { get; } = key; }
public sealed partial class AudioEngine(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string Ffmpeg => Path.Combine(DirectoryPath, "ffmpeg.exe");
    public string Ffprobe => Path.Combine(DirectoryPath, "ffprobe.exe");
    public static readonly string[] Extensions = [".wav", ".wave", ".mp3", ".flac", ".ogg", ".m4a", ".aac", ".opus", ".wma", ".aif", ".aiff"];
    public static readonly string[] ExportExtensions = [".wav", ".mp3", ".flac", ".ogg", ".m4a", ".opus"];
    public static string NewCachePath(string extension) => Path.Combine(Path.GetTempPath(), "XDWave-" + Guid.NewGuid().ToString("N") + extension);
    public async Task<AudioDocument> Open(string filename, CancellationToken token)
    {
        if (!File.Exists(Ffmpeg) || !File.Exists(Ffprobe)) throw new EditorError("EngineMissing");
        string path = Path.GetFullPath(filename);
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) throw new EditorError("AudioOnly");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string json = await Run(Ffprobe, ["-v", "error", "-protocol_whitelist", "file", "-show_entries", "stream=codec_type,sample_rate,channels:stream_disposition=attached_pic", "-of", "json", "-i", path], timeout.Token);
        using var metadata = JsonDocument.Parse(json);
        var streams = metadata.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        if (streams.Any(s => s.GetProperty("codec_type").GetString() == "video" && (!s.TryGetProperty("disposition", out var d) || !d.TryGetProperty("attached_pic", out var a) || a.GetInt32() != 1))) throw new EditorError("AudioOnly");
        var audio = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "audio");
        if (audio.ValueKind == JsonValueKind.Undefined) throw new EditorError("InvalidAudio");
        int rate = int.Parse(audio.GetProperty("sample_rate").GetString()!, CultureInfo.InvariantCulture), channels = audio.GetProperty("channels").GetInt32();
        if (channels is < 1 or > 2) throw new EditorError("StereoOnly");
        string cache = NewCachePath(".pcm"); PcmSource? source = null;
        try
        {
            await Run(Ffmpeg, ["-nostdin", "-v", "error", "-xerror", "-n", "-protocol_whitelist", "file", "-i", path, "-map", "0:a:0", "-vn", "-sn", "-dn", "-c:a", "pcm_f32le", "-f", "f32le", cache], token);
            source = new(cache, rate, channels);
            await Task.Run(() => source.BuildPeaks(token), token);
            var document = new AudioDocument(source, path); document.MarkSaved(); return document;
        }
        catch { source?.Dispose(); if (File.Exists(cache)) File.Delete(cache); throw; }
    }
    public async Task Export(AudioDocument document, string filename, bool overwrite, CancellationToken token)
    {
        string target = Path.GetFullPath(filename), extension = Path.GetExtension(target).ToLowerInvariant();
        if (document.IsOriginal(target)) throw new EditorError("OriginalProtected");
        if (!ExportExtensions.Contains(extension)) throw new EditorError("UnsupportedExport");
        if (document.Frames == 0) throw new EditorError("EmptySelection");
        if (File.Exists(target) && !overwrite) throw new EditorError("TargetExists");
        string raw = NewCachePath(".pcm"), output = Path.Combine(Path.GetDirectoryName(target)!, ".xd-wave-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            await Task.Run(() => WriteRaw(document, new(0, document.Frames), raw, token), token);
            var args = new List<string> { "-nostdin", "-v", "error", "-xerror", "-n", "-f", "f32le", "-ar", document.Source.Rate.ToString(CultureInfo.InvariantCulture), "-ac", document.Source.Channels.ToString(CultureInfo.InvariantCulture), "-i", raw, "-map_metadata", "-1", "-c:a" };
            args.Add(extension switch { ".wav" => "pcm_s24le", ".mp3" => "libmp3lame", ".flac" => "flac", ".ogg" => "libvorbis", ".m4a" => "aac", ".opus" => "libopus", _ => throw new EditorError("UnsupportedExport") });
            if (extension == ".mp3") args.AddRange(["-q:a", "2", "-ar", document.Source.Rate <= 24000 ? "24000" : document.Source.Rate <= 32000 ? "32000" : document.Source.Rate <= 44100 ? "44100" : "48000"]);
            if (extension == ".flac") args.AddRange(["-sample_fmt", "s32", "-bits_per_raw_sample", "24"]);
            if (extension == ".ogg") args.AddRange(["-q:a", "5"]);
            if (extension == ".m4a") args.AddRange(["-b:a", "192k", "-ar", Math.Min(document.Source.Rate, 96000).ToString(CultureInfo.InvariantCulture)]);
            if (extension == ".opus") args.AddRange(["-b:a", "128k", "-ar", "48000"]);
            args.Add(output); await Run(Ffmpeg, args, token); token.ThrowIfCancellationRequested();
            if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new EditorError("InvalidAudio");
            File.Move(output, target, overwrite); document.MarkSaved();
        }
        finally { if (File.Exists(raw)) File.Delete(raw); if (File.Exists(output)) File.Delete(output); }
    }
    private static void WriteRaw(AudioDocument document, FrameRange range, string path, CancellationToken token)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        document.Visit(range, (_, samples) => output.Write(MemoryMarshal.AsBytes(samples.AsSpan())), token);
    }
    public static void WritePreview(AudioDocument document, string path, CancellationToken token)
    {
        long size = document.Frames * document.Source.Channels * 2;
        if (size > uint.MaxValue - 44) throw new EditorError("PreviewTooLong");
        using var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        writer.Write("RIFF"u8.ToArray()); writer.Write((uint)(size + 36)); writer.Write("WAVEfmt "u8.ToArray()); writer.Write(16);
        writer.Write((short)1); writer.Write((short)document.Source.Channels); writer.Write(document.Source.Rate);
        writer.Write(document.Source.Rate * document.Source.Channels * 2); writer.Write((short)(document.Source.Channels * 2)); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write((uint)size);
        document.Visit(new(0, document.Frames), (_, samples) =>
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short value = (short)Math.Round(Math.Clamp(float.IsFinite(samples[i]) ? samples[i] : 0, -1, 1) * 32767);
                bytes[i * 2] = (byte)value; bytes[i * 2 + 1] = (byte)(value >> 8);
            }
            writer.Write(bytes);
        }, token);
    }
    internal static async Task<string> Run(string executable, IEnumerable<string> arguments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new EditorError("EngineFailed");
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(process.WaitForExitAsync(), output, errors);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new EditorError("EngineFailed", (await errors)[..Math.Min(2000, (await errors).Length)]);
        return await output;
    }
}
