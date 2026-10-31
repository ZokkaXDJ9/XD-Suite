using System.Windows;
using System.Windows.Controls;
using XD.Design;
namespace XDBurner;
public partial class MainWindow
{
    void SetupBetaLayout()
    {
        var body = (Grid)Content;
        ((FrameworkElement)body.Children[0]).Visibility = Visibility.Collapsed;
        body.ColumnDefinitions[0].Width = new GridLength(0);
        BetaChrome.Attach(this, () => L10n.Current == UiLanguage.English);
        var file = BetaChrome.Group(this, "_Datei", "_File");
        BetaChrome.Button(this, file, "Dateien hinzufügen...", "Add files...", AddFilesButton);
        BetaChrome.Button(this, file, "Ordner hinzufügen...", "Add folder...", AddFolderButton);
        BetaChrome.Button(this, file, "Liste leeren", "Clear list", ClearButton);
        BetaChrome.Add(this, file, "Beenden", "Exit", Close);
        var project = BetaChrome.Group(this, "_Projekt", "_Project");
        BetaChrome.Add(this, project, "Daten-CD", "Data CD", () => DataNav.IsChecked = true, Editor);
        BetaChrome.Add(this, project, "Audio-CD", "Audio CD", () => AudioNav.IsChecked = true, Editor);
        BetaChrome.Add(this, project, "ISO brennen", "Burn ISO", () => IsoNav.IsChecked = true, Editor);
        BetaChrome.Add(this, project, "CD-RW löschen", "Erase CD-RW", () => EraseNav.IsChecked = true, Editor);
        var help = BetaChrome.Group(this, "_Hilfe", "_Help");
        BetaChrome.Add(this, help, "Hilfe / Über XD Brenner", "Help / About XD Burner", () => Help_Click(this, new RoutedEventArgs()));
        BetaChrome.MoveLanguages(this, GermanMenu);
    }
}
