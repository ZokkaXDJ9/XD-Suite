using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NVorbis;

namespace XDBurner.Services;

public static class AudioService
{
    public static ProjectItem Inspect(string path)
    {
        using var reader = Open(path);
        if (reader.Duration <= TimeSpan.Zero) throw new InvalidOperationException($"„{Path.GetFileName(path)}“ enthält keinen Audiotitel.");
        if (reader.Samples.WaveFormat.Channels > 2) throw new InvalidOperationException("Bitte eine Mono- oder Stereo-Datei verwenden. Mehrkanalton wird nicht automatisch gemischt.");
        return new(Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), false, new FileInfo(path).Length, reader.Duration);
    }

    // IMAPI expects headerless 44.1 kHz / 16 bit / stereo PCM, padded to full 2352-byte CD sectors.
    public static long ConvertToPcm(string source, string destination, CancellationToken token, int gapSeconds = 2, bool discAtOnce = false)
    {
        if (gapSeconds < (discAtOnce ? 0 : 2) || gapSeconds > 30) throw new ArgumentOutOfRangeException(nameof(gapSeconds));
        using var reader = Open(source);
        ISampleProvider samples = reader.Samples;
        if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
        else if (samples.WaveFormat.Channels != 2) throw new InvalidOperationException("Nur Mono- und Stereo-Audio werden unterstützt.");
        if (samples.WaveFormat.SampleRate != 44100) samples = new WdlResamplingSampleProvider(samples, 44100);
        var pcm = new SampleToWaveProvider16(samples);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[2352 * 32];
        int read;
        while ((read = pcm.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
        }
        if (output.Length == 0) throw new InvalidOperationException("Die Audiodatei konnte nicht gelesen werden.");
        // Track-at-once supplies a fixed 2 s gap. Additional selected silence is appended to PCM.
        long length = Math.Max(300 * 2352L, (output.Length + 2351) / 2352 * 2352) + (discAtOnce ? gapSeconds : gapSeconds - 2) * 75L * 2352;
        output.SetLength(length);
        return length / 2352;
    }

    private sealed class Input(ISampleProvider samples, TimeSpan duration, IDisposable owner) : IDisposable
    {
        public ISampleProvider Samples { get; } = samples;
        public TimeSpan Duration { get; } = duration;
        public void Dispose() => owner.Dispose();
    }
    private sealed class VorbisSamples(VorbisReader reader) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
        public int Read(float[] buffer, int offset, int count) => reader.ReadSamples(buffer, offset, count);
    }
    private static Input Open(string path)
    {
        if (Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            var vorbis = new VorbisReader(path);
            return new(new VorbisSamples(vorbis), vorbis.TotalTime, vorbis);
        }
        var wave = new AudioFileReader(path);
        return new(wave, wave.TotalTime, wave);
    }
}
