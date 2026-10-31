using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XDCDRipper;
internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        var app = new Application(); app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "XD CD Ripper"); e.Handled = true; };
        app.Startup += async (_, _) =>
        {
            try
            {
                if (args.Contains("--self-test")) { app.ShutdownMode = ShutdownMode.OnExplicitShutdown; await SelfTest.Run(Arg(args, "--engine") ?? Path.Combine(AppContext.BaseDirectory, "engine", "ffmpeg.exe"), Arg(args, "--report")!); app.Shutdown(0); return; }
                var window = new MainWindow(args); app.MainWindow = window;
                if (Arg(args, "--render") is { } folder)
                {
                    window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -16000; window.ShowActivated = false; window.Show(); await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory(folder); using (var file = File.Create(Path.Combine(folder, args.Contains("--english") ? "CDRipper-English.png" : "CDRipper-German.png"))) encoder.Save(file); window.Close(); return;
                }
                window.Show();
            }
            catch (Exception e) { if (Arg(args, "--report") is { } report) File.AppendAllText(report, "FAIL: " + e); else MessageBox.Show(e.Message, "XD CD Ripper"); app.Shutdown(1); }
        }; app.Run();
    }
    static string? Arg(string[] args, string key) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
}
