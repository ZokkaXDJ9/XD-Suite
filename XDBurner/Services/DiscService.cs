using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace XDBurner.Services;

public sealed class DiscService
{
    public long GetDataImageSize(IReadOnlyList<string> paths, CancellationToken token)
    {
        if (paths.Count == 0) return 0;
        using var scope = new ComScope();
        var entries = ProjectFiles.Snapshot(paths, token);
        dynamic image = CreateImage(scope, entries, "XD_DISC", int.MaxValue, new IgnoreProgress(), token);
        dynamic result = scope.Keep((object)image.CreateResultImage());
        token.ThrowIfCancellationRequested();
        return (long)result.TotalBlocks * 2048;
    }
    private sealed class IgnoreProgress : IProgress<OperationProgress> { public void Report(OperationProgress value) { } }
    public const long DefaultCdBytes = 360000L * 2048;
    private const string DataEvents = "2735413C-7F64-5B0F-8F00-5D77AFBE261E";
    private const string AudioEvents = "2735413F-7F64-5B0F-8F00-5D77AFBE261E";
    private const string EraseEvents = "2735413A-7F64-5B0F-8F00-5D77AFBE261E";

    public List<RecorderInfo> GetRecorders()
    {
        using var scope = new ComScope();
        dynamic master = scope.Create("IMAPI2.MsftDiscMaster2");
        var result = new List<RecorderInfo>();
        if (!(bool)master.IsSupportedEnvironment) return result;
        for (int i = 0; i < (int)master.Count; i++)
        {
            string id = master[i];
            dynamic recorder = OpenRecorder(scope, id);
            var volumes = ((Array)recorder.VolumePathNames).Cast<object>().Select(v => v.ToString());
            result.Add(new(id, $"{string.Join(", ", volumes)}  ·  {((string)recorder.VendorId).Trim()} {((string)recorder.ProductId).Trim()}".Trim(' ', '·')));
        }
        return result;
    }

    public MediaInfo GetMedia(string recorderId)
    {
        using var scope = new ComScope();
        dynamic recorder = OpenRecorder(scope, recorderId);
        dynamic writer = scope.Create("IMAPI2.MsftDiscFormat2Data");
        writer.Recorder = recorder;
        int type = (int)writer.CurrentPhysicalMediaType;
        bool blank = (bool)writer.MediaHeuristicallyBlank;
        long bytes;
        try { bytes = (long)writer.FreeSectorsOnMedia * 2048; }
        catch (COMException) { bytes = 0; } // Closed CD-RWs must still remain available to the erase tool.
        return new($"{MediaName(type)} · {(blank ? "leer" : "bereits beschrieben")} · {Format.Bytes(bytes)} frei", bytes, bytes / 2048, blank, type);
    }

    public void Eject(string recorderId)
    {
        using var scope = new ComScope();
        dynamic recorder = OpenRecorder(scope, recorderId);
        recorder.EjectMedia();
    }

