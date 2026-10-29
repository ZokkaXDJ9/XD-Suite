using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;

namespace XDWaveEditor;

public readonly record struct FrameRange(long Start, long End)
{
    public long Length => Math.Max(0, End - Start);
}
public readonly record struct Piece(long SourceStart, long Length);
public readonly record struct Peak(float Min, float Max);

// Decoded audio stays on disk. Editing and undo retain only lists of source ranges.
public sealed class PcmSource : IDisposable
{
    private readonly SafeFileHandle handle;
    private Peak[][] peaks = [];
    private const int PeakFrames = 1024;
    public string Path { get; }
    public int Rate { get; }
    public int Channels { get; }
    public long Frames { get; }
    public PcmSource(string path, int rate, int channels)
    {
        Path = path; Rate = rate; Channels = channels;
        long size = new FileInfo(path).Length;
        if (channels is < 1 or > 2 || rate is < 8000 or > 384000 || size == 0 || size % (channels * 4) != 0) throw new EditorError("InvalidAudio");
        Frames = size / (channels * 4);
        handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
    }
    public float[] Read(long start, int count)
    {
        if (start < 0 || count < 0 || start + count > Frames) throw new ArgumentOutOfRangeException(nameof(start));
        var values = new float[checked(count * Channels)];
        Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
        long offset = start * Channels * 4;
        while (!bytes.IsEmpty)
        {
            int read = RandomAccess.Read(handle, bytes, offset);
            if (read == 0) throw new EndOfStreamException();
            bytes = bytes[read..]; offset += read;
        }
        return values;
    }
    public void BuildPeaks(CancellationToken token)
    {
        int count = checked((int)((Frames + PeakFrames - 1) / PeakFrames));
        var result = Enumerable.Range(0, Channels).Select(_ => new Peak[count]).ToArray();
        for (int block = 0; block < count; block++)
        {
            token.ThrowIfCancellationRequested();
            var values = Read((long)block * PeakFrames, (int)Math.Min(PeakFrames, Frames - (long)block * PeakFrames));
            for (int channel = 0; channel < Channels; channel++) result[channel][block] = Measure(values, channel);
        }
        peaks = result;
    }
    private Peak Measure(float[] values, int channel)
    {
        float min = 0, max = 0;
        for (int i = channel; i < values.Length; i += Channels)
        {
            float value = float.IsFinite(values[i]) ? Math.Clamp(values[i], -1, 1) : 0;
            min = Math.Min(min, value); max = Math.Max(max, value);
        }
        return new(min, max);
    }
    public Peak GetPeak(long start, long count, int channel)
    {
        if (count <= 0) return new(0, 0);
        if (count <= 512 || peaks.Length == 0) return Measure(Read(start, (int)count), channel);
        float min = 0, max = 0;
        for (long i = start / PeakFrames; i <= (start + count - 1) / PeakFrames; i++)
        {
            var peak = peaks[channel][i]; min = Math.Min(min, peak.Min); max = Math.Max(max, peak.Max);
        }
        return new(min, max);
    }
    public void Dispose() { handle.Dispose(); if (File.Exists(Path)) File.Delete(Path); }
}

