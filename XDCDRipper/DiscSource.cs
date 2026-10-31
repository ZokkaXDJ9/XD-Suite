using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XDCDRipper;
public sealed record CdTrack(int Number, int StartSector, int EndSector)
{
    public int Sectors => EndSector - StartSector;
    public string Duration => TimeSpan.FromSeconds(Sectors / 75d).ToString(@"mm\:ss");
}
public interface IDiscSource : IDisposable
{
    IReadOnlyList<CdTrack> Tracks { get; }
    byte[] Read(int startSector, int count);
    void VerifyDisc();
}
public sealed class WindowsDiscSource : IDiscSource
{
    readonly SafeFileHandle handle;
    readonly byte[] table;
    public IReadOnlyList<CdTrack> Tracks { get; }
    public static string[] Drives() => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.CDRom).Select(d => d.Name[..2]).ToArray();
    public WindowsDiscSource(string drive)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(drive, "^[A-Za-z]:$")) throw new ArgumentException("Ungültiges Laufwerk / Invalid drive.");
        handle = CreateFile(@"\\.\" + drive, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw Error("Laufwerk öffnen / Open drive"); }
        try
        {
            table = ReadToc(); Tracks = ParseToc(table);
            if (!DeviceIoControl(handle, 0x002d4804, [1], 1, null, 0, out _, IntPtr.Zero)) throw Error("CD während des Lesens sichern / Lock disc during reading");
            VerifyDisc();
        }
        catch { handle.Dispose(); throw; }
    }
    byte[] ReadToc()
    {
        var output = new byte[804]; // READ_TOC_EX, format TOC, LBA addressing.
        if (!DeviceIoControl(handle, 0x00024054, new byte[4], 4, output, output.Length, out int bytes, IntPtr.Zero)) throw Error("Inhaltsverzeichnis lesen / Read disc table of contents");
        if (bytes < 4 || bytes > output.Length) throw new IOException("Ungültiges CD-Inhaltsverzeichnis / Invalid CD table of contents.");
        return output[..bytes];
    }
    public static IReadOnlyList<CdTrack> ParseToc(byte[] toc)
    {
        if (toc.Length < 20) throw new InvalidDataException("Unvollständiges CD-Inhaltsverzeichnis / Incomplete CD table of contents.");
        int first = toc[2], last = toc[3], declared = BinaryPrimitives.ReadUInt16BigEndian(toc) + 2;
        if (first != 1 || last < first || last > 99 || declared < 4 + (last - first + 2) * 8 || declared > toc.Length) throw new InvalidDataException("Nicht unterstütztes CD-Inhaltsverzeichnis / Unsupported CD table of contents.");
        var result = new List<CdTrack>();
        for (int i = 0; i <= last - first; i++)
        {
            int offset = 4 + i * 8, next = offset + 8;
            if (toc[offset + 2] != first + i || (i == last - first && toc[next + 2] != 0xaa)) throw new InvalidDataException("Ungültige Titelreihenfolge / Invalid track order.");
            if ((toc[offset + 1] & 4) != 0) throw new InvalidDataException("Diese Beta unterstützt reine Audio-CDs, keine Daten- oder Mixed-Mode-CDs. / This beta supports audio-only CDs, not data or mixed-mode discs.");
            if ((toc[offset + 1] & 1) != 0) throw new InvalidDataException("CD mit Pre-Emphasis wird noch nicht unterstützt. / Pre-emphasis CDs are not supported yet.");
            int start = BinaryPrimitives.ReadInt32BigEndian(toc.AsSpan(offset + 4)), end = BinaryPrimitives.ReadInt32BigEndian(toc.AsSpan(next + 4));
            if (start < 0 || end <= start || end > 100 * 60 * 75) throw new InvalidDataException("Ungültige Titelgrenzen / Invalid track boundaries.");
            result.Add(new(first + i, start, end));
        }
        return result;
    }
    public byte[] Read(int startSector, int count)
    {
        if (startSector < 0 || count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count));
        var request = new byte[16]; BinaryPrimitives.WriteInt64LittleEndian(request, startSector * 2048L); BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(8), count); BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(12), 2); // TRACK_MODE_TYPE.CDDA
        var output = new byte[count * 2352];
        if (!DeviceIoControl(handle, 0x0002403e, request, request.Length, output, output.Length, out int bytes, IntPtr.Zero)) throw Error("Audiosektor " + startSector);
        if (bytes != output.Length) throw new IOException("Unvollständiger Audiosektor / Incomplete audio sector.");
        return output;
    }
    public void VerifyDisc() { if (!table.SequenceEqual(ReadToc())) throw new IOException("CD wurde gewechselt / Disc changed."); }
    public void Dispose() { if (!handle.IsClosed && !handle.IsInvalid) { DeviceIoControl(handle, 0x002d4804, [0], 1, null, 0, out _, IntPtr.Zero); handle.Dispose(); } }
    static IOException Error(string operation) => new(operation + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool DeviceIoControl(SafeFileHandle device, uint code, byte[]? input, int inputLength, [Out] byte[]? output, int outputLength, out int returned, IntPtr overlapped);
}