    public void ExportIso(IReadOnlyList<string> paths, string volumeName, string destination, IProgress<OperationProgress> progress, CancellationToken token)
    {
        ValidateProject(paths, volumeName);
        ProjectFiles.ValidateExportPath(destination, paths);
        // Write beside the destination, then replace atomically. An existing ISO survives a failed export.
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Native.PreventSleep(true);
        try
        {
            using var scope = new ComScope();
            progress.Report(new("Dateien werden geprüft …"));
            var entries = ProjectFiles.Snapshot(paths, token);
            dynamic image = CreateImage(scope, entries, volumeName, 360000, progress, token);
            dynamic result = scope.Keep((object)image.CreateResultImage());
            IStream stream = scope.Keep((IStream)result.ImageStream);
            CopyImage(stream, temporary, progress, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            Native.PreventSleep(false);
        }
    }

    public void BurnData(IReadOnlyList<string> paths, BurnOptions options, IProgress<OperationProgress> progress, CancellationToken token)
    {
        ValidateProject(paths, options.VolumeName);
        using var scope = new ComScope();
        dynamic recorder = OpenRecorder(scope, options.RecorderId);
        WithRecorder((object)recorder, options.Eject, progress, () =>
        {
            progress.Report(new("Rohling und Quelldateien werden geprüft …"));
            dynamic writer = CreateDataWriter(scope, recorder, options);
            var entries = ProjectFiles.Snapshot(paths, token);
            dynamic image = CreateImage(scope, entries, options.VolumeName, (int)writer.FreeSectorsOnMedia, progress, token);
            dynamic result = scope.Keep((object)image.CreateResultImage());
            if ((long)result.TotalBlocks > (long)writer.FreeSectorsOnMedia)
                throw new InvalidOperationException("Die Dateien passen mit dem Dateisystem nicht auf den eingelegten Rohling.");
            IStream stream = scope.Keep((IStream)result.ImageStream);
            WriteData(writer, stream, progress, token);
        });
    }

    public void BurnIso(string path, BurnOptions options, IProgress<OperationProgress> progress, CancellationToken token)
    {
        using var scope = new ComScope();
        IStream stream = scope.Keep(Native.OpenRead(path));
        stream.Stat(out var stat, 1);
        ValidateIso(stream, stat.cbSize);
        dynamic recorder = OpenRecorder(scope, options.RecorderId);
        WithRecorder((object)recorder, options.Eject, progress, () =>
        {
            progress.Report(new("ISO-Datei und Rohling werden geprüft …"));
            dynamic writer = CreateDataWriter(scope, recorder, options);
            if (stat.cbSize / 2048 > (long)writer.FreeSectorsOnMedia)
                throw new InvalidOperationException("Die ISO-Datei passt nicht auf den eingelegten Rohling.");
            WriteData(writer, stream, progress, token);
        });
    }

    public void BurnAudio(IReadOnlyList<string> paths, BurnOptions options, IProgress<OperationProgress> progress, CancellationToken token)
    {
        if (paths.Count is < 1 or > 99) throw new InvalidOperationException("Eine Audio-CD braucht 1 bis 99 Titel.");
        string temporary = Path.Combine(Path.GetTempPath(), "XDBurner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        Native.PreventSleep(true);
        try
        {
            // Decode every track before touching the disc. A bad final file must not waste a CD-R.
            var tracks = new List<(string Path, long Sectors)>();
            for (int i = 0; i < paths.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                progress.Report(new($"Titel {i + 1} von {paths.Count} wird vorbereitet: {Path.GetFileName(paths[i])}", i * 100d / paths.Count));
                string pcm = Path.Combine(temporary, $"{i:00}.pcm");
                tracks.Add((pcm, AudioService.ConvertToPcm(paths[i], pcm, token, options.AudioGapSeconds)));
            }
            token.ThrowIfCancellationRequested();
            using var scope = new ComScope();
            dynamic recorder = OpenRecorder(scope, options.RecorderId);
            WithRecorder((object)recorder, options.Eject, progress, () =>
            {
                dynamic writer = scope.Create("IMAPI2.MsftDiscFormat2TrackAtOnce");
                writer.Recorder = recorder;
                writer.ClientName = "XDBurner";
                writer.DoNotFinalizeMedia = false;
                EnsureCd(writer, recorder, true);
                SetSpeed(writer, options.Slow);
                int track = 0;
                Action<object, object> callback = (sender, args) =>
                {
                    dynamic state = args;
                    double portion = SectorProgress((object)state);
                    int action = (int)state.CurrentAction;
                    int currentTrack = Math.Min(track, tracks.Count - 1);
                    progress.Report(new($"Titel {currentTrack + 1} von {tracks.Count}: " + (action == 3 ? "wird abgeschlossen …" : "wird gebrannt …"), (currentTrack + portion) * 100d / tracks.Count));
                    if (token.IsCancellationRequested) TryCancel(sender, true);
                };
                using var connection = new ProgressConnection((object)writer, AudioEvents, callback);
                bool prepared = false;
                try
                {
                    progress.Report(new("Audio-CD wird vorbereitet …"));
                    writer.PrepareMedia();
                    prepared = true;
                    long required = tracks.Sum(t => t.Sectors + 150);
                    if (required > (long)writer.FreeSectorsOnMedia)
                        throw new InvalidOperationException("Die Titel passen einschließlich der Pausen nicht auf den eingelegten Rohling.");
                    for (track = 0; track < tracks.Count; track++)
                    {
                        token.ThrowIfCancellationRequested();
                        IStream stream = scope.Keep(Native.OpenRead(tracks[track].Path));
                        writer.AddAudioTrack(stream);
                    }
                    token.ThrowIfCancellationRequested();
                    progress.Report(new("Audio-CD wird abgeschlossen. Bitte warten …"));
                }
                finally
                {
                    if (prepared) writer.ReleaseMedia();
                }
                token.ThrowIfCancellationRequested();
            });
        }
        finally
        {
            // The directory is freshly created under the process temp folder and only contains our PCM files.
            foreach (string file in Directory.EnumerateFiles(temporary)) File.Delete(file);
            Directory.Delete(temporary);
            Native.PreventSleep(false);
        }
    }

    public void Erase(BurnOptions options, IProgress<OperationProgress> progress)
    {
        using var scope = new ComScope();
        dynamic recorder = OpenRecorder(scope, options.RecorderId);
        WithRecorder((object)recorder, options.Eject, progress, () =>
        {
            dynamic writer = scope.Create("IMAPI2.MsftDiscFormat2Erase");
            writer.Recorder = recorder;
            writer.ClientName = "XDBurner";
            if ((int)writer.CurrentPhysicalMediaType != 3 || !(bool)writer.IsCurrentMediaSupported(recorder))
                throw new InvalidOperationException("Bitte eine wiederbeschreibbare CD-RW einlegen. CD-Rs können nicht gelöscht werden.");
            writer.FullErase = options.FullErase;
            Action<object, int, int> callback = (_, elapsed, total) => progress.Report(new("CD-RW wird gelöscht. Bitte das Laufwerk geschlossen lassen …", total > 0 ? Math.Min(99, elapsed * 100d / total) : null));
            using var connection = new ProgressConnection((object)writer, EraseEvents, callback);
            progress.Report(new("CD-RW wird gelöscht …"));
            writer.EraseMedia();
        });
    }

    private static dynamic OpenRecorder(ComScope scope, string id)
    {
        dynamic recorder = scope.Create("IMAPI2.MsftDiscRecorder2");
        recorder.InitializeDiscRecorder(id);
        return recorder;
    }

    private static dynamic CreateDataWriter(ComScope scope, dynamic recorder, BurnOptions options)
    {
        dynamic writer = scope.Create("IMAPI2.MsftDiscFormat2Data");
        writer.Recorder = recorder;
        writer.ClientName = "XDBurner";
        EnsureCd(writer, recorder, true);
        writer.ForceMediaToBeClosed = true;
        SetSpeed(writer, options.Slow);
        if (options.Verify) ((IBurnVerification)(object)writer).SetLevel(2); // Full read-back verification.
        return writer;
    }

    private static void EnsureCd(dynamic writer, dynamic recorder, bool blankRequired)
    {
        if (!(bool)writer.IsRecorderSupported(recorder)) throw new InvalidOperationException("Dieses Laufwerk unterstützt die gewünschte Brennart nicht.");
        int type = (int)writer.CurrentPhysicalMediaType;
        if (type != 2 && type != 3) throw new InvalidOperationException("Bitte einen passenden CD-R- oder CD-RW-Rohling einlegen.");
        if (!(bool)writer.IsCurrentMediaSupported(recorder)) throw new InvalidOperationException("Der eingelegte Rohling kann mit diesem Laufwerk nicht beschrieben werden.");
        if (blankRequired && !(bool)writer.MediaHeuristicallyBlank)
            throw new InvalidOperationException("Die CD ist bereits beschrieben. Bitte einen leeren Rohling verwenden oder die CD-RW zuerst über „CD-RW löschen“ leeren.");
    }

    private static void SetSpeed(dynamic writer, bool slow)
    {
        var speeds = ((Array)writer.SupportedWriteSpeeds).Cast<object>().Select(Convert.ToInt32).Where(s => s > 0).ToArray();
        writer.SetWriteSpeed(slow && speeds.Length > 0 ? speeds.Min() : -1, false);
    }

    private static dynamic CreateImage(ComScope scope, List<DataEntry> entries, string name, int freeSectors, IProgress<OperationProgress> progress, CancellationToken token)
    {
        if (entries.Sum(e => e.Length) > freeSectors * 2048L) throw new InvalidOperationException("Die ausgewählten Dateien sind zu groß für die CD.");
        dynamic image = scope.Create("IMAPI2FS.MsftFileSystemImage");
        image.ChooseImageDefaultsForMediaType(2); // CD-R defaults, also appropriate for a blank CD-RW.
        image.FileSystemsToCreate = 3; // ISO 9660 + Joliet, compatible with common computers and players.
        image.FreeMediaBlocks = freeSectors;
        image.VolumeName = name;
        dynamic root = scope.Keep((object)image.Root);
        for (int i = 0; i < entries.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var entry = entries[i];
            if (i % 32 == 0) progress.Report(new($"CD-Inhalt wird zusammengestellt: {entry.DiscPath}"));
            if (entry.IsDirectory) root.AddDirectory(entry.DiscPath);
            else
            {
                IStream stream = scope.Keep(Native.OpenRead(entry.SourcePath));
                root.AddFile(entry.DiscPath, stream);
            }
        }
        return image;
    }

    private static void WriteData(dynamic writer, IStream stream, IProgress<OperationProgress> progress, CancellationToken token)
    {
        Action<object, object> callback = (sender, args) =>
        {
            dynamic state = args;
            int action = (int)state.CurrentAction;
            string text = action switch
            {
                0 => "Rohling wird geprüft …", 1 => "Medium wird vorbereitet …", 2 => "Brenner wird vorbereitet …",
                3 => "Laser wird kalibriert …", 4 => "Dateien werden auf die CD gebrannt …", 5 => "CD wird abgeschlossen …",
                6 => "Brennvorgang wird beendet …", 7 => "Geschriebene Daten werden von der CD zurückgelesen und geprüft …", _ => "Brennvorgang läuft …"
            };
            progress.Report(new(text, action == 4 ? SectorProgress((object)state) * 100 : null));
            if (token.IsCancellationRequested) TryCancel(sender, false);
        };
        using var connection = new ProgressConnection((object)writer, DataEvents, callback);
        token.ThrowIfCancellationRequested();
        progress.Report(new("Brennvorgang wird gestartet …"));
        writer.Write(stream);
        token.ThrowIfCancellationRequested();
    }

    private static double SectorProgress(object args)
    {
        dynamic state = args;
        return (int)state.SectorCount > 0
            ? Math.Clamp(((double)state.LastWrittenLba - (double)state.StartLba + 1) / (double)state.SectorCount, 0, 1) : 0;
    }

    private static void TryCancel(dynamic writer, bool audio)
    {
        try { if (audio) writer.CancelAddTrack(); else writer.CancelWrite(); }
        catch (COMException) { /* Some phases cannot be interrupted. Retry on the next progress event. */ }
    }

    private static void WithRecorder(object recorderObject, bool eject, IProgress<OperationProgress> progress, Action action)
    {
        dynamic recorder = recorderObject;
        Native.PreventSleep(true);
        bool acquired = false;
        try
        {
            recorder.AcquireExclusiveAccess(false, "XDBurner");
            acquired = true;
            action();
        }
        finally
        {
            if (acquired) recorder.ReleaseExclusiveAccess();
            Native.PreventSleep(false);
        }
        if (eject)
        {
            try { recorder.EjectMedia(); }
            catch (COMException) { progress.Report(new("Brennen erfolgreich. Bitte die CD manuell auswerfen.")); }
        }
    }

    private static void ValidateProject(IReadOnlyList<string> paths, string name)
    {
        if (paths.Count == 0) throw new InvalidOperationException("Bitte zuerst Dateien oder Ordner hinzufügen.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new InvalidOperationException("Bitte einen CD-Namen mit 1 bis 32 Zeichen ohne / \\ : * ? \" < > | eingeben.");
    }

    internal static void ValidateIso(IStream stream, long length)
    {
        if (length < 17 * 2048 || length % 2048 != 0) throw new InvalidOperationException("Die Datei ist kein gültiges ISO-Abbild mit 2048-Byte-Sektoren.");
        stream.Seek(16 * 2048 + 1, 0, IntPtr.Zero);
        byte[] signature = new byte[5];
        stream.Read(signature, signature.Length, IntPtr.Zero);
        stream.Seek(0, 0, IntPtr.Zero);
        if (System.Text.Encoding.ASCII.GetString(signature) != "CD001")
            throw new InvalidOperationException("Dieses Abbild enthält kein ISO-9660-Dateisystem. Bitte eine normale Daten-ISO verwenden; BIN/CUE und Audio-Abbilder werden nicht unterstützt.");
    }

    private static void CopyImage(IStream stream, string path, IProgress<OperationProgress> progress, CancellationToken token)
    {
        stream.Stat(out var stat, 1);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[1024 * 1024];
        IntPtr count = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                stream.Read(buffer, buffer.Length, count);
                int read = Marshal.ReadInt32(count);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                progress.Report(new("ISO-Datei wird gespeichert …", output.Length * 100d / stat.cbSize));
            }
            if (output.Length != stat.cbSize) throw new IOException("Das ISO-Abbild konnte nicht vollständig gelesen werden.");
        }
        finally { Marshal.FreeHGlobal(count); }
    }

