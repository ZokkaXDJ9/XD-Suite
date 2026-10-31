using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace XDConverter;

internal static class SelfTest
{
    public static async Task Run(AudioEngine engine, string report)
    {
        report = Path.GetFullPath(report); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        string root = Path.Combine(Path.GetDirectoryName(report)!, "conversion-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var checks = new List<string> { "XD Converter integration tests", DateTimeOffset.Now.ToString("O") };
        try
        {
            checks.Add("Engine: " + await engine.Check(CancellationToken.None));
            string source = Path.Combine(root, "Prüfton & $(unverändert) 空.wav");
            WriteTone(source);
            string tagged = Path.Combine(root, "Titel mit Tags.wav");
            await ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-nostdin", "-n", "-i", source, "-map_metadata", "-1", "-metadata", "title=Prüfton – café", "-metadata", "artist=XD Test", "-c:a", "copy", tagged], CancellationToken.None);
            byte[] before = SHA256.HashData(File.ReadAllBytes(tagged));
            var input = await engine.Inspect(tagged, CancellationToken.None);
            Check(input.Channels == 2 && input.SampleRate == 44100 && Math.Abs(input.Duration.TotalSeconds - 2) < .01, "WAV inspection");
            var outputs = new Dictionary<string, string>();
            foreach (var format in OutputFormat.All)
            {
                string output = await engine.Convert(input, new(format, Path.Combine(root, "outputs"), format.Id == "WAV" ? 16 : 192), _ => { }, CancellationToken.None);
                var converted = await engine.Inspect(output, CancellationToken.None);
                Check(converted.Channels == 2 && Math.Abs(converted.Duration.TotalSeconds - 2) < .2, "Duration/channels for " + format.Id);
                Check(converted.SampleRate == (format.Id == "Opus" ? 48000 : 44100), "Sample rate for " + format.Id);
                var metadata = await ProcessRunner.Run(engine.Ffprobe, ["-v", "error", "-show_entries", "format_tags:stream_tags", "-of", "json", output], CancellationToken.None);
                using var metadataJson = JsonDocument.Parse(metadata.Output);
                Check(ContainsText(metadataJson.RootElement, "Prüfton – café"), "Unicode title metadata for " + format.Id);
                outputs.Add(format.Id, output);
            }
            checks.Add("PASS: real WAV to MP3/WAV/FLAC/OGG/M4A/Opus conversion, decoded duration, channels, rate and Unicode text tags.");
            foreach (var pair in outputs)
            {
                var encoded = await engine.Inspect(pair.Value, CancellationToken.None);
                string restored = await engine.Convert(encoded, new(OutputFormat.All[1], Path.Combine(root, "decoded-" + pair.Key), 16), _ => { }, CancellationToken.None);
                var restoredInfo = await engine.Inspect(restored, CancellationToken.None);
                Check(Math.Abs(restoredInfo.Duration.TotalSeconds - 2) < .2 && restoredInfo.Channels == 2, "Full decode of " + pair.Key + " input");
            }
            checks.Add("PASS: MP3/WAV/FLAC/OGG/M4A/Opus inputs fully decoded and converted back to PCM WAV.");
            string decoded = Path.Combine(root, "flac-decoded.pcm"), original = Path.Combine(root, "original.pcm");
            await ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-i", outputs["FLAC"], "-f", "s16le", "-c:a", "pcm_s16le", decoded], CancellationToken.None);
            await ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-i", tagged, "-f", "s16le", "-c:a", "pcm_s16le", original], CancellationToken.None);
            Check(File.ReadAllBytes(decoded).SequenceEqual(File.ReadAllBytes(original)), "FLAC round trip retains PCM samples");
            string changed = await engine.Convert(input, new(OutputFormat.All[1], Path.Combine(root, "resampled"), 24, 48000, 1), _ => { }, CancellationToken.None);
            var resampled = await engine.Inspect(changed, CancellationToken.None);
            Check(resampled.SampleRate == 48000 && resampled.Channels == 1 && resampled.Codec == "pcm_s24le", "24-bit mono resampling");
            checks.Add("PASS: lossless FLAC PCM round trip; 24-bit WAV, 48 kHz resampling and mono output.");
            await EncodingChecks.Run(engine, input, root, checks);
            await AutomaticChecks.Run(engine, input, root, checks);

            string sameFolder = await engine.Convert(input, new(OutputFormat.All[1], root, 16), _ => { }, CancellationToken.None);
            Check(sameFolder != tagged && before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(tagged))), "Same-directory conversion protects original");
            byte[] existing = File.ReadAllBytes(outputs["MP3"]);
            string duplicate = await engine.Convert(input, new(OutputFormat.All[0], Path.Combine(root, "outputs")), _ => { }, CancellationToken.None);
            Check(duplicate != outputs["MP3"] && existing.SequenceEqual(File.ReadAllBytes(outputs["MP3"])), "Existing output receives no writes");
            string stripped = await engine.Convert(input, new(OutputFormat.All[2], Path.Combine(root, "no-tags"), Tags: false), _ => { }, CancellationToken.None);
            var tags = await ProcessRunner.Run(engine.Ffprobe, ["-v", "error", "-show_entries", "format_tags=title,artist", "-of", "json", stripped], CancellationToken.None);
            Check(!tags.Output.Contains("Prüfton", StringComparison.Ordinal), "Metadata removal");
            checks.Add("PASS: existing destination preserved, unique output names, source byte hashes unchanged and optional tag removal.");

            string corrupt = Path.Combine(root, "broken.mp3"); File.WriteAllText(corrupt, "Not audio.");
            await Reject(() => engine.Inspect(corrupt, CancellationToken.None), "Corrupt input rejected");
            string video = Path.Combine(root, "video-disguised.m4a");
            await ProcessRunner.Run(engine.Ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "color=size=16x16:rate=1", "-i", tagged, "-t", "0.25", "-c:v", "mpeg4", "-c:a", "aac", "-f", "mp4", video], CancellationToken.None);
            await Reject(() => engine.Inspect(video, CancellationToken.None), "Actual video in an audio-named container rejected");
            await Reject(() => engine.Convert(input, new(OutputFormat.All[5], Path.Combine(root, "bad-options"), SampleRate: 44100), _ => { }, CancellationToken.None), "Unsupported Opus sample rate rejected");
            using var canceled = new CancellationTokenSource(); bool receivedProgress = false;
            await Reject(() => engine.Convert(input, new(OutputFormat.All[0], Path.Combine(root, "cancel")), p => { if (p > 0 && p < 1) { receivedProgress = true; canceled.Cancel(); } }, canceled.Token), "Running conversion canceled");
            Check(receivedProgress && !Directory.EnumerateFiles(Path.Combine(root, "cancel")).Any(), "Canceled conversion removes partial output");
            Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(tagged))), "Cancellation retains source bytes");
            checks.Add("PASS: corrupt audio, video disguised as audio and invalid settings rejected; real conversion progress cancellation leaves no partial files or modified sources.");

            Texts.Current.Set(false);
            var window = new MainWindow(engine, new ConverterSettings(), true); window.Show(); window.SetPreview();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            IconChecks.VerifyWindowAndExe(window, root);
            checks.Add("PASS: embedded EXE icon, all nine ICO resolutions and native window/taskbar icon match the converter artwork.");
            var grid = (DataGrid)window.FindName("FilesGrid"); string filename = ((AudioRow)grid.Items[0]).Name;
            Texts.Current.Set(true); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(window.Title == "XD Converter" + (BuildInfo.IsBeta ? " Beta" : "") && ((Button)window.FindName("StartButton")).Content.ToString() == "Convert", "English UI");
            Check(((AudioRow)grid.Items[0]).Name == filename, "Filename not translated");
            Texts.Current.Set(false); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(window.Title == "XD Konverter" + (BuildInfo.IsBeta ? " Beta" : "") && ((Button)window.FindName("StartButton")).Content.ToString() == "Konvertieren", "German UI");
            if (BuildInfo.IsBeta) Check(Texts.Current["Preview"].Contains("-Beta") && BuildInfo.DataFolder == "XDConverterBeta" && BuildInfo.MutexName == @"Local\XDConverter.Beta.App" && typeof(App).Assembly.GetName().Name == "XDConverter.Beta", "Beta version label, settings, mutex and executable identity are separate");
            var formats = (ComboBox)window.FindName("FormatCombo");
            var modes = (ComboBox)window.FindName("ModeCombo");
            var qualityControl = (ComboBox)window.FindName("QualityCombo");
            var advanced = (Expander)window.FindName("AdvancedExpander");
            Check(!advanced.IsExpanded, "Optional settings start collapsed");
            foreach (var format in OutputFormat.All)
            {
                formats.SelectedItem = format;
                Check(qualityControl.SelectedItem.ToString() == "Automatisch" && modes.SelectedItem.ToString() == "Automatisch" && ((ComboBox)window.FindName("RateCombo")).SelectedItem.ToString() == "Automatisch" && ((ComboBox)window.FindName("ChannelCombo")).SelectedItem.ToString() == "Automatisch", "Automatic defaults for " + format.Id);
                Check(((ComboBox)window.FindName("DepthCombo")).SelectedItem.ToString() == "Automatisch" && ((ComboBox)window.FindName("ApplicationCombo")).SelectedItem.ToString() == "Automatisch", "Automatic depth/application defaults");
            }
            advanced.IsExpanded = true;
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "MP3");
            foreach (var mode in EncodingProfile.Modes("MP3"))
            {
                Select(modes, Texts.Current[mode.ToString()]);
                Check(qualityControl.SelectedItem.ToString() == "Automatisch", "Automatic bitrate/quality offered for " + mode);
            }
            Select(qualityControl, "V0");
            Check(qualityControl.SelectedItem.ToString() == "V0", "MP3 VBR quality selection");
            Texts.Current.Set(true);
            Check(qualityControl.SelectedItem.ToString() == "V0", "VBR quality survives translation");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "FLAC"); Select(qualityControl, "12 · maximum compression");
            Check(qualityControl.IsEnabled && qualityControl.Items[0].ToString() == "Automatic", "Automatic and manual FLAC compression available");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "MP3");
            Check(modes.SelectedItem.ToString() == Texts.Current["Vbr"] && qualityControl.SelectedItem.ToString() == "V0", "MP3 options retained across format changes");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "FLAC");
            Check(qualityControl.SelectedItem.ToString() == "12 · maximum compression", "FLAC options retained across format changes");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "Opus");
            Check(!((ComboBox)window.FindName("RateCombo")).Items.Cast<object>().Any(i => i.ToString()!.Contains("44,100", StringComparison.Ordinal)), "Opus excludes unsupported 44.1 kHz");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "MP3");
            Check(((Button)window.FindName("AutomaticButton")).IsVisible, "Manual overrides offer reset to automatic");
            ((Button)window.FindName("AutomaticButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(modes.SelectedItem.ToString() == "Automatic" && qualityControl.SelectedItem.ToString() == "Automatic", "Reset all audio preferences to automatic");
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "OGG"); Select(qualityControl, "Q -1");
            Texts.Current.Set(false);
            Check(qualityControl.SelectedItem.ToString() == "Q -1", "Vorbis Q-1 is a manual value distinct from Automatic");
            ((Button)window.FindName("AutomaticButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            formats.SelectedItem = OutputFormat.All.Single(f => f.Id == "MP3"); advanced.IsExpanded = false;
            checks.Add("PASS: actual WPF mode/quality selection, V0 retained through language and format changes, independent FLAC compression and codec-specific rate choices.");
            checks.Add("PASS: all formats start fully automatic; every MP3 mode offers automatic quality/bitrate, optional settings collapse, V0/Q-1 stay distinct from Automatic, and reset restores automatic operation.");
            ((Button)window.FindName("ClearButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((TextBox)window.FindName("OutputBox")).Text = Path.Combine(root, "gui-output");
            var drop = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [new DataObject(DataFormats.FileDrop, new[] { tagged }), DragDropKeyStates.None, DragDropEffects.Copy, window, new Point(50, 50)], null)!;
            drop.RoutedEvent = DragDrop.DropEvent; window.RaiseEvent(drop);
            Texts.Current.Set(true);
            var start = (Button)window.FindName("StartButton");
            await Wait(() => grid.Items.Count == 1 && start.IsVisible && start.IsEnabled);
            Check(((ComboBox)window.FindName("QualityCombo")).IsEnabled, "Quality control recovers after language switch during inspection");
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => start.IsVisible && ((AudioRow)grid.Items[0]).State == "Done");
            var completed = (AudioRow)grid.Items[0];
            Check(File.Exists(completed.Output) && before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(tagged))), "GUI conversion and source protection");
            window.Close();
            Check(ConverterSettings.DataDirectory.EndsWith(BuildInfo.DataFolder, StringComparison.Ordinal) && !typeof(App).Assembly.GetReferencedAssemblies().Any(a => a.Name is "XDSuite" or "XDBurner" or "XD.Common"), "Converter has independent settings and no launcher dependency");
            checks.Add("PASS: live German/English switching, original filenames retained, independent converter settings and no shared production application dependency.");
            checks.Add("PASS: actual WPF file drop, async inspection, language change during inspection, Convert button and completed MP3 output with unchanged source hash.");
            checks.Add("Fixtures and outputs retained for inspection: " + root);
            checks.Add("ALL CONVERTER TESTS PASSED");
        }
        catch (Exception error) { checks.Add("FAIL: " + error); throw; }
        finally { File.WriteAllLines(report, checks); }
    }
    private static bool ContainsText(JsonElement element, string value) => element.ValueKind switch { JsonValueKind.String => element.GetString() == value, JsonValueKind.Object => element.EnumerateObject().Any(p => ContainsText(p.Value, value)), JsonValueKind.Array => element.EnumerateArray().Any(p => ContainsText(p, value)), _ => false };
    private static void Check(bool okay, string description) { if (!okay) throw new InvalidOperationException(description); }
    private static void Select(ComboBox combo, string label) => combo.SelectedItem = combo.Items.Cast<object>().Single(i => i.ToString() == label);
    private static async Task Reject(Func<Task> action, string description) { try { await action(); } catch (Exception error) when (error is ConversionError or OperationCanceledException) { return; } throw new InvalidOperationException(description); }
    private static async Task Wait(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new InvalidOperationException("GUI workflow timed out."); await Task.Delay(25); }
    }
    private static void WriteTone(string path)
    {
        int frames = 88200, bytes = frames * 4;
        using var output = new BinaryWriter(File.Create(path), Encoding.ASCII);
        output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(bytes + 36); output.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1); output.Write((short)2); output.Write(44100); output.Write(176400); output.Write((short)4); output.Write((short)16); output.Write(Encoding.ASCII.GetBytes("data")); output.Write(bytes);
        for (int i = 0; i < frames; i++) { short sample = (short)(Math.Sin(i * Math.PI * 2 * 440 / 44100) * 12000); output.Write(sample); output.Write(sample); }
    }
}
