using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using XD.Common;

namespace XDSuite;

internal static class UninstallChecks
{
    internal static async Task Run(string report)
    {
        string root = Path.Combine(Path.GetTempPath(), "XD-uninstall-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string id = Guid.NewGuid().ToString();
        var package = FixturePackage(id);
        string registration = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + package.RegistryId;
        string exe = Path.Combine(root, package.Executable), uninstaller = Path.Combine(root, "unins000.exe");
        var originalLanguage = L10n.Current;
        MainWindow? window = null;
        try
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), exe);
            File.Copy(exe, uninstaller);
            using (var key = Registry.CurrentUser.CreateSubKey(registration))
            {
                key.SetValue("InstallLocation", root);
                key.SetValue("UninstallString", '"' + uninstaller + '"');
            }
            var installed = InstallationDiscovery.Find(package)!;
            Check(installed.Uninstaller == uninstaller, "registered uninstaller discovered");
            foreach (string bad in new[] { "unins000.exe", "cmd.exe /c " + uninstaller, '"' + uninstaller + "\" /SILENT", Path.Combine(Path.GetTempPath(), "unins000.exe"), exe, "\"unterminated" })
                Check(PackageUninstaller.Resolve(bad, exe) == null, "unsafe or unrelated uninstall command rejected");
            Check(PackageUninstaller.Resolve(uninstaller, exe) == uninstaller, "unquoted absolute Inno path accepted");
            bool invoked = false;
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                try { await PackageUninstaller.Uninstall(package, installed, token.Token, _ => { invoked = true; return Task.FromResult(0); }); throw new Exception("Cancellation was ignored"); }
                catch (OperationCanceledException) { }
            }
            Check(!invoked, "cancelled operation never starts a process");
            using (var key = Registry.CurrentUser.OpenSubKey(registration, true)) key!.DeleteValue("UninstallString");
            try { await PackageUninstaller.Uninstall(package, installed, CancellationToken.None, _ => { invoked = true; return Task.FromResult(0); }); throw new Exception("Stale registration accepted"); }
            catch (InvalidOperationException) { }
            Check(!invoked, "changed registration never starts a process");
            window = new MainWindow(new Catalog(1, [package]), _ => { invoked = true; return Task.FromResult(1); });
            window.Show();
            var button = (Button)window.FindName("UninstallButton");
            Check(button.IsVisible && !button.IsEnabled, "missing registered uninstaller disables the button");
            window.Close(); window = null;
            using (var key = Registry.CurrentUser.OpenSubKey(registration, true)) key!.SetValue("UninstallString", '"' + uninstaller + '"');

            int calls = 0;
            bool remove = false;
            var gate = new TaskCompletionSource<int>();
            window = new MainWindow(new Catalog(1, [package]), async start =>
            {
                calls++;
                Check(start.FileName == uninstaller && start.UseShellExecute && start.ArgumentList.SequenceEqual(new[] { "/NORESTART" }), "interactive uninstaller command, no silent flags");
                Check(!start.WorkingDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase), "working directory outside installation");
                await gate.Task;
                if (remove) { File.Delete(exe); Registry.CurrentUser.DeleteSubKeyTree(registration); }
                return remove ? 0 : 1;
            });
            window.Show();
            foreach (var language in Enum.GetValues<UiLanguage>())
            {
                L10n.Set(language, false);
                Check(((Button)window.FindName("UninstallButton")).Content.ToString() == (language == UiLanguage.German ? "Deinstallieren" : "Uninstall"), "button language switches");
                await Confirm(window, yes: false);
                Check(calls == 0 && File.Exists(exe), "No preserves installation and never starts uninstaller");
            }
            var cancelled = Confirm(window, yes: true);
            Check(calls == 1 && !((Button)window.FindName("UninstallButton")).IsEnabled && !((Button)window.FindName("ActionButton")).IsEnabled, "actions disabled while uninstaller runs");
            gate.SetResult(1);
            await cancelled;
            Check(File.Exists(exe) && ((Button)window.FindName("UninstallButton")).IsEnabled, "native cancellation retains installed state and restores controls");
            Check(((TextBlock)window.FindName("StatusTitle")).Text == SuiteText.Incomplete.ToString(), "cancellation does not report success");
            L10n.Set(UiLanguage.German, false);
            Check(((TextBlock)window.FindName("StatusTitle")).Text == "Deinstallation nicht abgeschlossen.", "result language switches");
            Check(!await PackageUninstaller.Uninstall(package, installed, CancellationToken.None, _ => throw new Win32Exception(1223)), "UAC cancellation is an incomplete operation");
            Check(!await PackageUninstaller.Uninstall(package, installed, CancellationToken.None, _ => Task.FromResult(0)), "zero exit without removal does not report success");
            remove = true;
            await Confirm(window, yes: true);
            Check(InstallationDiscovery.Find(package) == null && ((Button)window.FindName("UninstallButton")).Visibility == Visibility.Collapsed, "successful removal refreshes installed state and hides uninstall button");
            Check(((TextBlock)window.FindName("StatusTitle")).Text == SuiteText.Completed.ToString(), "successful removal reported");
            File.AppendAllText(report, "PASS: uninstall path validation, stale registration, pre-start cancellation and UAC cancellation\nPASS: real bilingual confirmation defaults to No; No preserves files; native cancellation keeps installed state; controls lock and restore; removal refreshes the UI\n");
        }
        finally
        {
            window?.Close();
            L10n.Set(originalLanguage, false);
            Registry.CurrentUser.DeleteSubKeyTree(registration, false);
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    // Used only with a randomly identified, isolated Inno fixture created by test-suite-uninstall.ps1.
    internal static async Task RunRealFixture(string directory, string report)
    {
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        string name = Path.GetFileName(root);
        if (!name.StartsWith("uninstall-test-", StringComparison.Ordinal) || !Guid.TryParse(name[15..], out var id)) throw new InvalidOperationException("Not an uninstall fixture directory.");
        var package = FixturePackage(id.ToString());
        var installed = InstallationDiscovery.Find(package) ?? throw new InvalidOperationException("Fixture not installed.");
        if (!string.Equals(installed.Executable, Path.Combine(root, "installed", package.Executable), StringComparison.OrdinalIgnoreCase) || installed.Uninstaller == null)
            throw new InvalidOperationException("Fixture outside isolated directory.");
        using var guard = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + package.RegistryId);
        if (guard == null) throw new InvalidOperationException("Fixture must be registered per-user.");
        var window = new MainWindow(new Catalog(1, [package]), start =>
        {
            Check(start.ArgumentList.SequenceEqual(new[] { "/NORESTART" }), "normal UI requests interactive uninstallation");
            // Only this guarded test fixture runs unattended. Production never adds these flags.
            start.ArgumentList.Add("/VERYSILENT"); start.ArgumentList.Add("/SUPPRESSMSGBOXES");
            return PackageUninstaller.RunProcess(start);
        });
        window.Show();
        try
        {
            await Confirm(window, yes: false);
            Check(File.Exists(installed.Executable), "No preserves real Inno installation");
            await Confirm(window, yes: true);
            Check(InstallationDiscovery.Find(package) == null && !File.Exists(installed.Executable), "real Inno uninstaller removes fixture");
            Check(((Button)window.FindName("UninstallButton")).Visibility == Visibility.Collapsed, "real uninstall refreshes UI");
            Check(File.Exists(Path.Combine(root, "keep.txt")), "unrelated file preserved");
            File.WriteAllText(report, "PASS: real Inno installation discovered through its random registration; No preserves it; Yes invokes the registered uninstaller and removes only the fixture; suite refreshes to not installed\nNOTE: isolated per-user AppId; silent flags added only by the test runner; real product installations untouched\n");
        }
        finally { window.Close(); }
    }

    private static AppPackage FixturePackage(string id) => new("uninstall-test-" + id, new("Deinstallationstest", "Uninstall test"), new("", ""), new("", ""), "0.0.1",
        "https://example.test/fixture.exe", new string('0', 64), 1, "UninstallFixture.exe", "{" + id + "}_is1", "UninstallFixture-" + id, []);

    private static async Task Confirm(MainWindow window, bool yes)
    {
        Exception? failure = null;
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Owner == window);
            try
            {
                var buttons = Descendants(dialog).OfType<Button>().ToArray();
                Check(buttons.Single(b => b.IsDefault).Content.ToString() == L10n.T("Nein"), "confirmation defaults to No");
                Check(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text == SuiteText.Confirm(((AppCard)((ListBox)window.FindName("AppList")).SelectedItem).Package).ToString()), "localized program named in confirmation");
                buttons.Single(b => b.Content.ToString() == L10n.T(yes ? "Ja" : "Nein")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error) { failure = error; dialog.Close(); }
        }));
        await window.UninstallSelected();
        if (failure != null) throw failure;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
