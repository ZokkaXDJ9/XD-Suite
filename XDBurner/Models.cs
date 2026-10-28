using System.IO;

namespace XDBurner;

public enum ProjectKind { Data, Audio, Iso, Erase }

public sealed record RecorderInfo(string Id, string SourceName)
{
    public string Name => L10n.T(SourceName);
}
public sealed record MediaInfo(string Description, long FreeBytes, long AudioSectors, bool IsBlank, int Type);
public sealed record OperationProgress(string Message, double? Percent = null);
public sealed record BurnOptions(string RecorderId, string VolumeName, bool Verify, bool Eject, bool Slow, bool FullErase, int AudioGapSeconds = 2);
public sealed record DataEntry(string DiscPath, string SourcePath, long Length, bool IsDirectory);

public sealed record ProjectItem(string SourcePath, string Name, bool IsDirectory, long Bytes, TimeSpan Duration)
{
    public string TypeLabel => L10n.T(Duration > TimeSpan.Zero ? "Audiotitel" : IsDirectory ? "Ordner" : Path.GetExtension(Name).TrimStart('.').ToUpperInvariant() + "-Datei");
    public string SizeLabel => Duration > TimeSpan.Zero ? Format.Duration(Duration) : Format.Bytes(Bytes);
    public static ProjectItem FromData(string path)
    {
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar) == path)
            throw new InvalidOperationException("Bitte einen Ordner auswählen, kein ganzes Laufwerk.");
        bool directory = Directory.Exists(path);
        var entries = ProjectFiles.Snapshot([path]);
        return new(path, Path.GetFileName(path), directory, entries.Sum(e => e.Length), TimeSpan.Zero);
    }
}

public static class Format
{
    public static string Bytes(long bytes) => bytes >= 1L << 30 ? (bytes / (double)(1L << 30)).ToString("0.00", L10n.Culture) + " GB" : bytes >= 1L << 20 ? (bytes / (double)(1L << 20)).ToString("0.0", L10n.Culture) + " MB" : bytes >= 1024 ? (bytes / 1024d).ToString("0.0", L10n.Culture) + " KB" : $"{bytes} B";
    public static string Duration(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
}

public static class ProjectFiles
{
    public static List<DataEntry> Snapshot(IEnumerable<string> paths, CancellationToken token = default)
    {
        var entries = new List<DataEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new InvalidOperationException($"Der Name „{name}“ kommt mehrfach vor. Bitte die Dateien oder Ordner vorher umbenennen.");
            Add(path, name);
        }
        return entries;

        void Add(string source, string target)
        {
            token.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(source);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException($"„{source}“ ist eine Verknüpfung oder ein Cloud-Platzhalter. Bitte zuerst lokal kopieren.");
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                entries.Add(new(target, source, 0, true));
                foreach (var child in Directory.EnumerateFileSystemEntries(source).Order(StringComparer.OrdinalIgnoreCase))
                    Add(child, target + "/" + Path.GetFileName(child));
            }
            else
            {
                long size = new FileInfo(source).Length;
                if (size > uint.MaxValue) throw new InvalidOperationException($"„{source}“ ist für das CD-Dateisystem zu groß.");
                entries.Add(new(target, source, size, false));
            }
        }
    }

    public static void ValidateExportPath(string destination, IEnumerable<string> sources)
    {
        destination = Path.GetFullPath(destination);
        foreach (string source in sources)
        {
            string full = Path.GetFullPath(source);
            if (destination.Equals(full, StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(full) && destination.StartsWith(full.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bitte die ISO-Datei außerhalb der ausgewählten Quelldateien und Quellordner speichern.");
        }
    }

    public static long AudioSectors(IEnumerable<TimeSpan> durations, int gapSeconds = 2)
    {
        if (gapSeconds is < 2 or > 30) throw new ArgumentOutOfRangeException(nameof(gapSeconds));
        return durations.Sum(d => Math.Max(300L, (long)Math.Ceiling(d.TotalSeconds * 75)) + gapSeconds * 75L);
    }
}
