using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace XDSuite;

internal static class PackageUninstaller
{
    // Execute only the registered Inno uninstaller beside the discovered application.
    // Never pass a registry command through a shell or guess an uninstaller for a portable copy.
    internal static string? Resolve(string? command, string executable)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        string path = command.Trim();
        if (path.StartsWith('"'))
        {
            if (path.Length < 3 || !path.EndsWith('"')) return null;
            path = path[1..^1];
        }
        try
        {
            if (!Path.IsPathFullyQualified(path) || path.Contains('"') ||
                !Regex.IsMatch(Path.GetFileName(path), @"^unins\d{3}\.exe$", RegexOptions.IgnoreCase)) return null;
            path = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(Path.GetFullPath(executable)), StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(path) ? path : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    internal static async Task<bool> Uninstall(AppPackage package, InstalledApp confirmed,
        CancellationToken token, Func<ProcessStartInfo, Task<int>>? run = null)
    {
        token.ThrowIfCancellationRequested();
        var current = InstallationDiscovery.Find(package);
        if (current?.Uninstaller == null ||
            !string.Equals(current.Executable, confirmed.Executable, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.Uninstaller, confirmed.Uninstaller, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(SuiteText.InstallationChanged.ToString());

        // Keep the working directory outside the program folder so it can be removed.
        var start = new ProcessStartInfo(current.Uninstaller) { UseShellExecute = true, WorkingDirectory = Path.GetTempPath() };
        start.ArgumentList.Add("/NORESTART");
        int exitCode;
        try { exitCode = await (run ?? RunProcess)(start); }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { return false; }
        // Do not kill a running uninstaller, even if the suite's token is cancelled.
        // Inno does not promise specific nonzero exit codes for user cancellation.
        return exitCode == 0 && InstallationDiscovery.Find(package) == null;
    }

    internal static async Task<int> RunProcess(ProcessStartInfo start)
    {
        using var process = Process.Start(start) ?? throw new IOException(SuiteText.CouldNotStart.ToString());
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}

internal static class SuiteText
{
    internal static readonly LocalText Uninstall = new("Deinstallieren", "Uninstall");
    internal static readonly LocalText UninstallTitle = new("Programm deinstallieren", "Uninstall program");
    internal static LocalText Confirm(AppPackage app) => new($"{app.Name.De} deinstallieren?", $"Uninstall {app.Name.En}?");
    internal static readonly LocalText NoUninstaller = new("Kein gültiger Deinstaller registriert. Bitte die Windows-Einstellungen prüfen.", "No valid uninstaller is registered. Please check Windows Settings.");
    internal static readonly LocalText Uninstalling = new("Programm wird deinstalliert …", "Uninstalling program …");
    internal static readonly LocalText FinishUninstall = new("Bitte die Deinstallation im geöffneten Fenster abschließen.", "Please complete the uninstallation in the window that opened.");
    internal static readonly LocalText Completed = new("Deinstallation abgeschlossen.", "Uninstallation completed.");
    internal static readonly LocalText Incomplete = new("Deinstallation nicht abgeschlossen.", "Uninstallation not completed.");
    internal static readonly LocalText StillInstalled = new("Das Programm ist weiterhin installiert.", "The program is still installed.");
    internal static readonly LocalText CheckInstallation = new("Bitte den Installationsstatus in den Windows-Einstellungen prüfen.", "Please check the installation status in Windows Settings.");
    internal static readonly LocalText InstallationChanged = new("Die Installation hat sich geändert. Bitte die Programmliste aktualisieren und erneut versuchen.", "The installation has changed. Please refresh the program list and try again.");
    internal static readonly LocalText CouldNotStart = new("Der Deinstaller konnte nicht gestartet werden.", "The uninstaller could not be started.");
    internal static readonly LocalText FinishSetup = new("Bitte die laufende Installation oder Deinstallation abschließen lassen.", "Please let the current installation or uninstallation finish.");
}
