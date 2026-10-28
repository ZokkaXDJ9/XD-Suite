using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace XDBurner.Services;

internal sealed class ComScope : IDisposable
{
    private readonly List<object> objects = [];
    public dynamic Create(string progId) => Keep(Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)!)!);
    public T Keep<T>(T value) where T : class { objects.Add(value); return value; }
    public void Dispose()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (Marshal.IsComObject(objects[i])) Marshal.ReleaseComObject(objects[i]);
        objects.Clear();
    }
}

internal static class Native
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateStreamOnFileEx(string file, uint mode, uint attributes, [MarshalAs(UnmanagedType.Bool)] bool create, IStream? template, out IStream stream);
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    public static IStream OpenRead(string path)
    {
        SHCreateStreamOnFileEx(path, 0x20, 0, false, null, out var stream);
        return stream;
    }

    public static void PreventSleep(bool active) => SetThreadExecutionState(active ? 0x80000001u : 0x80000000u);
}

// Vtable order and IID from the Windows SDK's imapi2.h. This interface is IUnknown, not IDispatch.
[ComImport, Guid("D2FFD834-958B-426D-8470-2A13879C6A91"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IBurnVerification
{
    void SetLevel(int level);
    void GetLevel(out int level);
}

internal sealed class ProgressConnection : IDisposable
{
    private readonly object source;
    private readonly Guid iid;
    private readonly Delegate callback;
    public ProgressConnection(object source, string guid, Delegate callback)
    {
        this.source = source; iid = new Guid(guid); this.callback = callback;
        ComEventsHelper.Combine(source, iid, 0x200, callback);
    }
    public void Dispose() => ComEventsHelper.Remove(source, iid, 0x200, callback);
}
