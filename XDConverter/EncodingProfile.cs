namespace XDConverter;

public enum EncodingMode { Automatic = -1, Cbr, Abr, Vbr, ConstrainedVbr, Pcm, Lossless }

// These choices describe the bundled encoders, rather than offering settings they cannot encode.
public static class EncodingProfile
{
    public const int AutomaticQuality = -1000; // Vorbis Q-1 and MP3 V0 are real manual choices.
    private static readonly int[] commonRates = [8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000, 64000, 88200, 96000, 176400, 192000];
    public static int[] Rates(string format, EncodingMode? mode = null) => format switch
    {
        "MP3" => [8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000],
        "M4A" => [7350, 8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000, 64000, 88200, 96000],
        "Opus" => [8000, 12000, 16000, 24000, 48000],
        "OGG" when mode == EncodingMode.Abr => [8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000],
        _ => commonRates
    };
    public static EncodingMode[] Modes(string format) => format switch
    {
        "MP3" => [EncodingMode.Cbr, EncodingMode.Abr, EncodingMode.Vbr],
        "M4A" or "OGG" => [EncodingMode.Abr, EncodingMode.Vbr],
        "Opus" => [EncodingMode.Vbr, EncodingMode.ConstrainedVbr, EncodingMode.Cbr],
        "FLAC" => [EncodingMode.Lossless],
        _ => [EncodingMode.Pcm]
    };
    public static int[] Bitrates(string format, int rate, int channels = 0) => format switch
    {
        "MP3" when rate is > 0 and <= 12000 => [8, 16, 24, 32, 40, 48, 56, 64],
        "MP3" when rate is > 0 and <= 24000 => [8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],
        "MP3" when rate >= 32000 => [32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],
        "MP3" => [8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 192, 224, 256, 320],
        "Opus" => new[] { 6, 8, 12, 16, 24, 32, 40, 48, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 512 }.Where(b => channels == 2 || b <= 256).ToArray(),
        "OGG" => VorbisBitrates(rate, channels),
        _ => new[] { 32, 48, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 512 }.Where(b => rate == 0 || b * 1000 <= 6 * rate * (channels == 2 ? 2 : 1)).ToArray()
    };
    private static int[] VorbisBitrates(int rate, int channels)
    {
        int maxMono = rate switch { 8000 => 32, 11025 or 12000 => 48, 16000 => 96, 22050 or 24000 => 80, 32000 => 160, _ => 224 };
        int maxStereo = rate switch { 8000 => 80, 11025 or 12000 => 96, 16000 => 192, 22050 or 24000 => 160, 32000 => 320, _ => 448 };
        int min = rate >= 32000 && channels != 1 ? 48 : 32;
        return new[] { 32, 48, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448 }.Where(b => b >= min && b <= (channels == 2 ? maxStereo : maxMono)).ToArray();
    }
    public static int[] Qualities(string format) => format switch
    {
        "MP3" => Enumerable.Range(0, 10).ToArray(),
        "OGG" => Enumerable.Range(-1, 12).ToArray(),
        _ => Enumerable.Range(1, 5).ToArray()
    };
    public static int DefaultQuality(string format) => format == "OGG" ? 5 : 2;
    public static EncodingMode EffectiveMode(string format, EncodingMode? mode) => mode is not null and not EncodingMode.Automatic ? mode.Value : format switch
    {
        "WAV" => EncodingMode.Pcm,
        "FLAC" => EncodingMode.Lossless,
        _ => EncodingMode.Vbr
    };
    public static bool UsesQuality(string format, EncodingMode mode) => mode == EncodingMode.Vbr && format != "Opus";
    public static bool UsesBitrate(string format, EncodingMode mode) => mode is EncodingMode.Cbr or EncodingMode.Abr or EncodingMode.ConstrainedVbr || format == "Opus";
    public static void Validate(ConvertOptions options)
    {
        string format = options.Format.Id;
        var mode = EffectiveMode(format, options.Mode);
        if (!OutputFormat.All.Contains(options.Format) || !Modes(format).Contains(mode) ||
            options.SampleRate != 0 && !Rates(format, mode).Contains(options.SampleRate) || options.Channels is not (0 or 1 or 2)) throw new ConversionError("InvalidOptions");
        if (UsesBitrate(format, mode) && options.Quality != 0 && !Bitrates(format, options.SampleRate, options.Channels).Contains(options.Quality) ||
            UsesQuality(format, mode) && options.VbrQuality != AutomaticQuality && !Qualities(format).Contains(options.VbrQuality) ||
            format == "WAV" && options.Quality is not (0 or 8 or 16 or 24 or 32 or 33 or 64) ||
            format == "FLAC" && (options.Compression is < -1 or > 12 || options.BitDepth is not (0 or 16 or 24)) ||
            format == "Opus" && options.Application is not ("auto" or "audio" or "voip" or "lowdelay")) throw new ConversionError("InvalidOptions");
    }
    public static ConvertOptions Resolve(AudioInfo input, ConvertOptions options)
    {
        Validate(options);
        string format = options.Format.Id;
        var mode = EffectiveMode(format, options.Mode);
        int channels = options.Channels == 0 ? format is "WAV" or "FLAC" ? input.Channels : Math.Min(input.Channels, 2) : options.Channels;
        int rate = options.SampleRate;
        if (rate == 0)
        {
            if (format == "Opus") rate = 48000;
            else if (format is "WAV" or "FLAC") rate = input.SampleRate;
            else
            {
                var rates = Rates(format, mode).AsEnumerable();
                if (UsesBitrate(format, mode) && options.Quality != 0)
                    rates = rates.Where(r => Bitrates(format, r, options.Channels).Contains(options.Quality));
                rate = rates.MinBy(r => Math.Abs(r - input.SampleRate));
            }
        }
        int quality = options.Quality;
        if (quality == 0)
        {
            if (format == "WAV") quality = input.Codec.StartsWith("pcm_f64", StringComparison.Ordinal) ? 64 : input.Codec.StartsWith("pcm_f32", StringComparison.Ordinal) ? 32 : input.BitsPerSample switch { 8 => 8, > 24 => 33, > 16 => 24, _ => 16 };
            else if (UsesBitrate(format, mode)) quality = Bitrates(format, rate, channels).MinBy(b => Math.Abs(b - (format == "Opus" ? channels == 1 ? 64 : 128 : channels == 1 ? 128 : 192)));
        }
        return options with { Mode = mode, SampleRate = rate, Quality = quality,
            VbrQuality = options.VbrQuality == AutomaticQuality ? DefaultQuality(format) : options.VbrQuality,
            Compression = options.Compression == -1 ? 5 : options.Compression,
            Application = options.Application == "auto" ? "audio" : options.Application };
    }
    public static int EffectiveRate(AudioInfo input, ConvertOptions options) => Resolve(input, options).SampleRate;
}
