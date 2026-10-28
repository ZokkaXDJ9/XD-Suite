using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XDConverter;

internal static class IconChecks
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr large, [Out] IntPtr[] small, uint count);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static void VerifyWindowAndExe(Window window, string outputDirectory)
    {
        if (window.Icon == null) throw new InvalidOperationException("Window icon missing.");
        var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/XDConverter.Preview;component/Assets/XDConverter.ico"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
            if (!decoder.Frames.Any(frame => frame.PixelWidth == size && frame.PixelHeight == size)) throw new InvalidOperationException("Icon size missing: " + size);
        var icons = new IntPtr[1];
        if (ExtractIconEx(Environment.ProcessPath!, 0, IntPtr.Zero, icons, 1) == 0 || icons[0] == IntPtr.Zero) throw new InvalidOperationException("Executable icon resource missing.");
        try { Verify(icons[0], decoder, Path.Combine(outputDirectory, "exe-icon.png")); }
        finally { DestroyIcon(icons[0]); }
        IntPtr handle = new WindowInteropHelper(window).Handle;
        IntPtr small = SendMessage(handle, 0x7F, IntPtr.Zero, IntPtr.Zero);
        if (small == IntPtr.Zero) small = SendMessage(handle, 0x7F, new IntPtr(2), IntPtr.Zero);
        if (small == IntPtr.Zero) throw new InvalidOperationException("Native window/taskbar icon missing.");
        Verify(small, decoder, Path.Combine(outputDirectory, "window-icon.png"));
    }
    private static void Verify(IntPtr icon, BitmapDecoder expected, string output)
    {
        var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        var frame = expected.Frames.FirstOrDefault(f => f.PixelWidth == bitmap.PixelWidth && f.PixelHeight == bitmap.PixelHeight) ?? throw new InvalidOperationException("Unexpected shell icon size.");
        byte[] actual = Pixels(bitmap), reference = Pixels(frame);
        int opaque = 0, matching = 0;
        for (int i = 0; i < reference.Length; i += 4)
        {
            if (reference[i + 3] < 250) continue;
            opaque++;
            if (actual[i + 3] >= 250 && Math.Abs(actual[i] - reference[i]) <= 2 && Math.Abs(actual[i + 1] - reference[i + 1]) <= 2 && Math.Abs(actual[i + 2] - reference[i + 2]) <= 2) matching++;
        }
        if (opaque == 0 || matching < opaque * .95) throw new InvalidOperationException("Windows icon does not match the converter artwork.");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(output); encoder.Save(file);
    }
    private static byte[] Pixels(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes;
    }
}
