using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace XD.Common;

public static class LocalizationChecks
{
    public static void Run()
    {
        var previous = L10n.Current;
        try
        {
            L10n.Set(UiLanguage.German, false);
            var text = new TextBlock { Text = "Daten-CD" };
            var status = new TextBlock { Text = "Noch keine Dateien" };
            var button = new Button { Content = "CD brennen" };
            var fileName = new TextBlock();
            fileName.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = new { Value = "Ordner" } });
            var userInput = new TextBox { Text = "Ordner" };
            var panel = new StackPanel(); foreach (var child in new UIElement[] { text, status, button, fileName, userInput }) panel.Children.Add(child);
            var window = new Window { Content = panel, Title = "XD Brenner" };
            using var translation = L10n.Attach(window);
            L10n.Set(UiLanguage.English, false);
            Check(text.Text == "Data CD" && (string)button.Content == "Burn CD", "Static text and button translation");
            Check(window.Title == "XD Burner", "Window title translation");
            Check(fileName.Text == "Ordner" && userInput.Text == "Ordner", "Filenames and user input stay unchanged");
            status.Text = "Titel 2 von 7 wird vorbereitet: Mein Lied.mp3";
            Check(status.Text == "Preparing track 2 of 7: Mein Lied.mp3", "Interpolated live progress translation");
            L10n.Set(UiLanguage.German, false);
            Check(text.Text == "Daten-CD" && (string)button.Content == "CD brennen", "Switching back without restart");
            Check(status.Text == "Titel 2 von 7 wird vorbereitet: Mein Lied.mp3", "Dynamic status round-trip");
        }
        finally { L10n.Set(previous, false); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Localization: " + message); }
}
