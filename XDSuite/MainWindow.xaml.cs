using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using XD.Common;

namespace XDSuite;

public partial class MainWindow : Window
{
    private Catalog catalog = new(1, []);
    private readonly PackageTransport transport = new();
    private string filter = "all";
    private bool busy, installing;
    private (LocalText Title, LocalText Detail)? operationStatus;
    private Func<ProcessStartInfo, Task<int>>? uninstallRunner;
    private CancellationTokenSource? cancellation;
    private readonly CancellationTokenSource lifetime = new();
    private AppCard? Selected => AppList.SelectedItem as AppCard;
    private Catalog betaCatalog = new(1, []);
    private bool betaFetched;
    private readonly bool localPreview;
    private string BetaCachePath => Path.Combine(SuiteSettings.DataDirectory, "beta-catalog.json");

    public MainWindow(bool preview = false, bool skipUpdateCheck = false, bool updateCompleted = false)
    {
        localPreview = preview;
        InitializeComponent();
        L10n.Attach(this);
        L10n.Changed += LanguageChanged;
        Closed += (_, _) => { lifetime.Cancel(); L10n.Changed -= LanguageChanged; transport.Dispose(); lifetime.Dispose(); };
        AllNav.IsChecked = true;
        ReadLocalCatalog(preview);
        if (!preview)
        {
            try { betaCatalog = ReadBetaCache(File.ReadAllText(BetaCachePath)); }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { App.Log(e); }
        }
        LanguageChanged(null, EventArgs.Empty);
        Loaded += async (_, _) =>
        {
            if (preview) return;
            await RefreshCatalog();
            if (updateCompleted) { StatusTitle.Text = "Update abgeschlossen."; StatusText.Text = "XD Suite " + SuiteUpdates.VersionText; }
            if (!skipUpdateCheck && IsVisible) await CheckSuiteUpdate();
        };
    }
    internal MainWindow(Catalog testCatalog, Func<ProcessStartInfo, Task<int>> runner) : this(preview: true)
    {
        catalog = testCatalog;
        uninstallRunner = runner;
        Rebuild();
    }
    private async Task CheckSuiteUpdate()
    {
        try
        {
            var update = await SuiteUpdates.Check(transport, SuiteSettings.Repository, SuiteUpdates.CurrentVersion, lifetime.Token);
            if (update == null || !IsVisible || busy) return;
            string message = $"Für XD Suite ist Version {update.Package.Version} verfügbar.\nInstallierte Version: {SuiteUpdates.VersionText}\n\nJetzt herunterladen und installieren?\n\nDie vorhandene Installation wird aktualisiert. Einstellungen und installierte Programme bleiben erhalten.";
            if (LocalizedDialog.Show(this, message, "Update verfügbar", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            SetBusy(true);
            ProgressBar.Visibility = Visibility.Visible; ProgressBar.IsIndeterminate = false; ProgressBar.Value = 0;
            StatusTitle.Text = "Suite-Update wird heruntergeladen …";
            try
            {
                var progress = new Progress<DownloadProgress>(p => { ProgressBar.Value = p.Percent; StatusText.Text = p.Message; });
                string installer = await transport.Download(update.Package, Path.Combine(SuiteSettings.DataDirectory, "Downloads"), progress, cancellation!.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                SuiteUpdates.StartInstallerAfterExit(installer, update.Package);
                SetBusy(false);
                Application.Current.Shutdown();
            }
            catch (OperationCanceledException) { StatusTitle.Text = "Download abgebrochen."; StatusText.Text = "Es wurden keine Änderungen vorgenommen."; }
            catch (Exception error) { ShowError(error); }
            finally { if (busy) SetBusy(false); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { App.Log(error); }
    }
    private string CachePath => Path.Combine(SuiteSettings.DataDirectory, "catalog-" + SuiteSettings.Repository.Replace('/', '-') + ".json");
    private void ReadLocalCatalog(bool preview = false)
    {
        var bundled = PackageValidation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));
        try
        {
            catalog = preview ? bundled : PackageValidation.Parse(File.ReadAllText(CachePath));
            // A cache from an older app must not replace the newer bundled packages when offline.
            catalog = new(1, catalog.Apps.Select(cached => bundled.Apps.FirstOrDefault(p => p.Id == cached.Id && Version.Parse(p.Version) > Version.Parse(cached.Version)) ?? cached).Concat(bundled.Apps.Where(p => catalog.Apps.All(c => c.Id != p.Id))).ToList());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { catalog = bundled; }
        Rebuild();
    }
    private void Rebuild()
    {
        string? previous = Selected?.Package.Id;
        string search = SearchBox.Text.Trim();
        var source = filter == "beta" ? betaCatalog.Apps : filter == "installed"
            ? catalog.Apps.Concat(betaCatalog.Apps).Concat(InstallationDiscovery.FindInstalledBetas()).DistinctBy(p => p.RegistryId, StringComparer.OrdinalIgnoreCase)
            : catalog.Apps.AsEnumerable();
        var cards = source.Select(p => new AppCard(p, InstallationDiscovery.Find(p))).Where(c => filter != "installed" || c.Installed != null).Where(c => filter != "updates" || c.HasUpdate).Where(c => search.Length == 0 || (c.Name + " " + c.Description + " " + c.Category).Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
        AppList.ItemsSource = cards;
        AppList.SelectedItem = cards.FirstOrDefault(c => c.Package.Id == previous) ?? cards.FirstOrDefault();
        EmptyText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"{cards.Count} " + (cards.Count == 1 ? "Programm" : "Programme");
        ListHeading.Text = filter == "beta" ? BetaText("Beta-Releases", "Beta releases") : L10n.T(filter switch { "installed" => "Installierte Programme", "updates" => "Verfügbare Updates", _ => "Alle Programme" });
        BetaNav.Content = BetaText("Beta-Releases", "Beta releases");
        BetaBanner.Visibility = filter == "beta" ? Visibility.Visible : Visibility.Collapsed;
        BetaNotice.Text = BetaText("Vorläufige Branch-Versionen zum manuellen Testen. Beta-Programme werden getrennt installiert und ersetzen keine stabilen Programme.", "Preliminary branch builds for manual testing. Beta applications install separately and do not replace stable applications.");
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        var selected = Selected;
        DetailPanel.DataContext = selected;
        DetailPanel.Visibility = selected == null ? Visibility.Hidden : Visibility.Visible;
        ActionButton.Content = selected?.Action;
        ActionButton.IsEnabled = !busy && selected != null;
        LaunchButton.Visibility = selected?.HasUpdate == true ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.IsEnabled = !busy;
        UninstallButton.Content = SuiteText.Uninstall.ToString();
        UninstallButton.Visibility = selected?.Installed != null ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.IsEnabled = !busy && selected?.Installed?.Uninstaller != null;
        UninstallButton.ToolTip = selected?.Installed?.Uninstaller == null ? SuiteText.NoUninstaller.ToString() : null;
        PackageSizeText.Text = selected == null || selected.Installed != null && !selected.HasUpdate ? "" : "Setup · " + selected.Size;
    }
    private async void Filter_Checked(object sender, RoutedEventArgs e) { if (AppList == null) return; filter = (string)((RadioButton)sender).Tag; Rebuild(); if (filter == "beta" && !betaFetched && !localPreview) await RefreshBeta(); }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (AppList != null) Rebuild(); }
    private void App_Selected(object sender, SelectionChangedEventArgs e) => RefreshSelection();
    private async void Refresh_Click(object sender, RoutedEventArgs e) { if (filter == "beta") await RefreshBeta(); else await RefreshCatalog(); }
    private static string BetaText(string de, string en) => new LocalText(de, en).ToString();
    private static Catalog ReadBetaCache(string json)
    {
        var cached = PackageValidation.Parse(json);
        foreach (var app in cached.Apps)
        {
            var parts = new Uri(app.InstallerUrl).AbsolutePath.Split('/');
            if (parts.Length != 7) throw new InvalidDataException("Invalid beta cache.");
            BetaReleases.Parse(JsonSerializer.Serialize(new Catalog(1, [app]), PackageValidation.JsonOptions), SuiteSettings.Repository, Uri.UnescapeDataString(parts[5]));
        }
        return cached;
    }
    internal void ShowBetaPreview(Catalog packages) { betaCatalog = packages; betaFetched = true; BetaNav.IsChecked = true; }
    private async Task RefreshBeta()
    {
        if (busy) return; SetBusy(true);
        SetOperationStatus(new("Beta-Releases werden geladen …", "Loading beta releases …"), new("GitHub-Vorabversionen werden geprüft.", "Checking GitHub prereleases."));
        try
        {
            var updated = await BetaReleases.Fetch(transport, SuiteSettings.Repository, cancellation!.Token);
            Directory.CreateDirectory(SuiteSettings.DataDirectory); string temporary = BetaCachePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, PackageValidation.JsonOptions)); File.Move(temporary, BetaCachePath, true);
            betaCatalog = updated; betaFetched = true;
            SetOperationStatus(new("Beta-Releases aktualisiert.", "Beta releases updated."), updated.Apps.Count == 0 ? new("Noch keine veröffentlichten Beta-Programme verfügbar.", "No published beta applications available yet.") : new("Programm zum Testen auswählen.", "Select an application to test."));
        }
        catch (OperationCanceledException) { SetOperationStatus(new("Aktualisierung abgebrochen.", "Refresh cancelled."), new("Der bisherige Beta-Katalog bleibt verfügbar.", "The previous beta catalog remains available.")); }
        catch (Exception e) { App.Log(e); SetOperationStatus(new("Beta-Quelle nicht erreichbar.", "Beta source unavailable."), new("Gespeicherte Beta-Programme bleiben verfügbar. Erneut aktualisieren, um neue Versionen zu laden.", "Cached beta applications remain available. Refresh again to load new versions.")); }
        finally { SetBusy(false); Rebuild(); }
    }
    private async Task RefreshCatalog()
    {
        if (busy) return;
        SetBusy(true);
        ProgressBar.Visibility = Visibility.Collapsed;
        StatusTitle.Text = "Programmkatalog wird aktualisiert …";
        StatusText.Text = "Verbindung zu GitHub Releases wird hergestellt.";
        try
        {
            var updated = await transport.FetchLatestCatalog(SuiteSettings.Repository, cancellation!.Token);
            Directory.CreateDirectory(SuiteSettings.DataDirectory);
            string temporary = CachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(updated, PackageValidation.JsonOptions)); File.Move(temporary, CachePath, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            catalog = updated;
            StatusTitle.Text = "Programmkatalog aktualisiert.";
            StatusText.Text = "Programmkatalog von GitHub Releases geladen.";
        }
        catch (OperationCanceledException) when (cancellation!.IsCancellationRequested) { StatusTitle.Text = "Aktualisierung abgebrochen."; StatusText.Text = "Der bisherige Programmkatalog bleibt verfügbar."; }
        catch (Exception ex)
        {
            App.Log(ex);
            StatusTitle.Text = "Gespeicherter Programmkatalog verfügbar.";
            StatusText.Text = "Die Onlinequelle ist gerade nicht erreichbar. Vorhandene Programme kannst du weiterhin starten.";
        }
        finally { SetBusy(false); Rebuild(); }
    }
    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (busy || Selected is not { } selected) return;
        if (selected.Installed != null && !selected.HasUpdate) { Launch(selected); return; }
        string question = $"{selected.Name} {selected.Package.Version} herunterladen und installieren?\n\nDas Programm kann danach auch ohne XD Suite gestartet werden.";
        if (LocalizedDialog.Show(this, question, "Programm installieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        StatusTitle.Text = "Download wird vorbereitet …";
        StatusText.Text = "Das Setup wird heruntergeladen und geprüft.";
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 0;
        try
        {
            var progress = new Progress<DownloadProgress>(p => { ProgressBar.Value = p.Percent; StatusText.Text = p.Message + $" {p.Bytes / 1048576d:0.0} / {p.Total / 1048576d:0.0} MB"; });
            string setup = await transport.Download(selected.Package, Path.Combine(SuiteSettings.DataDirectory, "Downloads"), progress, cancellation!.Token);
            await PackageInstaller.Install(setup, selected.Package, () =>
            {
                installing = true; CancelButton.IsEnabled = false; ProgressBar.IsIndeterminate = true;
                StatusTitle.Text = "Programm wird installiert …";
                StatusText.Text = "Bitte warten, bis das Setup fertig ist.";
            }, cancellation.Token);
            StatusTitle.Text = "Installation abgeschlossen.";
            StatusText.Text = "Das Programm kann über die Suite oder das Startmenü geöffnet werden.";
            ProgressBar.IsIndeterminate = false; ProgressBar.Value = 100;
        }
        catch (OperationCanceledException) { StatusTitle.Text = "Download abgebrochen."; StatusText.Text = "Es wurde kein Programm installiert."; ProgressBar.Visibility = Visibility.Collapsed; }
        catch (Exception ex) { ShowError(ex); ProgressBar.Visibility = Visibility.Collapsed; }
        finally { installing = false; SetBusy(false); Rebuild(); }
    }
    private void Launch_Click(object sender, RoutedEventArgs e) { if (Selected is { } selected) Launch(selected); }
    private async void Uninstall_Click(object sender, RoutedEventArgs e) => await UninstallSelected();
    internal async Task UninstallSelected()
    {
        if (busy || Selected is not { Installed.Uninstaller: not null } selected) return;
        if (LocalizedDialog.Show(this, SuiteText.Confirm(selected.Package).ToString(), SuiteText.UninstallTitle.ToString(),
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        installing = true;
        CancelButton.Visibility = Visibility.Collapsed;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        SetOperationStatus(SuiteText.Uninstalling, SuiteText.FinishUninstall);
        try
        {
            bool removed = await PackageUninstaller.Uninstall(selected.Package, selected.Installed, cancellation!.Token, uninstallRunner);
            SetOperationStatus(removed ? SuiteText.Completed : SuiteText.Incomplete,
                removed ? selected.Package.Name : InstallationDiscovery.Find(selected.Package) != null ? SuiteText.StillInstalled : SuiteText.CheckInstallation);
        }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            installing = false;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
            SetBusy(false);
            Rebuild();
        }
    }
    private void SetOperationStatus(LocalText title, LocalText detail)
    {
        operationStatus = (title, detail);
        StatusTitle.Text = title.ToString(); StatusText.Text = detail.ToString();
    }
    private void Launch(AppCard card)
    {
        operationStatus = null;
        try
        {
            var installed = InstallationDiscovery.Find(card.Package) ?? throw new FileNotFoundException("Das Programm wurde nicht gefunden. Bitte neu installieren.");
            using var launched = InstallationDiscovery.Launch(installed);
            StatusTitle.Text = "Programm gestartet."; StatusText.Text = card.Name;
        }
        catch (Exception ex) { ShowError(ex); Rebuild(); }
    }
    private void SetBusy(bool value)
    {
        busy = value;
        if (value) { operationStatus = null; cancellation = new CancellationTokenSource(); }
        else { cancellation?.Dispose(); cancellation = null; }
        RefreshButton.IsEnabled = !value;
        AllNav.IsEnabled = InstalledNav.IsEnabled = UpdatesNav.IsEnabled = BetaNav.IsEnabled = !value;
        CancelButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = !installing;
        RefreshSelection();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!installing) { cancellation?.Cancel(); CancelButton.IsEnabled = false; } }
    private void German_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.German);
    private void English_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.English);
    private void SetLanguage(UiLanguage language) { try { L10n.Set(language); } catch (Exception ex) { ShowError(ex); } }
    private void LanguageChanged(object? sender, EventArgs e)
    {
        GermanMenu.IsChecked = L10n.Current == UiLanguage.German; EnglishMenu.IsChecked = L10n.Current == UiLanguage.English;
        Rebuild();
        if (operationStatus is { } status) SetOperationStatus(status.Title, status.Detail);
    }
    private void ShowError(Exception ex)
    {
        operationStatus = null;
        App.Log(ex); StatusTitle.Text = "Vorgang fehlgeschlagen.";
        StatusText.Text = ex is HttpRequestException ? "Der Download ist gerade nicht erreichbar. Bitte die Internetverbindung prüfen." : ex.Message;
        LocalizedDialog.Show(this, StatusText.Text, "XD Suite");
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true; LocalizedDialog.Show(this, installing ? SuiteText.FinishSetup.ToString() : "Bitte den Download oder die Aktualisierung zuerst beenden oder abbrechen.", "XD Suite");
    }
}
