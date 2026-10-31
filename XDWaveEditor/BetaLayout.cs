using System.Linq;
using System.Windows;
using System.Windows.Controls;
using XD.Design;
namespace XDWaveEditor;
public partial class MainWindow
{
    void SetupBetaLayout()
    {
        var body = (Grid)Content;
        var workspace = body.Children.OfType<Grid>().First();
        var header = workspace.Children.OfType<DockPanel>().First();
        header.Visibility = Visibility.Collapsed;
        var editor = workspace.Children.OfType<Grid>().First(g => Grid.GetRow(g) == 2);
        editor.Children.OfType<Border>().First().Visibility = Visibility.Collapsed;
        editor.ColumnDefinitions[1].Width = editor.ColumnDefinitions[2].Width = new GridLength(0);
        workspace.Margin = new Thickness(25, 0, 25, 18);
        BetaChrome.Attach(this, () => T.English);
        BetaChrome.MoveLanguages(this, GermanMenu);
        foreach (var button in new[] { OpenButton, ExportButton, UndoButton, RedoButton, ToolsButton }) BetaChrome.Toolbar(this, button);
    }
}
