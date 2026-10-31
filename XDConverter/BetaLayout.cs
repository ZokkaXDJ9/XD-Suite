using System.Windows;
using System.Windows.Controls;
using XD.Design;
namespace XDConverter;
public partial class MainWindow
{
    void SetupBetaLayout()
    {
        var body = (Grid)Content;
        ((FrameworkElement)body.Children[0]).Visibility = Visibility.Collapsed;
        body.Margin = new Thickness(25, 0, 25, 20);
        BetaChrome.Attach(this, () => T.English);
        var file = BetaChrome.Group(this, "_Datei", "_File");
        foreach (var name in new[] { "AddButton", "FolderButton", "ClearButton", "OpenButton" })
            if (FindName(name) is Button button) BetaChrome.Button(this, file, button);
        BetaChrome.Add(this, file, "Beenden", "Exit", Close);
        var convert = BetaChrome.Group(this, "_Konvertieren", "_Convert");
        BetaChrome.Button(this, convert, StartButton);
        var help = BetaChrome.Group(this, "_Hilfe", "_Help");
        BetaChrome.Add(this, help, "Hilfe / Über XD Konverter", "Help / About XD Converter", () => Help_Click(this, new RoutedEventArgs()));
        BetaChrome.MoveLanguages(this, GermanMenu);
    }
}
