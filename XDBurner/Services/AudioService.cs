using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace XDBurner.Services;

public static class AudioService
{
    public static ProjectItem Inspect(string path)
    {
        using var reader = new AudioFileReader(path);
        if (reader.TotalTime <= TimeSpan.Zero) throw new InvalidOperationException($"„{Path.GetFileName(path)}“ enthält keinen Audiotitel.");
        if (reader.WaveFormat.Channels > 2) throw new InvalidOperationException("Bitte eine Mono- oder Stereo-Datei verwenden. Mehrkanalton wird nicht automatisch gemischt.");
        return new(Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), false, new FileInfo(path).Length, reader.TotalTime);
    }

    // IMAPI expects headerless 44.1 kHz / 16 bit / stereo PCM, padded to full 2352-byte CD sectors.
    public static long ConvertToPcm(string source, string destination, CancellationToken token)
    {
        using var reader = new AudioFileReader(source);
        ISampleProvider samples = reader;
        if (reader.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
        else if (reader.WaveFormat.Channels != 2) throw new InvalidOperationException("Nur Mono- und Stereo-Audio werden unterstützt.");
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
        long length = Math.Max(300 * 2352L, (output.Length + 2351) / 2352 * 2352);
        output.SetLength(length);
        return length / 2352;
    }
}
