using System.ComponentModel;
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
    private readonly SuiteSettings settings = SuiteSettings.Load();
    private readonly PackageTransport transport = new();
    private string filter = "all";
    private bool busy, installing;
    private CancellationTokenSource? cancellation;
    private AppCard? Selected => AppList.SelectedItem as AppCard;

    public MainWindow(bool preview = false)
    {
        InitializeComponent();
        L10n.Attach(this);
        L10n.Changed += LanguageChanged;
        Closed += (_, _) => { L10n.Changed -= LanguageChanged; transport.Dispose(); };
        AllNav.IsChecked = true;
        ReadLocalCatalog();
        LanguageChanged(null, EventArgs.Empty);
        Loaded += async (_, _) => { if (!preview) await RefreshCatalog(); };
    }
    private string CachePath => Path.Combine(SuiteSettings.DataDirectory, "catalog-" + settings.Repository.Replace('/', '-') + ".json");
    private void ReadLocalCatalog()
    {
        try { catalog = PackageValidation.Parse(File.ReadAllText(CachePath)); }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { catalog = PackageValidation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json"))); }
        Rebuild();
    }
    private void Rebuild()
    {
        string? previous = Selected?.Package.Id;
        string search = SearchBox.Text.Trim();
        var cards = catalog.Apps.Select(p => new AppCard(p, InstallationDiscovery.Find(p))).Where(c => filter != "installed" || c.Installed != null).Where(c => filter != "updates" || c.HasUpdate).Where(c => search.Length == 0 || (c.Name + " " + c.Description + " " + c.Category).Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
        AppList.ItemsSource = cards;
        AppList.SelectedItem = cards.FirstOrDefault(c => c.Package.Id == previous) ?? cards.FirstOrDefault();
        EmptyText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"{cards.Count} " + (cards.Count == 1 ? "Programm" : "Programme");
        ListHeading.Text = filter switch { "installed" => "Installierte Programme", "updates" => "Verfügbare Updates", _ => "Alle Programme" };
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
        PackageSizeText.Text = selected == null || selected.Installed != null && !selected.HasUpdate ? "" : "Setup · " + selected.Size;
    }
    private void Filter_Checked(object sender, RoutedEventArgs e) { if (AppList == null) return; filter = (string)((RadioButton)sender).Tag; Rebuild(); }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (AppList != null) Rebuild(); }
    private void App_Selected(object sender, SelectionChangedEventArgs e) => RefreshSelection();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCatalog();
    private async Task RefreshCatalog()
    {
        if (busy) return;
        SetBusy(true);
        ProgressBar.Visibility = Visibility.Collapsed;
        StatusTitle.Text = "Programmkatalog wird aktualisiert …";
        StatusText.Text = "Verbindung zu GitHub Releases wird hergestellt.";
        try
        {
            var updated = await transport.FetchCatalog(settings.CatalogUri, cancellation!.Token);
            Directory.CreateDirectory(SuiteSettings.DataDirectory);
            string temporary = CachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(updated, PackageValidation.JsonOptions)); File.Move(temporary, CachePath, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            catalog = updated;
            StatusTitle.Text = "Deine Programme sind auf dem neuesten Stand.";
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
        string question = $"{selected.Name} {selected.Package.Version} herunterladen und installieren?\n\nDownloadquelle: GitHub Releases\nDas Programm kann danach auch ohne XD Suite gestartet werden.";
        if (LocalizedDialog.Show(this, question, "Programm installieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        StatusTitle.Text = "Dein Programm wird vorbereitet …";
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
                StatusTitle.Text = "Das Programm wird installiert …";
                StatusText.Text = "Bitte warten, bis das Setup fertig ist.";
            }, cancellation.Token);
            StatusTitle.Text = "Dein Programm ist bereit.";
            StatusText.Text = "Du kannst es hier oder direkt über das Startmenü öffnen.";
            ProgressBar.IsIndeterminate = false; ProgressBar.Value = 100;
        }
        catch (OperationCanceledException) { StatusTitle.Text = "Download abgebrochen."; StatusText.Text = "Es wurde kein Programm installiert."; ProgressBar.Visibility = Visibility.Collapsed; }
        catch (Exception ex) { ShowError(ex); ProgressBar.Visibility = Visibility.Collapsed; }
        finally { installing = false; SetBusy(false); Rebuild(); }
    }
    private void Launch_Click(object sender, RoutedEventArgs e) { if (Selected is { } selected) Launch(selected); }
    private void Launch(AppCard card)
    {
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
        if (value) cancellation = new CancellationTokenSource();
        else { cancellation?.Dispose(); cancellation = null; }
        RefreshButton.IsEnabled = !value;
        CancelButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = !installing;
        RefreshSelection();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!installing) { cancellation?.Cancel(); CancelButton.IsEnabled = false; } }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var dialog = new Window { Title = L10n.T("Downloadquelle"), Width = 480, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = FontFamily, FontSize = 14, Background = Background };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = L10n.T("GitHub-Repository für den Programmkatalog"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var input = new TextBox { Text = settings.Repository, Padding = new Thickness(10) };
        System.Windows.Automation.AutomationProperties.SetName(input, "GitHub Repository");
        panel.Children.Add(input);
        panel.Children.Add(new TextBlock { Text = L10n.T("Format: Besitzer/Repository. Ein öffentliches Release muss catalog.json und die einzelnen Setups enthalten."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 18), FontSize = 12 });
        var save = new Button { Content = L10n.T("Speichern"), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) =>
        {
            try { SuiteSettings.ValidateRepository(input.Text.Trim()); settings.Repository = input.Text.Trim(); settings.Save(); ReadLocalCatalog(); dialog.DialogResult = true; }
            catch (Exception ex) { LocalizedDialog.Show(dialog, ex.Message, "Downloadquelle"); }
        };
        panel.Children.Add(save); dialog.Content = panel;
        if (dialog.ShowDialog() == true) _ = RefreshCatalog();
    }
    private void German_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.German);
    private void English_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.English);
    private void SetLanguage(UiLanguage language) { try { L10n.Set(language); } catch (Exception ex) { ShowError(ex); } }
    private void LanguageChanged(object? sender, EventArgs e)
    {
        GermanMenu.IsChecked = L10n.Current == UiLanguage.German; EnglishMenu.IsChecked = L10n.Current == UiLanguage.English;
        Rebuild();
    }
    private void ShowError(Exception ex)
    {
        App.Log(ex); StatusTitle.Text = "Das hat noch nicht geklappt.";
        StatusText.Text = ex is HttpRequestException ? "Der Download ist gerade nicht erreichbar. Bitte die Internetverbindung und die Downloadquelle prüfen." : ex.Message;
        LocalizedDialog.Show(this, StatusText.Text, "XD Suite");
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true; LocalizedDialog.Show(this, installing ? "Bitte die laufende Installation abschließen lassen." : "Bitte den Download oder die Aktualisierung zuerst beenden oder abbrechen.", "XD Suite");
    }
}