public sealed class AudioDocument(PcmSource source, string original)
{
    private sealed record State(Piece[] Pieces, Guid Id, FrameRange Selection, long Cursor);
    private readonly Stack<State> undo = new(), redo = new();
    private List<Piece> pieces = [new(0, source.Frames)];
    private Piece[] clipboard = [];
    private Guid stateId = Guid.NewGuid(), savedId;
    public PcmSource Source { get; } = source;
    public string Original { get; } = original;
    public IReadOnlyList<Piece> Pieces => pieces;
    public long Frames => pieces.Sum(p => p.Length);
    public double Seconds => Frames / (double)Source.Rate;
    public FrameRange Selection { get; private set; }
    public long Cursor { get; set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool CanPaste => clipboard.Length > 0;
    public long ClipboardFrames => clipboard.Sum(p => p.Length);
    public bool IsDirty => savedId != Guid.Empty && savedId != stateId;
    public event Action? Edited;
    public event Action? SelectionChanged;
    public void MarkSaved() => savedId = stateId;
    public void Select(long start, long end)
    {
        start = Math.Clamp(start, 0, Frames); end = Math.Clamp(end, 0, Frames);
        Selection = new(Math.Min(start, end), Math.Max(start, end)); Cursor = start;
        SelectionChanged?.Invoke();
    }
    private State Snapshot() => new(pieces.ToArray(), stateId, Selection, Cursor);
    private void Restore(State state)
    {
        pieces = state.Pieces.ToList(); stateId = state.Id; Selection = state.Selection; Cursor = state.Cursor; Edited?.Invoke();
    }
    private void Commit(List<Piece> result, long cursor)
    {
        if (result.SequenceEqual(pieces)) return;
        undo.Push(Snapshot()); redo.Clear(); pieces = result; stateId = Guid.NewGuid();
        Cursor = Math.Clamp(cursor, 0, Frames); Selection = new(Cursor, Cursor); Edited?.Invoke();
    }
    public void Undo() { if (!CanUndo) return; redo.Push(Snapshot()); Restore(undo.Pop()); }
    public void Redo() { if (!CanRedo) return; undo.Push(Snapshot()); Restore(redo.Pop()); }
    private IEnumerable<Piece> Slice(long start, long end)
    {
        long offset = 0;
        foreach (var piece in pieces)
        {
            long first = Math.Max(start, offset), last = Math.Min(end, offset + piece.Length);
            if (last > first) yield return new(piece.SourceStart + first - offset, last - first);
            offset += piece.Length;
        }
    }
    public void DeleteSelection() { if (Selection.Length > 0) Remove([Selection]); }
    public void TrimSelection() { if (Selection.Length > 0) Commit(Slice(Selection.Start, Selection.End).ToList(), 0); }
    public void CopySelection() { if (Selection.Length > 0) clipboard = Slice(Selection.Start, Selection.End).ToArray(); }
    public void CutSelection() { if (Selection.Length > 0) { CopySelection(); DeleteSelection(); } }
    public void Paste()
    {
        if (!CanPaste) return;
        long start = Selection.Length > 0 ? Selection.Start : Math.Clamp(Cursor, 0, Frames);
        long end = Selection.Length > 0 ? Selection.End : start;
        var result = Slice(0, start).Concat(clipboard).Concat(Slice(end, Frames)).ToList();
        Commit(result, start + ClipboardFrames);
    }
    public void Remove(IEnumerable<FrameRange> ranges)
    {
        var result = new List<Piece>(); long position = 0;
        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            long start = Math.Clamp(range.Start, 0, Frames), end = Math.Clamp(range.End, 0, Frames);
            if (end <= position || end <= start) continue;
            if (start > position) result.AddRange(Slice(position, start));
            position = end;
        }
        result.AddRange(Slice(position, Frames));
        Commit(result, Selection.Start);
    }
    public Peak GetPeak(long start, long end, int channel)
    {
        float min = 0, max = 0; long position = 0;
        foreach (var piece in pieces)
        {
            long first = Math.Max(start, position), last = Math.Min(end, position + piece.Length);
            if (last > first)
            {
                var peak = Source.GetPeak(piece.SourceStart + first - position, last - first, channel);
                min = Math.Min(min, peak.Min); max = Math.Max(max, peak.Max);
            }
            position += piece.Length;
            if (position >= end) break;
        }
        return new(min, max);
    }
    public void Visit(FrameRange range, Action<long, float[]> visit, CancellationToken token)
    {
        long position = range.Start;
        foreach (var piece in Slice(range.Start, range.End))
        for (long read = 0; read < piece.Length;)
        {
            token.ThrowIfCancellationRequested();
            int count = (int)Math.Min(16384, piece.Length - read);
            visit(position, Source.Read(piece.SourceStart + read, count));
            read += count; position += count;
        }
    }
}
