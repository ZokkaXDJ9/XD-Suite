using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XDBurner;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        L10n.Initialize();
        if (e.Args.Contains("--english")) L10n.Set(UiLanguage.English, false);
        if (e.Args.Contains("--german")) L10n.Set(UiLanguage.German, false);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteLog(args.Exception);
            LocalizedDialog.Show("XD Brenner konnte den letzten Schritt nicht ausführen.\n\n" + args.Exception.Message, "XD Brenner", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        try
        {
            if (e.Args.Contains("--self-test"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string report = GetArgument(e.Args, "--report") ?? Path.GetFullPath("self-test.txt");
                await Task.Run(() => SelfTest.Run(report));
                LocalizationChecks.Run();
                File.AppendAllText(report, "PASS: live German/English UI switching, progress translation and user-data protection\n");
                Shutdown(0);
                return;
            }
            if (e.Args.Contains("--render"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string directory = GetArgument(e.Args, "--render") ?? Path.GetFullPath("artifacts");
                Directory.CreateDirectory(directory);
                foreach (var mode in Enum.GetValues<ProjectKind>())
                {
                    var window = new MainWindow(true);
                    MainWindow = window;
                    window.Show();
                    window.SetPreview(mode, true);
                    if (e.Args.Contains("--compact")) { window.Width = 1080; window.Height = 780; }
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(directory, mode + ".png"))) encoder.Save(output);
                    window.Close();
                }
                Shutdown(0);
                return;
            }
            instanceMutex = new Mutex(true, @"Local\XDBurner.App", out ownsMutex);
            if (!ownsMutex)
            {
                LocalizedDialog.Show("XD Brenner ist bereits geöffnet. Bitte das vorhandene Fenster verwenden.", "XD Brenner");
                Shutdown(0);
                return;
            }
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            string? report = GetArgument(e.Args, "--report");
            if (report != null) File.AppendAllText(report, "\nFEHLER: " + ex);
            else LocalizedDialog.Show(ex.Message, "XD Brenner konnte nicht starten");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string? GetArgument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    internal static void WriteLog(Exception exception)
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDBurner", "Logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, DateTime.Today.ToString("yyyy-MM-dd") + ".log"), DateTimeOffset.Now.ToString("O") + "\n" + exception + "\n\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
