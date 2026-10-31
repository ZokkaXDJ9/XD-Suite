using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XD.UpdateHelper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Environment.CurrentDirectory = Path.GetTempPath();
        if (args.Length == 2 && args[0] == "--cleanup-forwarder")
        {
            CleanupForwarder(args[1]);
            return;
        }
        if (Path.GetFileName(Application.ExecutablePath).Equals("XDSuite.exe", StringComparison.OrdinalIgnoreCase))
        {
            // Compatibility forwarder for the first update out of the old per-user directory.
            using (var root = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
            using (var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{F80CC53F-43F1-4E46-B7F4-61435E75303D}_is1"))
            {
                string folder = key?.GetValue("InstallLocation") as string ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XD Suite", "XDSuite");
                string target = Path.Combine(folder, "XDSuite.exe");
                if (File.Exists(target) && !target.Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    Process.Start(new ProcessStartInfo(target, string.Join(" ", args.Select(a => "\"" + a.Replace("\"", "\\\"") + "\""))) { UseShellExecute = true, WorkingDirectory = folder });
            }
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new UpdateWindow(Options.Parse(args))); }
        catch (Exception error) { MessageBox.Show(error.Message, "XD Suite Update", MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.ExitCode = 1; }
    }
    private static void CleanupForwarder(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            string flat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XDSuite", "XDSuite.exe");
            string perUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "XDSuite", "XDSuite.exe");
            if (!path.Equals(flat, StringComparison.OrdinalIgnoreCase) && !path.Equals(perUser, StringComparison.OrdinalIgnoreCase)) return;
            string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XD Suite", "XDSuite", "XDSuite.exe");
            byte[] own = File.ReadAllBytes(Application.ExecutablePath);
            for (int attempt = 0; attempt < 120; attempt++)
            {
                System.Threading.Thread.Sleep(1000);
                bool started = false;
                foreach (var process in Process.GetProcessesByName("XDSuite"))
                    using (process) { try { if (process.MainModule.FileName.Equals(target, StringComparison.OrdinalIgnoreCase)) started = true; } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { } }
                if (!started) continue;
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(own)) return;
                try
                {
                    File.Delete(path);
                    if (File.Exists(path + ".config") && File.Exists(Application.ExecutablePath + ".config") && File.ReadAllBytes(path + ".config").SequenceEqual(File.ReadAllBytes(Application.ExecutablePath + ".config"))) File.Delete(path + ".config");
                    string folder = Path.GetDirectoryName(path);
                    if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder, false);
                    return;
                }
                catch (IOException) { }
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

internal sealed class Options
{
    public int Parent;
    public string Setup = "", Target = "", Sha256 = "", Language = "german";
    public bool English => Language == "english";
    public string Executable => Path.Combine(Target, "XDSuite.exe");
    public string InstallTarget => Target.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "XDSuite"), StringComparison.OrdinalIgnoreCase) || Target.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XDSuite"), StringComparison.OrdinalIgnoreCase) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XD Suite", "XDSuite") : Target;
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || !new[] { "--parent", "--setup", "--target", "--sha256", "--language" }.Contains(args[i]) || values.ContainsKey(args[i])) throw new ArgumentException("Invalid update arguments.");
            values.Add(args[i], args[i + 1]);
        }
        var result = new Options { Parent = int.Parse(values["--parent"]), Setup = Path.GetFullPath(values["--setup"]), Target = Path.GetFullPath(values["--target"]).TrimEnd(Path.DirectorySeparatorChar), Sha256 = values["--sha256"], Language = values["--language"] };
        if (result.Parent <= 0 || !File.Exists(result.Setup) || !File.Exists(result.Executable) || !Regex.IsMatch(result.Sha256, "^[A-Fa-f0-9]{64}$") || result.Language != "german" && result.Language != "english") throw new ArgumentException("Invalid update arguments.");
        return result;
    }
}

