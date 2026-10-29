using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XD.Common;

namespace XDSuite;

public partial class App : Application
{
    private Mutex? mutex;
    private bool ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        L10n.Initialize();
        if (e.Args.Contains("--english")) L10n.Set(UiLanguage.English, false);
        if (e.Args.Contains("--german")) L10n.Set(UiLanguage.German, false);
        try
        {
            if (e.Args.Contains("--test-uninstall"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await UninstallChecks.RunRealFixture(Argument(e.Args, "--test-uninstall")!, Argument(e.Args, "--report")!);
                Shutdown(0); return;
            }
            if (e.Args.Contains("--test-install"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string target = Argument(e.Args, "--test-install")!;
                string setup = Argument(e.Args, "--setup")!;
                string report = Argument(e.Args, "--report")!;
                var catalog = PackageValidation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));
                var package = catalog.Apps.Single(p => p.Id == (Argument(e.Args, "--app") ?? "xd-burner"));
                if (InstallationDiscovery.Find(package) != null || Directory.Exists(target)) throw new InvalidOperationException("An existing burner installation must not be replaced by the integration test.");
                await PackageInstaller.Install(setup, package, () => { }, CancellationToken.None, target);
                var installed = InstallationDiscovery.Find(package)!;
                if (!installed.Executable.Equals(Path.Combine(Path.GetFullPath(target), package.Executable), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected installation discovery path.");
                File.WriteAllText(report, "PASS: real installer launched through suite backend, hash verified, installed app discovered via Windows registration\n");
                Shutdown(0); return;
            }
            if (e.Args.Contains("--test-launch"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var catalog = PackageValidation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));
                var installed = InstallationDiscovery.Find(catalog.Apps.Single(p => p.Id == (Argument(e.Args, "--app") ?? "xd-burner"))) ?? throw new FileNotFoundException("Installed application not found.");
                using var child = InstallationDiscovery.Launch(installed);
                await Task.Delay(1800);
                child.Refresh();
                if (child.HasExited || !child.Responding || !child.CloseMainWindow()) throw new InvalidOperationException("Installed burner did not launch or close normally.");
                await child.WaitForExitAsync();
                File.WriteAllText(Argument(e.Args, "--report")!, "PASS: suite discovers and launches the installed standalone burner, which responds and closes normally\n");
                Shutdown(0); return;
            }
            if (e.Args.Contains("--self-test"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string report = Argument(e.Args, "--report") ?? "suite-self-test.txt";
                await SelfTest.Run(report);
                LocalizationChecks.Run();
                await UninstallChecks.Run(report);
                File.AppendAllText(report, "PASS: live German/English UI switching, dynamic messages, menu text and user-data protection\n");
                Shutdown(0); return;
            }
            if (e.Args.Contains("--render"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string directory = Argument(e.Args, "--render")!;
                Directory.CreateDirectory(directory);
                foreach (var language in Enum.GetValues<UiLanguage>())
                {
                    L10n.Set(language, false);
                    var window = new MainWindow(true); MainWindow = window; window.Show();
                    if (e.Args.Contains("--compact")) { window.Width = 1050; window.Height = 760; }
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(directory, "Suite-" + language + ".png"))) encoder.Save(output);
                    window.Close();
                }
                Shutdown(0); return;
            }
            mutex = new Mutex(true, @"Local\XDSuite.App", out ownsMutex);
            if (!ownsMutex) { LocalizedDialog.Show("XD Suite ist bereits geöffnet.", "XD Suite"); Shutdown(0); return; }
            MainWindow = new MainWindow(skipUpdateCheck: e.Args.Contains("--skip-update-check"), updateCompleted: e.Args.Contains("--update-completed")); MainWindow.Show();
            _ = RemoveLegacyForwarder();
        }
        catch (Exception ex)
        {
            Log(ex);
            string? report = Argument(e.Args, "--report");
            if (report != null) File.AppendAllText(report, "FAIL: " + ex);
            else LocalizedDialog.Show(ex.Message, "XD Suite");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e); }
    private static string? Argument(string[] args, string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    public static void Log(Exception ex)
    {
        try { Directory.CreateDirectory(SuiteSettings.DataDirectory); File.AppendAllText(Path.Combine(SuiteSettings.DataDirectory, "errors.log"), DateTimeOffset.Now.ToString("O") + "\n" + ex + "\n"); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private static async Task RemoveLegacyForwarder()
    {
        string oldFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "XDSuite");
        if (Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\').Equals(oldFolder, StringComparison.OrdinalIgnoreCase)) return;
        string oldExe = Path.Combine(oldFolder, "XDSuite.exe"), helper = Path.Combine(AppContext.BaseDirectory, "XDUpdateHelper.exe");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await Task.Delay(1000);
            try
            {
                if (!File.Exists(oldExe) || !File.Exists(helper) || !File.ReadAllBytes(oldExe).SequenceEqual(File.ReadAllBytes(helper))) return;
                File.Delete(oldExe);
                string config = oldExe + ".config";
                if (File.Exists(config) && File.Exists(helper + ".config") && File.ReadAllBytes(config).SequenceEqual(File.ReadAllBytes(helper + ".config"))) File.Delete(config);
                if (!Directory.EnumerateFileSystemEntries(oldFolder).Any()) Directory.Delete(oldFolder, false);
                return;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { return; }
        }
    }
}