    private static string MediaName(int type) => L10n.T(type switch { 2 => "CD-R", 3 => "CD-RW", 1 => "CD-ROM", _ => "Kein CD-R/CD-RW-Medium" });

    public static string DescribeError(Exception error)
    {
        if (error is COMException com)
        {
            return unchecked((uint)com.HResult) switch
            {
                0xC0AA0202 => "Keine CD eingelegt. Bitte einen passenden Rohling einlegen und „Aktualisieren“ wählen.",
                0xC0AA0203 => "Das Medium ist nicht kompatibel. Bitte eine CD-R oder CD-RW verwenden.",
                0xC0AA0205 => "Das Laufwerk liest die CD noch ein. Bitte kurz warten und erneut versuchen.",
                0xC0AA0210 => "Der Brenner wird von einem anderen Programm verwendet. Bitte dieses schließen.",
                0xC0AA0209 => "Die eingelegte CD ist schreibgeschützt oder bereits abgeschlossen.",
                0xC0AA0002 => "Der Vorgang wurde abgebrochen. Ein bereits beschriebener CD-R-Rohling ist eventuell nicht mehr verwendbar.",
                0xC0AA0300 => "Der Datenstrom zum Brenner wurde unterbrochen. Bitte lokale Quelldateien und eine langsamere Brenngeschwindigkeit verwenden.",
                _ => $"Windows meldet einen Gerätefehler (0x{com.HResult:X8}): {com.Message}"
            };
        }
        return error.Message;
    }
}