internal sealed class UpdateWindow : Form
{
    private readonly Options options;
    private readonly Label status;
    private readonly string log;
    private readonly string setupLog;
    public UpdateWindow(Options options)
    {
        this.options = options;
        log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.log");
        setupLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup.log");
        Text = "XD Suite Update"; ClientSize = new Size(450, 115); FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen; ControlBox = false; Font = new Font("Segoe UI", 10);
        status = new Label { Text = T("XD Suite wird beendet …", "Waiting for XD Suite to exit …"), AutoSize = false, Location = new Point(22, 20), Size = new Size(406, 32) };
        Controls.Add(status);
        Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Location = new Point(22, 65), Size = new Size(406, 12) });
        Shown += Run;
    }
    private string T(string de, string en) => options.English ? en : de;
    private async void Run(object? sender, EventArgs args)
    {
        bool installed = false;
        try
        {
            await Task.Run(WaitForParent);
            status.Text = T("Setup wird geprüft …", "Verifying installer …");
            await Task.Run(Verify);
            status.Text = T("Update wird installiert …", "Installing update …");
            int exitCode = await Task.Run(Install);
            if (exitCode != 0 && exitCode != 3010)
            {
                string detail = "";
                if (File.Exists(setupLog))
                {
                    string? line = File.ReadLines(setupLog).LastOrDefault(l => l.Contains("PrepareToInstall failed:"));
                    if (line != null) detail = "\n\n" + line.Substring(line.IndexOf("PrepareToInstall failed:", StringComparison.Ordinal) + 24).Trim();
                }
                throw new IOException(T("Das Setup wurde mit Fehlercode ", "Installer exited with error code ") + exitCode + "." + detail + T("\n\nSetup-Protokoll: ", "\n\nSetup log: ") + setupLog);
            }
            installed = true;
            if (exitCode == 3010) MessageBox.Show(T("Das Update ist installiert. Windows muss neu gestartet werden.", "The update is installed. Windows needs to be restarted."), Text);
            status.Text = T("XD Suite wird gestartet …", "Starting XD Suite …");
            Restart(exitCode == 3010 ? "--skip-update-check" : "--update-completed");
        }
        catch (Exception error)
        {
            File.AppendAllText(log, DateTimeOffset.Now.ToString("O") + "\n" + error + "\n");
            MessageBox.Show(T("Das Update konnte nicht abgeschlossen werden.\n\n", "The update could not be completed.\n\n") + error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            if (!installed) { try { Restart("--skip-update-check"); } catch (Exception restartError) { File.AppendAllText(log, restartError.ToString()); } }
        }
        finally { Close(); }
    }
    private void WaitForParent()
    {
        Process parent;
        try { parent = Process.GetProcessById(options.Parent); } catch (ArgumentException) { return; }
        using (parent)
        {
            if (!parent.HasExited && !Path.GetFullPath(parent.MainModule.FileName).Equals(options.Executable, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected parent process.");
            if (!parent.WaitForExit(60000)) throw new IOException(T("XD Suite ist noch geöffnet. Das Update wurde nicht gestartet.", "XD Suite is still open. The update was not started."));
        }
    }
    private void Verify()
    {
        using (var stream = new FileStream(options.Setup, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var hash = SHA256.Create())
            if (!BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").Equals(options.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(T("Die Setup-Prüfsumme stimmt nicht. Die Datei wird nicht ausgeführt.", "The installer checksum does not match. The file will not be run."));
    }
    private int Install()
    {
        // Update the current directory through the SAME installer and AppId. No uninstaller is invoked.
        var start = new ProcessStartInfo(options.Setup)
        {
            UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetTempPath(),
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /COMPONENTS=suite /LANG=" + options.Language + " /DIR=" + Quote(options.InstallTarget) + " /LOG=" + Quote(setupLog)
        };
        File.AppendAllText(log, "Starting update setup: " + options.Setup + "\nTarget: " + options.InstallTarget + "\n");
        using (var installer = Process.Start(start) ?? throw new IOException("Installer could not be started.")) { installer.WaitForExit(); return installer.ExitCode; }
    }
    private void Restart(string argument)
    {
        string target = Path.Combine(options.InstallTarget, "XDSuite.exe");
        if (!File.Exists(target)) target = options.Executable;
        if (!File.Exists(target)) throw new FileNotFoundException("XD Suite executable not found.");
        Process.Start(new ProcessStartInfo(target, argument) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
