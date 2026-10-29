using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace XDWaveEditor;

public enum EffectKind { Normalize, Gain, FadeIn, FadeOut, Mute, Reverse, Invert, Repeat, InsertSilence, Tempo, Pitch, Equalizer, HighPass, LowPass, Denoise, Compressor }
public sealed record EffectParameter(string Key, double Minimum, double Maximum, double Default, double Step, string Unit);
public sealed record EffectDefinition(EffectKind Kind, EffectParameter[] Parameters, bool CanClip = false)
{
    public string Name => Texts.Current["Fx" + Kind];
    public string Help => Texts.Current["Fx" + Kind + "Help"];
    public override string ToString() => Name;
}
public sealed record EffectSettings(EffectKind Kind, double[] Values, bool AllowClipping = false, bool RemoveDc = true);
public static class Effects
{
    public static EffectDefinition[] ForRate(int rate) =>
    [
        new(EffectKind.Normalize, [new("TargetPeak", -24, 0, -1, .1, "dB")]),
        new(EffectKind.Gain, [new("GainAmount", -24, 24, 3, .1, "dB")], true),
        new(EffectKind.FadeIn, []), new(EffectKind.FadeOut, []), new(EffectKind.Mute, []), new(EffectKind.Reverse, []), new(EffectKind.Invert, []),
        new(EffectKind.Repeat, [new("Copies", 2, 20, 2, 1, "×")]),
        new(EffectKind.InsertSilence, [new("SilenceLength", .01, 60, 1, .01, "s")]),
        new(EffectKind.Tempo, [new("TempoPercent", 50, 200, 100, 1, "%")], true),
        new(EffectKind.Pitch, [new("Semitones", -12, 12, 0, .1, "")], true),
        new(EffectKind.Equalizer, [new("Bass", -12, 12, 0, .5, "dB"), new("Mids", -12, 12, 0, .5, "dB"), new("Treble", -12, 12, 0, .5, "dB")], true),
        new(EffectKind.HighPass, [new("Cutoff", 20, Math.Min(20000, rate * .45), 80, 1, "Hz")], true),
        new(EffectKind.LowPass, [new("Cutoff", 20, Math.Min(20000, rate * .45), Math.Min(5000, rate * .45), 1, "Hz")], true),
        new(EffectKind.Denoise, [new("NoiseReduction", 1, 30, 12, .5, "dB"), new("NoiseFloor", -80, -20, -50, 1, "dB")], true),
        new(EffectKind.Compressor, [new("Threshold", -48, -1, -18, .5, "dB"), new("Ratio", 1, 10, 3, .1, ":1"), new("Attack", 1, 100, 10, 1, "ms"), new("Release", 10, 1000, 120, 5, "ms")], true)
    ];
    public static EffectSettings Defaults(EffectKind kind, int rate) => new(kind, ForRate(rate).Single(d => d.Kind == kind).Parameters.Select(p => p.Default).ToArray());
    internal static void Validate(EffectSettings settings, int rate)
    {
        var definition = ForRate(rate).SingleOrDefault(d => d.Kind == settings.Kind) ?? throw new EditorError("InvalidEffect");
        if (settings.Values.Length != definition.Parameters.Length) throw new EditorError("InvalidEffect");
        for (int i = 0; i < settings.Values.Length; i++)
            if (!double.IsFinite(settings.Values[i]) || settings.Values[i] < definition.Parameters[i].Minimum || settings.Values[i] > definition.Parameters[i].Maximum) throw new EditorError("InvalidEffect");
        if (settings.Kind == EffectKind.Repeat && settings.Values[0] != Math.Truncate(settings.Values[0])) throw new EditorError("InvalidEffect");
    }
}

