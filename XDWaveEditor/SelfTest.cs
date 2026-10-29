using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace XDWaveEditor;

internal static class SelfTest
{
    internal static async Task Run(AudioEngine engine, string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        string root = Path.Combine(Path.GetTempPath(), "XDWave-Test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var lines = new List<string>();
        try
        {
            const int rate = 8000, channels = 2;
            var samples = new float[23000 * channels];
            for (int frame = 0; frame < 23000; frame++)
            {
                float tone = (float)(.25 + .1 * Math.Sin(frame * 2 * Math.PI * 440 / rate));
                if (frame < 1000 || frame >= 17000) samples[frame * 2] = samples[frame * 2 + 1] = tone;
                if (frame is >= 9000 and < 13000) samples[frame * 2 + 1] = tone;
            }
            string raw = Path.Combine(root, "fixture.pcm"); File.WriteAllBytes(raw, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
            var source = new PcmSource(raw, rate, channels); source.BuildPeaks(CancellationToken.None);
            var doc = new AudioDocument(source, Path.Combine(root, "Original.wav")); doc.MarkSaved();
            doc.Select(1600, 8400); doc.DeleteSelection(); Check(doc.Frames == 16200 && doc.IsDirty, "manual deletion is sample-accurate");
            var expected = samples[..3200].Concat(samples[16800..]).ToArray();
            Check(Read(doc).SequenceEqual(expected), "editing preserves retained PCM samples exactly");
            doc.Undo(); Check(doc.Frames == 23000 && !doc.IsDirty && Read(doc).SequenceEqual(samples), "undo restores original bytes and saved state");
            doc.Redo(); Check(Read(doc).SequenceEqual(expected), "redo restores edited samples");
            doc.Select(12000, 2000); Check(doc.Selection == new FrameRange(2000, 12000), "reverse selection normalized");
            doc.TrimSelection(); Check(doc.Frames == 10000, "trim across edit boundaries"); doc.Undo();
            doc.Select(0, doc.Frames); doc.DeleteSelection(); Check(doc.Frames == 0, "delete entire document"); doc.Undo();
            doc.Undo();
            doc.Remove([new(300, 600), new(100, 500)]); Check(doc.Frames == 22500, "overlapping cuts merged without duplicate audio"); doc.Undo();
            doc.Select(100, 1100); doc.CopySelection(); doc.Select(2000, 2000); doc.Paste();
            Check(doc.Frames == 24000 && Read(doc).Skip(4000).Take(2000).SequenceEqual(samples.Skip(200).Take(2000)), "copy and paste retain exact stereo samples"); doc.Undo();
            doc.Select(100, 1100); doc.CutSelection(); Check(doc.Frames == 22000 && doc.ClipboardFrames == 1000, "cut retains clipboard"); doc.Undo();
            doc.Select(0, doc.Frames); doc.DeleteSelection(); doc.Paste(); Check(doc.Frames == 1000, "paste restores audio into empty document"); doc.Undo(); doc.Undo();
            lines.Add("PASS: exact stereo PCM selection/delete, cut/copy/paste, paste into empty audio, trim, overlapping cuts, undo/redo, saved-state tracking and empty-document recovery");

            string original = doc.Original;
            AudioEngine.WritePreview(doc, original, CancellationToken.None); source.Dispose();
            string originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original)));
            doc = await engine.Open(original, CancellationToken.None);
            doc.Select(1600, 8400); doc.DeleteSelection();
            await Reject(() => engine.Export(doc, original, true, CancellationToken.None), "OriginalProtected");
            foreach (string extension in AudioEngine.ExportExtensions)
            {
                string path = Path.Combine(root, "Edited-ä 日本語" + extension);
                await engine.Export(doc, path, false, CancellationToken.None);
                var decoded = await engine.Open(path, CancellationToken.None);
                Check(Math.Abs(decoded.Seconds - doc.Seconds) < .2 && decoded.Source.Channels == 2, extension + " reopens with expected duration and stereo");
                if (extension is ".wav" or ".flac")
                {
                    var a = Read(doc); var b = Read(decoded);
                    Check(a.Length == b.Length && a.Zip(b).All(v => Math.Abs(v.First - v.Second) < .000001), extension + " preserves retained samples at 24-bit precision");
                }
                decoded.Dispose();
                await Reject(() => engine.Export(doc, path, false, CancellationToken.None), "TargetExists");
            }
            Check(originalHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))), "source file remains byte-identical");
            string monoPath = Path.Combine(root, "mono.wav");
            await AudioEngine.Run(engine.Ffmpeg, ["-nostdin", "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=0.5", "-ac", "1", monoPath], CancellationToken.None);
            var mono = await engine.Open(monoPath, CancellationToken.None);
            Check(mono.Source.Channels == 1 && mono.Source.Rate == 44100, "common mono input");
            mono.Select(4410, 8820); mono.CutSelection(); mono.Paste();
            Check(mono.Frames == 22050, "mono cut and paste retain duration");
            await engine.Export(mono, Path.Combine(root, "mono-edited.wav"), false, CancellationToken.None); mono.Dispose();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                try { await engine.Export(doc, Path.Combine(root, "cancel.wav"), false, cancel.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { }
            }
            Check(!File.Exists(Path.Combine(root, "cancel.wav")) && !Directory.EnumerateFiles(root, ".xd-wave-*").Any(), "cancelled export leaves no output or partial files");
            var stopwatch = Stopwatch.StartNew();
            using (var cancel = new CancellationTokenSource(150))
            {
                try { await AudioEngine.Run(engine.Ffmpeg, ["-nostdin", "-v", "error", "-re", "-f", "lavfi", "-i", "sine=duration=10", "-f", "null", "-"], cancel.Token); throw new Exception("Running process was not cancelled"); }
                catch (OperationCanceledException) { }
            }
            Check(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "running FFmpeg process terminates promptly on cancellation");
            string broken = Path.Combine(root, "broken.wav"); File.WriteAllText(broken, "not audio");
            await Reject(() => engine.Open(broken, CancellationToken.None), "EngineFailed");
            lines.Add("PASS: real FFmpeg WAV/MP3/FLAC/OGG/M4A/Opus export and reimport, Unicode paths, lossless sample checks, original protection, existing-output protection, corrupt input and live process cancellation");

            var window = new MainWindow(engine); window.Show();
            Check(!((Button)window.FindName("ExportButton")).IsEnabled, "empty editor disables export");
            window.LoadDocument(doc);
            foreach (bool english in new[] { false, true })
            {
                Texts.Current.Set(english);
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(((Button)window.FindName("DeleteButton")).Content.ToString() == (english ? "Delete selection" : "Auswahl löschen"), "live editor language switching");
                Check(((Button)window.FindName("CopyButton")).Content.ToString() == (english ? "Copy" : "Kopieren"), "live copy tool language switching");
            }
            doc.Select(100, 1100);
            ((Button)window.FindName("DeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(doc.Frames == 15200, "real WPF delete button edits selected samples");
            Check(window.Wave.VisibleFrames == doc.Frames, "fitted waveform follows document length after editing");
            ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(doc.Frames == 16200, "real WPF undo button restores audio");
            doc.Select(100, 1100); ((Button)window.FindName("CopyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            doc.Select(2000, 2000); ((Button)window.FindName("PasteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(doc.Frames == 17200, "real WPF copy and paste buttons"); doc.Undo();
            window.Wave.Fit(); window.UpdateLayout();
            Check(Math.Abs(window.Wave.FrameAt(window.Wave.ActualWidth / 2) - doc.Frames / 2) <= 1, "waveform coordinates map to audio frames");
            window.Wave.Zoom(2); Check(window.Wave.VisibleFrames == doc.Frames / 2d, "waveform zoom");
            window.Wave.Scroll(doc.Frames); Check(window.Wave.ViewStart + window.Wave.VisibleFrames <= doc.Frames, "scroll clamps to document");
            string preview = Path.Combine(root, "playback.wav"); AudioEngine.WritePreview(doc, preview, CancellationToken.None);
            await CheckPlayback(preview, doc.Seconds);
            doc.MarkSaved(); window.Close();
            lines.Add("PASS: live bilingual WPF controls, editing buttons, waveform mapping/zoom/scroll, and muted Windows MediaPlayer open, duration, seek and playback clock");
            await AdvancedChecks.Run(engine, root, lines);
            lines.Add("NOTE: Playback checks are muted; audible output on the user's speakers remains a manual check. Original audio metadata is not copied into exports.");
            lines.Add("ALL WAVE EDITOR CHECKS PASSED");
        }
        catch (Exception error) { lines.Add("FAIL: " + error); throw; }
        finally
        {
            File.WriteAllLines(report, lines);
            // The unique test directory contains only files created by this run.
            foreach (string file in Directory.EnumerateFiles(root)) { try { File.Delete(file); } catch (IOException) { } }
            if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
        }
    }
    private static float[] Read(AudioDocument document)
    {
        var samples = new List<float>(); document.Visit(new(0, document.Frames), (_, block) => samples.AddRange(block), CancellationToken.None); return samples.ToArray();
    }
    private static async Task Reject(Func<Task> action, string key)
    {
        try { await action(); } catch (EditorError error) when (error.Key == key) { return; }
        throw new InvalidOperationException("Expected error: " + key);
    }
    private static async Task CheckPlayback(string path, double seconds)
    {
        var player = new MediaPlayer { Volume = 0 };
        var ready = new TaskCompletionSource();
        player.MediaOpened += (_, _) => ready.TrySetResult();
        player.MediaFailed += (_, error) => ready.TrySetException(error.ErrorException);
        try
        {
            player.Open(new Uri(path)); await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Check(player.NaturalDuration.HasTimeSpan && Math.Abs(player.NaturalDuration.TimeSpan.TotalSeconds - seconds) < .05, "playback duration");
            player.Position = TimeSpan.FromSeconds(.25); player.Play(); await Task.Delay(400);
            Check(player.Position.TotalSeconds > .35, "playback clock advances after seeking"); player.Pause();
        }
        finally { player.Close(); }
    }
    internal static async Task<string> CreateDemo(AudioEngine engine)
    {
        string path = AudioEngine.NewCachePath(".wav");
        await AudioEngine.Run(engine.Ffmpeg, ["-nostdin", "-v", "error", "-f", "lavfi", "-i", "aevalsrc=if(between(t\\,0.4\\,2.1)+between(t\\,3.3\\,5)+between(t\\,6.1\\,7.6)\\,0.45*sin(2*PI*220*t)*(0.6+0.4*sin(2*PI*4*t))\\,0)|if(between(t\\,0.4\\,2.1)+between(t\\,3.3\\,5)+between(t\\,6.1\\,7.6)\\,0.35*sin(2*PI*330*t)*(0.6+0.4*sin(2*PI*3*t))\\,0):s=44100:d=8", "-c:a", "pcm_s16le", path], CancellationToken.None);
        return path;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
