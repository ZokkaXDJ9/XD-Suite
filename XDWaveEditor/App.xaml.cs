using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XDWaveEditor;

public partial class App : Application
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    private Mutex? mutex;
    private bool ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); Resources["T"] = Texts.Current;
        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID("XDSuite.WaveEditor.Preview"));
            var engine = new AudioEngine(Arg(e.Args, "--engine") ?? Path.Combine(AppContext.BaseDirectory, "engine"));
            if (e.Args.Contains("--self-test"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await SelfTest.Run(engine, Arg(e.Args, "--report") ?? "wave-editor-tests.txt"); Shutdown(0); return;
            }
            if (e.Args.Contains("--render"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string directory = Path.GetFullPath(Arg(e.Args, "--render")!); Directory.CreateDirectory(directory);
                string input = await SelfTest.CreateDemo(engine);
                try
                {
                    foreach (bool english in new[] { false, true })
                    {
                        Texts.Current.Set(english);
                        var window = new MainWindow(engine); MainWindow = window;
                        var doc = await engine.Open(input, CancellationToken.None); window.LoadDocument(doc);
                        doc.Select((long)(doc.Source.Rate * 1.1), (long)(doc.Source.Rate * 2.3));
                        window.Show();
                        if (e.Args.Contains("--compact")) { window.Width = 1000; window.Height = 720; }
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var file = File.Create(Path.Combine(directory, english ? "Wave-English.png" : "Wave-German.png"))) encoder.Save(file);
                        var effects = new EffectDialog(engine, doc, EffectKind.Equalizer) { Owner = window }; effects.Show();
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); effects.UpdateLayout();
                        var effectBitmap = new RenderTargetBitmap((int)effects.ActualWidth, (int)effects.ActualHeight, 96, 96, PixelFormats.Pbgra32); effectBitmap.Render(effects);
                        var effectEncoder = new PngBitmapEncoder(); effectEncoder.Frames.Add(BitmapFrame.Create(effectBitmap));
                        using (var file = File.Create(Path.Combine(directory, english ? "Effects-English.png" : "Effects-German.png"))) effectEncoder.Save(file);
                        effects.Close();
                        window.Close();
                    }
                }
                finally { File.Delete(input); }
                Shutdown(0); return;
            }
            mutex = new Mutex(true, @"Local\XDWaveEditor.Preview.App", out ownsMutex);
            if (!ownsMutex) { Shutdown(0); return; }
            Texts.Current.Load();
            if (e.Args.Contains("--english")) Texts.Current.Set(true);
            if (e.Args.Contains("--german")) Texts.Current.Set(false);
            MainWindow = new MainWindow(engine); MainWindow.Show();
        }
        catch (Exception error)
        {
            string? report = Arg(e.Args, "--report");
            if (report != null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!); File.AppendAllText(report, "FAIL: " + error); }
            else MessageBox.Show(Texts.Current.Error(error), Texts.Current["Title"], MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e); }
    private static string? Arg(string[] args, string key) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
}
