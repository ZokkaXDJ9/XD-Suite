using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XDConverter;

public partial class App : Application
{
    private Mutex? mutex;
    private bool ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); Resources["Strings"] = Texts.Current;
        try
        {
            var engine = new AudioEngine(Argument(e.Args, "--engine") ?? Path.Combine(AppContext.BaseDirectory, "engine"));
            if (e.Args.Contains("--self-test"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await SelfTest.Run(engine, Argument(e.Args, "--report") ?? "converter-preview-tests.txt"); Shutdown(0); return;
            }
            if (e.Args.Contains("--render"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string directory = Path.GetFullPath(Argument(e.Args, "--render")!); Directory.CreateDirectory(directory);
                foreach (bool english in new[] { false, true })
                {
                    Texts.Current.Set(english);
                    var window = new MainWindow(engine, new(), true); MainWindow = window; window.Show(); window.SetPreview();
                    if (e.Args.Contains("--compact")) { window.Width = 1000; window.Height = 700; }
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(directory, english ? "Converter-English.png" : "Converter-German.png"))) encoder.Save(output);
                    window.Close();
                }
                Shutdown(0); return;
            }
            mutex = new Mutex(true, @"Local\XDConverter.Preview.App", out ownsMutex);
            if (!ownsMutex) { Shutdown(0); return; }
            var settings = PreviewSettings.Load();
            if (e.Args.Contains("--english")) settings.English = true;
            if (e.Args.Contains("--german")) settings.English = false;
            Texts.Current.Set(settings.English);
            MainWindow = new MainWindow(engine, settings); MainWindow.Show();
        }
        catch (Exception error)
        {
            string? report = Argument(e.Args, "--report");
            if (report != null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!); File.AppendAllText(report, "FAIL: " + error); }
            else { Log(error); MessageBox.Show(Texts.Current.ErrorText(error), Texts.Current["Title"]); }
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e); }
    private static string? Argument(string[] args, string name) { int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    public static void Log(Exception error)
    {
        try { string directory = Path.Combine(PreviewSettings.DataDirectory, "Logs"); Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, "preview.log"), DateTimeOffset.Now.ToString("O") + "\n" + error + "\n"); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
