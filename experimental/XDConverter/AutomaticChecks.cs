using System.IO;
using System.Text.Json;

namespace XDConverter;

internal static class AutomaticChecks
{
    public static async Task Run(AudioEngine engine, AudioInfo input, string root, List<string> checks)
    {
        string folder = Path.Combine(root, "automatic"); Directory.CreateDirectory(folder);
        int conversions = 0;
        // Different source files must resolve independently in the same batch.
        var sources = new List<AudioInfo> { input };
        foreach (var (codec, rate, channels) in new[] { ("pcm_s16le", 8000, 1), ("pcm_s24le", 96000, 2), ("pcm_s32le", 192000, 2), ("pcm_f32le", 48000, 2), ("pcm_f64le", 88200, 2) })
        {
            string path = Path.Combine(folder, codec + ".wav");
            await ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-i", input.Path, "-ar", rate.ToString(), "-ac", channels.ToString(), "-c:a", codec, path], CancellationToken.None);
            sources.Add(await engine.Inspect(path, CancellationToken.None));
        }
        foreach (var source in sources)
            foreach (var format in OutputFormat.All)
            {
                string path = await engine.Convert(source, new(format, Path.Combine(folder, format.Id)), _ => { }, CancellationToken.None);
                var converted = await engine.Inspect(path, CancellationToken.None); conversions++;
                Require(converted.Channels == source.Channels && Math.Abs(converted.Duration.TotalSeconds - source.Duration.TotalSeconds) < .3, "Fully automatic conversion changed channel count or duration: " + format.Id);
                if (format.Id == "WAV") Require(converted.Codec == source.Codec && converted.SampleRate == source.SampleRate && converted.BitsPerSample == source.BitsPerSample, "Automatic WAV failed to retain source PCM depth/type/rate.");
                if (format.Id == "FLAC") Require(converted.SampleRate == source.SampleRate, "Automatic FLAC sample rate changed.");
                if (format.Id == "Opus") Require(converted.SampleRate == 48000, "Opus output reporting changed.");
            }
        // Each explicit lossy mode must also work with an automatic bitrate/quality.
        foreach (var format in OutputFormat.All.Where(f => f.Id is not ("WAV" or "FLAC")))
            foreach (var mode in EncodingProfile.Modes(format.Id))
                foreach (var source in sources.Take(2))
                {
                    string output = await engine.Convert(source, new(format, Path.Combine(folder, "modes"), Mode: mode), _ => { }, CancellationToken.None);
                    var converted = await engine.Inspect(output, CancellationToken.None); conversions++;
                    Require(converted.Channels == source.Channels && converted.Duration.TotalSeconds > 1, "Automatic bitrate/quality failed in " + mode);
                }
        var mp3 = OutputFormat.All.Single(f => f.Id == "MP3");
        string overridden = await engine.Convert(input, new(mp3, folder, SampleRate: 48000, Channels: 1), _ => { }, CancellationToken.None);
        var info = await engine.Inspect(overridden, CancellationToken.None);
        Require(info.SampleRate == 48000 && info.Channels == 1, "Automatic mode ignored a sample rate/channel override.");
        var wav = OutputFormat.All.Single(f => f.Id == "WAV");
        string depth = await engine.Convert(input, new(wav, folder, Quality: 24), _ => { }, CancellationToken.None);
        Require((await engine.Inspect(depth, CancellationToken.None)).BitsPerSample == 24, "Automatic conversion ignored a WAV depth override.");
        string high = await engine.Convert(input, new(mp3, folder, VbrQuality: 0), _ => { }, CancellationToken.None);
        string low = await engine.Convert(input, new(mp3, folder, VbrQuality: 9), _ => { }, CancellationToken.None);
        Require(new FileInfo(high).Length > new FileInfo(low).Length, "Manual V0/V9 quality overrides did not affect automatic MP3 encoding.");
        checks.Add($"PASS: {conversions} real automatic conversions across six formats, mono/8 kHz and high-resolution PCM/float sources; every lossy mode accepts automatic quality/bitrate; rate/channel/depth/VBR overrides take effect.");

        var defaults = JsonSerializer.Deserialize<PreviewSettings>("{\"Format\":\"MP3\",\"Quality\":192,\"Encoders\":{\"MP3\":{\"Mode\":0,\"Bitrate\":192,\"VbrQuality\":2,\"Depth\":16,\"Compression\":5,\"Application\":0}}}")!;
        var p = defaults.ForFormat("MP3");
        Require(p.Mode == EncodingMode.Automatic && p.Bitrate == 0 && p.VbrQuality == EncodingProfile.AutomaticQuality && p.Depth == 0 && p.Compression == -1 && p.Application == -1, "Old fixed defaults were not upgraded to Automatic.");
        p.Mode = EncodingMode.Cbr; p.Bitrate = 192;
        var saved = JsonSerializer.Deserialize<PreviewSettings>(JsonSerializer.Serialize(defaults))!;
        Require(saved.ForFormat("MP3").Mode == EncodingMode.Cbr && saved.ForFormat("MP3").Bitrate == 192, "Explicit settings were reset again after the one-time upgrade.");
        saved.UseAutomatic("MP3");
        Require(saved.ForFormat("MP3").Mode == EncodingMode.Automatic && saved.ForFormat("MP3").VbrQuality == EncodingProfile.AutomaticQuality, "Reset did not restore automatic preferences.");
        var custom = JsonSerializer.Deserialize<PreviewSettings>("{\"Encoders\":{\"MP3\":{\"Mode\":0,\"Bitrate\":320,\"VbrQuality\":2}}}")!;
        Require(custom.ForFormat("MP3").Mode == EncodingMode.Cbr && custom.ForFormat("MP3").Bitrate == 320, "Migration changed the encoding mode needed by a non-default manual bitrate.");
        var legacy = JsonSerializer.Deserialize<PreviewSettings>("{\"Format\":\"MP3\",\"Quality\":320}")!;
        Require(legacy.ForFormat("MP3").Mode == EncodingMode.Cbr && legacy.ForFormat("MP3").Bitrate == 320, "Legacy bitrate override lost its encoding mode.");
        checks.Add("PASS: old fixed defaults migrate once to Automatic; new explicit overrides survive save/load; reset restores automatic preferences.");
    }
    private static void Require(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }
}
