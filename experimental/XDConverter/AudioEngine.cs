using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace XDConverter;

public sealed record AudioInfo(string Path, TimeSpan Duration, int SampleRate, int Channels, string Codec, long Bytes, int BitsPerSample = 0);
public sealed record OutputFormat(string Id, string Extension, string Encoder)
{
    public static readonly OutputFormat[] All = [new("MP3", ".mp3", "libmp3lame"), new("WAV", ".wav", "pcm_s16le"), new("FLAC", ".flac", "flac"), new("OGG", ".ogg", "libvorbis"), new("M4A", ".m4a", "aac"), new("Opus", ".opus", "libopus")];
    public override string ToString() => Id;
}
public sealed record ConvertOptions(OutputFormat Format, string Directory, int Quality = 0, int SampleRate = 0, int Channels = 0, bool Tags = true,
    EncodingMode? Mode = null, int VbrQuality = EncodingProfile.AutomaticQuality, int Compression = -1, int BitDepth = 0, string Application = "auto");
public sealed class ConversionError(string key, string detail = "") : Exception(detail)
{
    public string Key { get; } = key;
}

public sealed class AudioEngine(string directory)
{
    public string DirectoryPath { get; } = System.IO.Path.GetFullPath(directory);
    public string Ffmpeg => System.IO.Path.Combine(DirectoryPath, "ffmpeg.exe");
    public string Ffprobe => System.IO.Path.Combine(DirectoryPath, "ffprobe.exe");
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".wave", ".flac", ".ogg", ".m4a", ".aac", ".opus", ".wma", ".aif", ".aiff", ".aifc", ".ape", ".wv", ".mp2", ".amr", ".ac3", ".dts", ".caf", ".au" };

    public async Task<string> Check(CancellationToken token)
    {
        if (!File.Exists(Ffmpeg) || !File.Exists(Ffprobe)) throw new ConversionError("EngineMissing");
        var version = await ProcessRunner.Run(Ffmpeg, ["-version"], token);
        var encoders = await ProcessRunner.Run(Ffmpeg, ["-hide_banner", "-encoders"], token);
        foreach (string encoder in OutputFormat.All.Select(p => p.Encoder))
            if (!encoders.Output.Contains(encoder, StringComparison.Ordinal)) throw new ConversionError("EncoderMissing", encoder);
        return version.Output.Split('\n')[0].Trim();
    }

    public async Task<AudioInfo> Inspect(string input, CancellationToken token)
    {
        string path = System.IO.Path.GetFullPath(input);
        if (!File.Exists(path)) throw new ConversionError("FileMissing", path);
        if (!Extensions.Contains(System.IO.Path.GetExtension(path))) throw new ConversionError("AudioOnly");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var result = await ProcessRunner.Run(Ffprobe, ["-v", "error", "-protocol_whitelist", "file", "-show_entries", "format=duration:stream=codec_type,codec_name,sample_rate,channels,duration,bits_per_sample,bits_per_raw_sample:stream_disposition=attached_pic", "-of", "json", "-i", path], timeout.Token);
        using var document = JsonDocument.Parse(result.Output);
        var streams = document.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        if (streams.Any(s => s.GetProperty("codec_type").GetString() == "video" && (!s.TryGetProperty("disposition", out var d) || !d.TryGetProperty("attached_pic", out var a) || a.GetInt32() != 1))) throw new ConversionError("AudioOnly");
        var audio = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "audio");
        if (audio.ValueKind == JsonValueKind.Undefined) throw new ConversionError("NoAudio");
        double seconds = 0;
        if (document.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var duration)) double.TryParse(duration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        if (seconds <= 0 && audio.TryGetProperty("duration", out duration)) double.TryParse(duration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        int bits = 0;
        if (audio.TryGetProperty("bits_per_raw_sample", out var rawBits)) int.TryParse(rawBits.ToString(), out bits);
        if (bits == 0 && audio.TryGetProperty("bits_per_sample", out var sampleBits)) int.TryParse(sampleBits.ToString(), out bits);
        return new(path, TimeSpan.FromSeconds(double.IsFinite(seconds) && seconds > 0 ? seconds : 0), int.Parse(audio.GetProperty("sample_rate").GetString()!, CultureInfo.InvariantCulture), audio.GetProperty("channels").GetInt32(), audio.GetProperty("codec_name").GetString() ?? "", new FileInfo(path).Length, bits);
    }

    public async Task<string> Convert(AudioInfo input, ConvertOptions options, Action<double> progress, CancellationToken token)
    {
        options = EncodingProfile.Resolve(input, options);
        token.ThrowIfCancellationRequested();
        string outputDirectory = System.IO.Path.GetFullPath(options.Directory);
        Directory.CreateDirectory(outputDirectory);
        string temporary = System.IO.Path.Combine(outputDirectory, ".xd-convert-" + Guid.NewGuid().ToString("N") + options.Format.Extension);
        try
        {
            var args = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error", "-xerror", "-n", "-protocol_whitelist", "file", "-i", input.Path, "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", options.Tags ? "0" : "-1", "-c:a" };
            args.Add(options.Format.Id == "WAV" ? options.Quality switch { 8 => "pcm_u8", 24 => "pcm_s24le", 32 => "pcm_f32le", 33 => "pcm_s32le", 64 => "pcm_f64le", _ => "pcm_s16le" } : options.Format.Encoder);
            var mode = options.Mode!.Value;
            if (options.Format.Id == "FLAC")
            {
                args.AddRange(["-compression_level", options.Compression.ToString(CultureInfo.InvariantCulture)]);
                if (options.BitDepth != 0) args.AddRange(["-sample_fmt", options.BitDepth == 16 ? "s16" : "s32", "-bits_per_raw_sample", options.BitDepth.ToString(CultureInfo.InvariantCulture)]);
            }
            else if (EncodingProfile.UsesQuality(options.Format.Id, mode)) args.AddRange(["-q:a", options.VbrQuality.ToString(CultureInfo.InvariantCulture)]);
            else if (EncodingProfile.UsesBitrate(options.Format.Id, mode)) args.AddRange(["-b:a", options.Quality.ToString(CultureInfo.InvariantCulture) + "k"]);
            if (options.Format.Id == "MP3") args.AddRange(["-abr", mode == EncodingMode.Abr ? "1" : "0"]);
            if (options.Format.Id == "Opus") args.AddRange(["-vbr", mode == EncodingMode.Cbr ? "off" : mode == EncodingMode.ConstrainedVbr ? "constrained" : "on", "-application", options.Application]);
            if (options.Format.Id == "MP3") args.AddRange(["-id3v2_version", "3"]);
            args.AddRange(["-ar", options.SampleRate.ToString(CultureInfo.InvariantCulture)]);
            if (options.Channels != 0) args.AddRange(["-ac", options.Channels.ToString(CultureInfo.InvariantCulture)]);
            else if (input.Channels > 2 && options.Format.Id is "MP3" or "M4A" or "OGG" or "Opus") args.AddRange(["-ac", "2"]);
            args.AddRange(["-stats_period", "0.2", "-progress", "pipe:1", "-nostats", temporary]);
            await ProcessRunner.Run(Ffmpeg, args, token, line =>
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && long.TryParse(line[12..], CultureInfo.InvariantCulture, out long microseconds) && input.Duration.TotalSeconds > 0)
                    progress(Math.Clamp(microseconds / 1_000_000d / input.Duration.TotalSeconds, 0, .99));
            });
            token.ThrowIfCancellationRequested();
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0) throw new ConversionError("EmptyOutput");
            string stem = System.IO.Path.GetFileNameWithoutExtension(input.Path);
            for (int i = 0; i < 10000; i++)
            {
                token.ThrowIfCancellationRequested();
                string target = System.IO.Path.Combine(outputDirectory, stem + (i == 0 ? "" : $" ({i})") + options.Format.Extension);
                if (target.Equals(input.Path, StringComparison.OrdinalIgnoreCase) || File.Exists(target) || Directory.Exists(target)) continue;
                try { File.Move(temporary, target, false); progress(1); return target; }
                catch (IOException) when (File.Exists(target) || Directory.Exists(target)) { }
            }
            throw new ConversionError("NameCollision");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal sealed record ProcessResult(string Output, string Error);
internal static class ProcessRunner
{
    internal static async Task<ProcessResult> Run(string executable, IEnumerable<string> arguments, CancellationToken token, Action<string>? lineReceived = null)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = System.IO.Path.GetDirectoryName(executable)! };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new ConversionError("EngineFailed");
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
        var output = Read(process.StandardOutput, lineReceived);
        var error = Read(process.StandardError, null);
        await Task.WhenAll(process.WaitForExitAsync(), output, error);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new ConversionError("EngineFailed", await error);
        return new(await output, await error);
    }
    private static async Task<string> Read(StreamReader reader, Action<string>? received)
    {
        var text = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            received?.Invoke(line);
            text.AppendLine(line);
            if (text.Length > 65536) text.Remove(0, text.Length - 65536);
        }
        return text.ToString();
    }
}
