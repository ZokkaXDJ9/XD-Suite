using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;

namespace XDWaveEditor;

internal static class AdvancedChecks
{
    private const int Rate = 48000;
    internal static async Task Run(AudioEngine engine, string root, List<string> lines)
    {
        var samples = Tone(3);
        FrameRange all = new(0, samples.Length / 2), range = new(Rate / 2, Rate * 5 / 2);
        using (var doc = Make(samples))
        {
            await engine.ApplyEffect(doc, range, new(EffectKind.Gain, [-6]), CancellationToken.None);
            var result = Read(doc); double gain = Math.Pow(10, -.3);
            Check(result[..Rate].SequenceEqual(samples[..Rate]) && result[(Rate * 5)..].SequenceEqual(samples[(Rate * 5)..]), "gain leaves audio outside selection untouched");
            Check(Math.Abs(result[Rate + 2] - samples[Rate + 2] * gain) < 1e-7 && doc.Selection == range, "gain on selected samples");
            string material = doc.Pieces.Single(p => p.Audio != null).Audio!.Path;
            doc.Undo(); Check(Read(doc).SequenceEqual(samples) && !doc.IsDirty, "effect undo restores original audio and dirty state");
            doc.Redo(); Check(Read(doc).SequenceEqual(result), "effect redo");
            doc.Select(range.Start, range.End); doc.CopySelection(); doc.Select(doc.Frames, doc.Frames); doc.Paste();
            Check(Read(doc).TakeLast((int)range.Length * 2).SequenceEqual(result.Skip((int)range.Start * 2).Take((int)range.Length * 2)), "clipboard retains rendered effect material");
            doc.Undo(); doc.Undo(); doc.Dispose(); Check(!File.Exists(material), "effect cache is owned and removed even when retained only by redo history");
        }
        foreach (var kind in new[] { EffectKind.FadeIn, EffectKind.FadeOut, EffectKind.Mute, EffectKind.Invert })
        {
            float[] constant = Enumerable.Range(0, 2000).Select(i => i % 2 == 0 ? .25f : -.5f).ToArray();
            using var doc = Make(constant);
            await engine.ApplyEffect(doc, new(0, doc.Frames), Effects.Defaults(kind, Rate), CancellationToken.None);
            var result = Read(doc);
            if (kind == EffectKind.FadeIn) Check(result[0] == 0 && result[1] == 0 && result[^2] == .25f && result[^1] == -.5f, "fade-in endpoints and stereo alignment");
            if (kind == EffectKind.FadeOut) Check(result[0] == .25f && result[1] == -.5f && result[^2] == 0 && result[^1] == 0, "fade-out endpoints");
            if (kind == EffectKind.Mute) Check(result.All(v => v == 0) && doc.Frames == 1000, "mute preserves duration");
            if (kind == EffectKind.Invert) Check(result.Zip(constant).All(p => p.First == -p.Second), "polarity inversion");
            doc.Undo(); Check(Read(doc).SequenceEqual(constant), kind + " undo");
        }
        using (var doc = Make(Enumerable.Range(0, 40).Select(i => (i % 2 == 0 ? 1 : -1) * (i / 2 + 1) / 100f).ToArray()))
        {
            doc.Select(2, 4); doc.DeleteSelection(); var before = Read(doc);
            await engine.ApplyEffect(doc, new(0, doc.Frames), Effects.Defaults(EffectKind.Reverse, Rate), CancellationToken.None);
            var reversed = Enumerable.Range(0, before.Length / 2).Reverse().SelectMany(i => before.Skip(i * 2).Take(2));
            Check(Read(doc).SequenceEqual(reversed), "reverse across prior cuts keeps channels paired"); doc.Undo();
            await engine.ApplyEffect(doc, new(2, 5), new(EffectKind.Repeat, [3]), CancellationToken.None);
            Check(doc.Frames == 24 && Read(doc).Skip(4).Take(18).SequenceEqual(Enumerable.Range(0, 3).SelectMany(_ => before.Skip(4).Take(6))), "repeat exact selection and total copies");
        }
        var offset = Tone(2); for (int i = 0; i < offset.Length; i++) offset[i] += i % 2 == 0 ? .2f : -.1f;
        using (var doc = Make(offset))
        {
            await engine.ApplyEffect(doc, new(0, doc.Frames), Effects.Defaults(EffectKind.Normalize, Rate), CancellationToken.None);
            var result = Read(doc);
            Check(Math.Abs(result.Max(v => Math.Abs(v)) - Math.Pow(10, -.05)) < .000002, "normalize reaches -1 dB peak");
            Check(Math.Abs(result.Where((_, i) => i % 2 == 0).Average()) < .000001 && Math.Abs(result.Where((_, i) => i % 2 == 1).Average()) < .000001, "normalize removes per-channel DC offset");
            Check(Math.Abs(Rms(result, 0, 0, 1) / Rms(result, 1, 0, 1) - 2) < .01, "normalize preserves stereo balance");
        }
        using (var doc = Make(samples))
        {
            long before = doc.Frames;
            await engine.ApplyEffect(doc, new(100, 100), new(EffectKind.InsertSilence, [.1]), CancellationToken.None);
            Check(doc.Frames == before + 4800 && Read(doc).Skip(200).Take(9600).All(v => v == 0), "insert silence at cursor shifts audio without deletion"); doc.Undo();
            doc.Select(0, doc.Frames); doc.DeleteSelection();
            await engine.ApplyEffect(doc, new(0, 0), new(EffectKind.InsertSilence, [.1]), CancellationToken.None);
            Check(doc.Frames == 4800 && Read(doc).All(v => v == 0), "insert silence into empty edited audio");
        }
        using (var doc = Make(Enumerable.Repeat(.8f, 2000).ToArray()))
        {
            Guid revision = doc.Revision;
            await Reject(() => engine.ApplyEffect(doc, new(0, doc.Frames), new(EffectKind.Gain, [12]), CancellationToken.None), "WouldClip");
            Check(doc.Revision == revision && Read(doc).All(v => v == .8f), "rejected clipping does not change document");
            await Reject(() => engine.ApplyEffect(doc, new(0, doc.Frames), new(EffectKind.Tempo, [double.NaN]), CancellationToken.None), "InvalidEffect");
            await engine.ApplyEffect(doc, new(0, doc.Frames), new(EffectKind.Gain, [12], AllowClipping: true), CancellationToken.None);
            Check(Read(doc).Max() > 1, "clipping requires explicit opt-in");
        }
        using (var doc = Make(Tone(120)))
        {
            Guid revision = doc.Revision;
            using var cancel = new CancellationTokenSource(5);
            try { await engine.ApplyEffect(doc, new(0, doc.Frames), new(EffectKind.Gain, [-3]), cancel.Token); throw new Exception("Effect cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(doc.Revision == revision && !doc.IsDirty, "cancellation does not commit a partial effect");
        }
        lines.Add("PASS: selected-range gain, exact fades/mute/reverse/invert/repeat/silence insertion, DC removal and linked peak normalization, rendered clipboard, undo/redo/cache lifetime, clipping opt-in and transactional effect cancellation");

        foreach (var kind in new[] { EffectKind.Tempo, EffectKind.Pitch, EffectKind.Equalizer, EffectKind.HighPass, EffectKind.LowPass, EffectKind.Denoise, EffectKind.Compressor })
        {
            float[] signal = samples;
            if (kind is EffectKind.Equalizer or EffectKind.HighPass or EffectKind.LowPass) signal = MixedTone(3);
            if (kind == EffectKind.Denoise) signal = Noise(3);
            if (kind == EffectKind.Compressor) signal = Tone(3, dynamic: true);
            using var doc = Make(signal);
            var settings = kind switch
            {
                EffectKind.Tempo => new(kind, [150d]), EffectKind.Pitch => new(kind, [12d]),
                EffectKind.Equalizer => new(kind, [6d, 0, 0]), EffectKind.HighPass or EffectKind.LowPass => new EffectSettings(kind, [1000]),
                EffectKind.Denoise => new(kind, [12d, -45]), EffectKind.Compressor => new(kind, [-18d, 4, 10, 120]),
                _ => Effects.Defaults(kind, Rate)
            };
            await engine.ApplyEffect(doc, new(0, doc.Frames), settings, CancellationToken.None);
            var result = Read(doc);
            Check(doc.Frames == (kind == EffectKind.Tempo ? 96000 : 144000) && result.All(float.IsFinite), kind + " output duration and valid samples");
            if (kind == EffectKind.Tempo) Check(Math.Abs(Frequency(result, .3, 1.2) - 440) < 4, "tempo preserves pitch");
            if (kind == EffectKind.Pitch) Check(Math.Abs(Frequency(result, .3, 1.2) - 880) < 4, "octave shift preserves duration and doubles frequency");
            if (kind == EffectKind.Equalizer) Check(Magnitude(result, 80) > Magnitude(signal, 80) * 1.7, "bass EQ boosts low band");
            if (kind == EffectKind.HighPass) Check(Magnitude(result, 80) < Magnitude(signal, 80) * .05 && Magnitude(result, 6000) > Magnitude(signal, 6000) * .8, "high-pass rejects lows and retains highs");
            if (kind == EffectKind.LowPass) Check(Magnitude(result, 6000) < Magnitude(signal, 6000) * .1 && Magnitude(result, 80) > Magnitude(signal, 80) * .8, "low-pass rejects highs and retains lows");
            if (kind == EffectKind.Denoise) Check(Rms(result, 0, .3, .7) < Rms(signal, 0, .3, .7) * .9 && Magnitude(result, 440, 1.2, 1.8) > .1, "noise reduction reduces background and retains tone");
            if (kind == EffectKind.Compressor) Check(Rms(result, 0, 2, 2.5) < Rms(signal, 0, 2, 2.5) * .7 && Rms(result, 0, .5, 1) > Rms(signal, 0, .5, 1) * .9, "compressor attenuates loud section and retains quiet section");
            doc.Undo(); Check(Read(doc).SequenceEqual(signal), kind + " undo restores exact source");
        }
        string monoRaw = AudioEngine.NewCachePath(".pcm");
        float[] monoSamples = Enumerable.Range(0, 16000).Select(i => (float)(.2 * Math.Sin(i * 2 * Math.PI * 440 / 8000))).ToArray();
        File.WriteAllBytes(monoRaw, MemoryMarshal.AsBytes(monoSamples.AsSpan()).ToArray());
        var monoSource = new PcmSource(monoRaw, 8000, 1); monoSource.BuildPeaks(CancellationToken.None);
        using (var mono = new AudioDocument(monoSource, monoRaw + ".original.wav"))
        {
            mono.MarkSaved();
            await engine.ApplyEffect(mono, new(0, mono.Frames), Effects.Defaults(EffectKind.Normalize, 8000), CancellationToken.None);
            await engine.ApplyEffect(mono, new(0, mono.Frames), new(EffectKind.Pitch, [3]), CancellationToken.None);
            Check(mono.Frames == 16000 && Read(mono).All(float.IsFinite), "mono effects at low sample rate");
        }
        lines.Add("PASS: real FFmpeg tempo/pitch duration and frequency checks, EQ band gain, high/low-pass frequency rejection, noise RMS reduction with tone retained, compressor level response, mono processing and exact effect undo");

        string imported = Path.Combine(root, "inserted-mono.wav");
        await AudioEngine.Run(engine.Ffmpeg, ["-nostdin", "-v", "error", "-f", "lavfi", "-i", "sine=sample_rate=44100:duration=0.25", "-ac", "1", imported], CancellationToken.None);
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(imported));
        using (var doc = Make(samples))
        {
            doc.Select(100, 100); await engine.InsertAudio(doc, imported, CancellationToken.None);
            Check(doc.Frames == all.Length + 12000 && doc.Selection == new FrameRange(100, 12100), "insert resamples mono input to document stereo rate");
            Check(Read(doc).Skip(200).Take(24000).Where((_, i) => i % 2 == 0).Any(v => v != 0), "inserted audio contains signal");
            await Reject(() => engine.Export(doc, imported, true, CancellationToken.None), "OriginalProtected");
            string export = Path.Combine(root, "advanced-export.wav"); await engine.Export(doc, export, false, CancellationToken.None);
            using var reopened = await engine.Open(export, CancellationToken.None); Check(reopened.Frames == doc.Frames, "composite multi-source audio exports and reopens");
            doc.Undo(); Check(Read(doc).SequenceEqual(samples) && originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(imported))), "insert undo preserves original audio files");
        }
        lines.Add("PASS: audio insertion with sample-rate/channel adaptation, protection of every imported original, composite export/reimport and insertion undo");
        await CheckWindows(engine, samples);
        lines.Add("PASS: German/English effect names and dialog defaults, real WPF effect Apply and cancellation, precise time selection with validation, effect menu/button state and actual muted loop playback");
    }
    private static async Task CheckWindows(AudioEngine engine, float[] samples)
    {
        foreach (bool english in new[] { false, true })
        {
            Texts.Current.Set(english);
            using var doc = Make(samples);
            foreach (var definition in Effects.ForRate(Rate))
            {
                Check(definition.Name != "Fx" + definition.Kind && definition.Help != "Fx" + definition.Kind + "Help", "effect translation " + definition.Kind);
                Effects.Validate(Effects.Defaults(definition.Kind, Rate), Rate);
            }
            var dialog = new EffectDialog(engine, doc, EffectKind.Gain); dialog.Show();
            dialog.Inputs[0].Entry.Text = "garbage";
            Check(!await dialog.Apply() && !doc.IsDirty, "invalid numeric input does not edit audio");
            dialog.Inputs[0].Value = -6;
            dialog.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await dialog.Pending!;
            Check(!dialog.IsVisible && doc.IsDirty, "real effect dialog apply button commits and closes");
            var precise = new SelectionDialog(doc); precise.Show();
            precise.StartEntry.Text = "0,25"; precise.EndEntry.Text = "00:00:01.500";
            Check(precise.TryRange(out var range) && range == new FrameRange(12000, 72000), "precise selection accepts decimal seconds and full timestamps");
            precise.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(doc.Selection == range && !precise.IsVisible, "precise selection button");
            var invalid = new SelectionDialog(doc); invalid.StartEntry.Text = "NaN";
            Check(!invalid.TryRange(out _), "non-finite time rejected"); invalid.Close();
            Guid revision = doc.Revision;
            var cancelled = new EffectDialog(engine, doc, EffectKind.Normalize); cancelled.Show();
            cancelled.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); cancelled.Close();
            Check(!await cancelled.Pending! && !cancelled.IsVisible && doc.Revision == revision, "closing a processing effect dialog cancels without modifying audio");
            doc.MarkSaved();
        }
        Texts.Current.Set(false);
        var window = new MainWindow(engine); window.Show();
        Check(!((Button)window.FindName("ToolsButton")).IsEnabled, "effects disabled without audio");
        var document = Make(samples); window.LoadDocument(document);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(((Button)window.FindName("ToolsButton")).IsEnabled, "effects enabled with document");
        document.MarkSaved(); window.Close();
        var player = new MediaPlayer { Volume = 0 };
        var looping = new MainWindow(engine, player); looping.Show();
        var loopDoc = Make(Tone(1)); looping.LoadDocument(loopDoc); loopDoc.Select(12000, 24000);
        ((CheckBox)looping.FindName("LoopPlayback")).IsChecked = true;
        await looping.PlayAudio();
        int wraps = 0; long previous = 12000;
        for (int i = 0; i < 80 && wraps < 2; i++)
        {
            await Task.Delay(40);
            long current = loopDoc.Cursor;
            if (previous - current > 2400) wraps++;
            previous = current;
        }
        Check(wraps >= 2, "actual muted selection playback loops repeatedly");
        looping.Close();
    }
    private static AudioDocument Make(float[] samples)
    {
        string path = AudioEngine.NewCachePath(".pcm"); File.WriteAllBytes(path, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
        var source = new PcmSource(path, Rate, 2); source.BuildPeaks(CancellationToken.None);
        var document = new AudioDocument(source, path + ".original.wav"); document.MarkSaved(); return document;
    }
    private static float[] Tone(int seconds, bool dynamic = false)
    {
        var result = new float[Rate * seconds * 2];
        for (int i = 0; i < result.Length / 2; i++)
        {
            double level = dynamic ? i < Rate * 1.5 ? .05 : .8 : .3;
            result[i * 2] = (float)(level * Math.Sin(i * 2 * Math.PI * 440 / Rate));
            result[i * 2 + 1] = (float)(level / 2 * Math.Sin(i * 2 * Math.PI * 660 / Rate));
        }
        return result;
    }
    private static float[] MixedTone(int seconds)
    {
        var result = new float[Rate * seconds * 2];
        for (int i = 0; i < result.Length / 2; i++) result[i * 2] = result[i * 2 + 1] = (float)(.1 * (Math.Sin(i * 2 * Math.PI * 80 / Rate) + Math.Sin(i * 2 * Math.PI * 1000 / Rate) + Math.Sin(i * 2 * Math.PI * 6000 / Rate)));
        return result;
    }
    private static float[] Noise(int seconds)
    {
        var random = new Random(194); var result = new float[Rate * seconds * 2];
        for (int i = 0; i < result.Length / 2; i++)
        {
            float noise = (float)((random.NextDouble() - .5) * .01);
            result[i * 2] = result[i * 2 + 1] = noise + (i >= Rate && i < Rate * 2 ? (float)(.2 * Math.Sin(i * 2 * Math.PI * 440 / Rate)) : 0);
        }
        return result;
    }
    private static float[] Read(AudioDocument document)
    {
        var result = new List<float>(); document.Visit(new(0, document.Frames), (_, samples) => result.AddRange(samples), CancellationToken.None); return result.ToArray();
    }
    private static double Frequency(float[] samples, double start, double end)
    {
        int crossings = 0;
        for (int i = (int)(start * Rate) + 1; i < (int)(end * Rate); i++) if (samples[(i - 1) * 2] < 0 && samples[i * 2] >= 0) crossings++;
        return crossings / (end - start);
    }
    private static double Rms(float[] samples, int channel, double start, double end)
    {
        double sum = 0; int first = (int)(start * Rate), last = (int)(end * Rate);
        for (int i = first; i < last; i++) sum += samples[i * 2 + channel] * samples[i * 2 + channel];
        return Math.Sqrt(sum / (last - first));
    }
    private static double Magnitude(float[] samples, double hz, double start = .5, double end = 2.5)
    {
        double real = 0, imaginary = 0; int first = (int)(start * Rate), last = (int)(end * Rate);
        for (int i = first; i < last; i++) { real += samples[i * 2] * Math.Cos(i * 2 * Math.PI * hz / Rate); imaginary += samples[i * 2] * Math.Sin(i * 2 * Math.PI * hz / Rate); }
        return 2 * Math.Sqrt(real * real + imaginary * imaginary) / (last - first);
    }
    private static async Task Reject(Func<Task> action, string key)
    {
        try { await action(); } catch (EditorError error) when (error.Key == key) { return; }
        throw new InvalidOperationException("Expected " + key);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