public sealed partial class AudioEngine
{
    public async Task ApplyEffect(AudioDocument document, FrameRange range, EffectSettings settings, CancellationToken token)
    {
        Effects.Validate(settings, document.Source.Rate); token.ThrowIfCancellationRequested();
        Guid revision = document.Revision;
        if (settings.Kind == EffectKind.Repeat) { document.Repeat(range, (int)settings.Values[0]); return; }
        bool insert = settings.Kind == EffectKind.InsertSilence;
        if (!insert) document.ValidateRange(range);
        else if (range.Start < 0 || range.Start > document.Frames) throw new EditorError("InvalidEffect");
        string output = NewCachePath(".pcm"), input = NewCachePath(".pcm"); PcmSource? material = null;
        try
        {
            if (settings.Kind is EffectKind.Gain or EffectKind.Normalize or EffectKind.FadeIn or EffectKind.FadeOut or EffectKind.Mute or EffectKind.Reverse or EffectKind.Invert or EffectKind.InsertSilence)
                await Task.Run(() => RenderSamples(document, range, settings, output, token), token);
            else
            {
                await Task.Run(() => WriteRaw(document, range, input, token), token);
                string filter = Filter(settings, document.Source.Rate);
                long length = settings.Kind == EffectKind.Tempo ? Math.Max(1, (long)Math.Round(range.Length * 100d / settings.Values[0])) : range.Length;
                filter += $",apad=whole_len={length},atrim=end_sample={length}";
                await Run(Ffmpeg, ["-nostdin", "-v", "error", "-xerror", "-n", "-f", "f32le", "-ar", document.Source.Rate.ToString(CultureInfo.InvariantCulture), "-ac", document.Source.Channels.ToString(CultureInfo.InvariantCulture), "-i", input, "-af", filter, "-c:a", "pcm_f32le", "-f", "f32le", output], token);
            }
            material = new(output, document.Source.Rate, document.Source.Channels);
            await Task.Run(() => material.BuildPeaks(token), token);
            if (!settings.AllowClipping && Effects.ForRate(document.Source.Rate).Single(d => d.Kind == settings.Kind).CanClip && material.MaxAmplitude > 1.000001f) throw new EditorError("WouldClip");
            token.ThrowIfCancellationRequested();
            document.Replace(insert ? new(range.Start, range.Start) : range, material, revision);
            material = null; // The document now owns this immutable material, including its undo history.
        }
        finally
        {
            if (material != null && !document.OwnsMaterial(material.Path)) material.Dispose();
            if (File.Exists(input)) File.Delete(input);
            if (!document.OwnsMaterial(output) && File.Exists(output)) File.Delete(output);
        }
    }
    public async Task InsertAudio(AudioDocument document, string filename, CancellationToken token)
    {
        Guid revision = document.Revision;
        long cursor = Math.Clamp(document.Cursor, 0, document.Frames);
        using var imported = await Open(filename, token);
        string path = NewCachePath(".pcm"); PcmSource? material = null;
        try
        {
            await Run(Ffmpeg, ["-nostdin", "-v", "error", "-xerror", "-n", "-f", "f32le", "-ar", imported.Source.Rate.ToString(CultureInfo.InvariantCulture), "-ac", imported.Source.Channels.ToString(CultureInfo.InvariantCulture), "-i", imported.Source.Path, "-ar", document.Source.Rate.ToString(CultureInfo.InvariantCulture), "-ac", document.Source.Channels.ToString(CultureInfo.InvariantCulture), "-c:a", "pcm_f32le", "-f", "f32le", path], token);
            material = new(path, document.Source.Rate, document.Source.Channels);
            await Task.Run(() => material.BuildPeaks(token), token); token.ThrowIfCancellationRequested();
            document.Replace(new(cursor, cursor), material, revision, filename); material = null;
        }
        finally
        {
            if (material != null && !document.OwnsMaterial(material.Path)) material.Dispose();
            if (!document.OwnsMaterial(path) && File.Exists(path)) File.Delete(path);
        }
    }
    private static string Filter(EffectSettings settings, int rate)
    {
        string N(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);
        double[] v = settings.Values;
        return settings.Kind switch
        {
            EffectKind.Tempo => "atempo=" + N(v[0] / 100),
            EffectKind.Pitch => $"asetrate={Math.Round(rate * Math.Pow(2, v[0] / 12))},aresample={rate},atempo={N(1 / Math.Pow(2, v[0] / 12))}",
            EffectKind.Equalizer => $"bass=g={N(v[0])}:f=120,equalizer=f={Math.Min(1000, rate * .2).ToString(CultureInfo.InvariantCulture)}:t=q:w=1:g={N(v[1])},treble=g={N(v[2])}:f={Math.Min(6000, rate * .3).ToString(CultureInfo.InvariantCulture)}",
            EffectKind.HighPass => "highpass=f=" + N(v[0]),
            EffectKind.LowPass => "lowpass=f=" + N(v[0]),
            EffectKind.Denoise => $"afftdn=nr={N(v[0])}:nf={N(v[1])}:tn=1",
            EffectKind.Compressor => $"acompressor=threshold={N(Math.Pow(10, v[0] / 20))}:ratio={N(v[1])}:attack={N(v[2])}:release={N(v[3])}:makeup=1:link=maximum",
            _ => throw new EditorError("InvalidEffect")
        };
    }
    private static void RenderSamples(AudioDocument document, FrameRange range, EffectSettings settings, string path, CancellationToken token)
    {
        int channels = document.Source.Channels;
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (settings.Kind == EffectKind.InsertSilence)
        {
            long frames = Math.Max(1, (long)Math.Round(settings.Values[0] * document.Source.Rate));
            byte[] zero = new byte[16384 * channels * 4];
            while (frames > 0) { token.ThrowIfCancellationRequested(); int count = (int)Math.Min(16384, frames); output.Write(zero, 0, count * channels * 4); frames -= count; }
            return;
        }
        if (settings.Kind == EffectKind.Reverse)
        {
            for (long end = range.End; end > range.Start;)
            {
                token.ThrowIfCancellationRequested(); long first = Math.Max(range.Start, end - 16384);
                var values = new List<float>(); document.Visit(new(first, end), (_, samples) => values.AddRange(samples), token);
                var block = values.ToArray(); int frames = block.Length / channels;
                for (int i = 0; i < frames / 2; i++)
                for (int channel = 0; channel < channels; channel++) (block[i * channels + channel], block[(frames - 1 - i) * channels + channel]) = (block[(frames - 1 - i) * channels + channel], block[i * channels + channel]);
                output.Write(MemoryMarshal.AsBytes(block.AsSpan())); end = first;
            }
            return;
        }
        double gain = settings.Kind == EffectKind.Gain ? Math.Pow(10, settings.Values[0] / 20) : 1;
        double[] means = new double[channels];
        if (settings.Kind == EffectKind.Normalize)
        {
            if (settings.RemoveDc)
            {
                document.Visit(range, (_, samples) => { for (int i = 0; i < samples.Length; i++) means[i % channels] += samples[i]; }, token);
                for (int channel = 0; channel < channels; channel++) means[channel] /= range.Length;
            }
            double peak = 0;
            document.Visit(range, (_, samples) => { for (int i = 0; i < samples.Length; i++) peak = Math.Max(peak, Math.Abs(samples[i] - means[i % channels])); }, token);
            gain = peak > 1e-12 ? Math.Pow(10, settings.Values[0] / 20) / peak : 1;
        }
        document.Visit(range, (offset, samples) =>
        {
            for (int i = 0; i < samples.Length; i++)
            {
                long frame = offset - range.Start + i / channels;
                double fade = range.Length <= 1 ? 0 : frame / (double)(range.Length - 1);
                samples[i] = settings.Kind switch
                {
                    EffectKind.Mute => 0,
                    EffectKind.Invert => -samples[i],
                    EffectKind.FadeIn => (float)(samples[i] * fade),
                    EffectKind.FadeOut => (float)(samples[i] * (1 - fade)),
                    _ => (float)((samples[i] - means[i % channels]) * gain)
                };
            }
            output.Write(MemoryMarshal.AsBytes(samples.AsSpan()));
        }, token);
    }
}
