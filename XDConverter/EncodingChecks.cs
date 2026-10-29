using System.IO;
using System.Text;
using System.Text.Json;

namespace XDConverter;

internal static class EncodingChecks
{
    public static async Task Run(AudioEngine engine, AudioInfo input, string root, List<string> checks)
    {
        string folder = Path.Combine(root, "encoding-options");
        int conversions = 0;
        async Task<string> Encode(ConvertOptions options)
        {
            string path = await engine.Convert(input, options, _ => { }, CancellationToken.None);
            var result = await engine.Inspect(path, CancellationToken.None);
            int expected = options.Format.Id == "Opus" ? 48000 : EncodingProfile.EffectiveRate(input, options);
            Require(result.SampleRate == expected && result.Channels == (options.Channels == 0 ? input.Channels : options.Channels), "Encoded rate/channel mismatch: " + options);
            Require(Math.Abs(result.Duration.TotalSeconds - input.Duration.TotalSeconds) < .25, "Encoded duration mismatch: " + options);
            conversions++;
            return path;
        }
        foreach (var format in OutputFormat.All)
        {
            foreach (var mode in EncodingProfile.Modes(format.Id))
            {
                // Exercise every offered rate with each mode, including low-rate and high-resolution output.
                foreach (int rate in EncodingProfile.Rates(format.Id, mode))
                {
                    int quality = format.Id == "WAV" ? 16 : EncodingProfile.Bitrates(format.Id, rate, 2).MinBy(b => Math.Abs(b - 192));
                    await Encode(new(format, folder, quality, rate, 2, Mode: mode, VbrQuality: EncodingProfile.DefaultQuality(format.Id)));
                }
                if (EncodingProfile.UsesBitrate(format.Id, mode))
                    foreach (int bitrate in EncodingProfile.Bitrates(format.Id, 0, 2))
                        await Encode(new(format, folder, bitrate, Channels: 2, Mode: mode));
                if (EncodingProfile.UsesQuality(format.Id, mode))
                    foreach (int quality in EncodingProfile.Qualities(format.Id))
                        await Encode(new(format, folder, Channels: 2, Mode: mode, VbrQuality: quality));
            }
        }
        // Boundary combinations catch encoders that reject a rate/bitrate/channel tuple or silently round it.
        foreach (string id in new[] { "MP3", "M4A", "OGG" })
        {
            var format = OutputFormat.All.Single(f => f.Id == id);
            var mode = id == "MP3" ? EncodingMode.Cbr : EncodingMode.Abr;
            foreach (int rate in EncodingProfile.Rates(id, mode))
                foreach (int channels in new[] { 1, 2 })
                {
                    int[] bits = EncodingProfile.Bitrates(id, rate, channels);
                    foreach (int bit in new[] { bits[0], bits[^1] }.Distinct())
                    {
                        string output = await Encode(new(format, folder, bit, rate, channels, Mode: mode));
                        if (id == "MP3")
                        {
                            var probe = await Probe(engine, output, "stream=bit_rate");
                            using var json = JsonDocument.Parse(probe);
                            Require(long.Parse(json.RootElement.GetProperty("streams")[0].GetProperty("bit_rate").GetString()!) == bit * 1000, "MP3 CBR bitrate was rounded.");
                        }
                    }
                }
        }
        var wav = OutputFormat.All.Single(f => f.Id == "WAV");
        foreach (var (depth, codec) in new[] { (8, "pcm_u8"), (16, "pcm_s16le"), (24, "pcm_s24le"), (33, "pcm_s32le"), (32, "pcm_f32le"), (64, "pcm_f64le") })
        {
            string output = await Encode(new(wav, folder, depth));
            Require((await engine.Inspect(output, CancellationToken.None)).Codec == codec, "WAV bit-depth codec mismatch.");
        }
        var flac = OutputFormat.All.Single(f => f.Id == "FLAC");
        string pcm = Path.Combine(folder, "source.pcm");
        await Decode(engine, input.Path, pcm);
        byte[] expectedPcm = File.ReadAllBytes(pcm);
        foreach (int level in Enumerable.Range(0, 13))
        {
            string output = await Encode(new(flac, folder, Compression: level));
            string raw = Path.Combine(folder, "flac-level-" + level + ".pcm"); await Decode(engine, output, raw);
            Require(expectedPcm.SequenceEqual(File.ReadAllBytes(raw)), "FLAC compression changed PCM samples.");
        }
        foreach (int depth in new[] { 16, 24 })
        {
            string output = await Encode(new(flac, folder, BitDepth: depth));
            string probe = await Probe(engine, output, "stream=bits_per_raw_sample");
            using var json = JsonDocument.Parse(probe);
            Require(json.RootElement.GetProperty("streams")[0].GetProperty("bits_per_raw_sample").GetString() == depth.ToString(), "FLAC bit-depth mismatch.");
        }
        var opus = OutputFormat.All.Single(f => f.Id == "Opus");
        foreach (string app in new[] { "audio", "voip", "lowdelay" }) await Encode(new(opus, folder, Application: app));
        checks.Add($"PASS: {conversions} real conversions cover every rate, bitrate, VBR quality and mode; mono/stereo bitrate boundaries, six WAV depths, FLAC levels 0-12 with byte-identical decoded PCM, FLAC 16/24 bits and three Opus applications.");

        string dynamic = Path.Combine(folder, "changing-audio.wav"); WriteDynamicAudio(dynamic);
        var dynamicInfo = await engine.Inspect(dynamic, CancellationToken.None);
        foreach (string id in new[] { "MP3", "Opus" })
        {
            var format = OutputFormat.All.Single(f => f.Id == id);
            string cbr = await engine.Convert(dynamicInfo, new(format, folder, 128, Mode: EncodingMode.Cbr), _ => { }, CancellationToken.None);
            string vbr = await engine.Convert(dynamicInfo, new(format, folder, 128, Mode: EncodingMode.Vbr), _ => { }, CancellationToken.None);
            int[] fixedPackets = await PacketSizes(engine, cbr), variablePackets = await PacketSizes(engine, vbr);
            Require(fixedPackets.Max() - fixedPackets.Min() <= 1, id + " CBR packets vary beyond frame padding.");
            Require(variablePackets.Max() - variablePackets.Min() > 32, id + " VBR packets did not vary with the audio content.");
        }
        checks.Add("PASS: actual MP3/Opus packet sizes confirm constant bitrate (padding only) and content-dependent VBR on changing audio.");

        var old = JsonSerializer.Deserialize<ConverterSettings>("{\"Format\":\"WAV\",\"Quality\":24,\"Rate\":48000,\"Channels\":1,\"English\":true,\"OutputDirectory\":\"C:\\\\Audio\",\"Tags\":false}")!;
        Require(old.ForFormat("WAV").Depth == 24 && old.ForFormat("WAV").Rate == 48000 && old.ForFormat("WAV").Channels == 1 && !old.Tags && old.English && old.OutputDirectory == @"C:\Audio", "Legacy preferences not preserved.");
        old.ForFormat("MP3").Mode = EncodingMode.Vbr; old.ForFormat("MP3").VbrQuality = 0;
        var restored = JsonSerializer.Deserialize<ConverterSettings>(JsonSerializer.Serialize(old))!;
        Require(restored.ForFormat("MP3").Mode == EncodingMode.Vbr && restored.ForFormat("MP3").VbrQuality == 0 && restored.ForFormat("WAV").Depth == 24, "Per-format preference round trip failed.");
        checks.Add("PASS: legacy preferences imported, folder/language/tags retained and per-format encoding preferences survive serialization.");
    }
    private static void Require(bool okay, string description) { if (!okay) throw new InvalidOperationException(description); }
    private static async Task<string> Probe(AudioEngine engine, string path, string entries) => (await ProcessRunner.Run(engine.Ffprobe, ["-v", "error", "-show_entries", entries, "-of", "json", path], CancellationToken.None)).Output;
    private static Task<ProcessResult> Decode(AudioEngine engine, string path, string raw) => ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-i", path, "-f", "s16le", "-c:a", "pcm_s16le", raw], CancellationToken.None);
    private static async Task<int[]> PacketSizes(AudioEngine engine, string path)
    {
        using var json = JsonDocument.Parse(await Probe(engine, path, "packet=size"));
        return json.RootElement.GetProperty("packets").EnumerateArray().Select(p => int.Parse(p.GetProperty("size").GetString()!)).ToArray();
    }
    private static void WriteDynamicAudio(string path)
    {
        const int frames = 44100 * 6, bytes = frames * 4;
        using var output = new BinaryWriter(File.Create(path), Encoding.ASCII);
        output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(bytes + 36); output.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1); output.Write((short)2); output.Write(44100); output.Write(176400); output.Write((short)4); output.Write((short)16); output.Write(Encoding.ASCII.GetBytes("data")); output.Write(bytes);
        var random = new Random(731);
        for (int i = 0; i < frames; i++)
        {
            short sample = i < 44100 ? (short)0 : i < 44100 * 3 ? (short)(Math.Sin(i * Math.PI * 2 * 440 / 44100) * 12000) : (short)random.Next(-22000, 22001);
            output.Write(sample); output.Write(sample);
        }
    }
}
