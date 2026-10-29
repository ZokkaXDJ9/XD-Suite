using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XDWaveEditor;

internal sealed class SelectionDialog : Window
{
    private readonly AudioDocument document;
    internal TextBox StartEntry { get; } = new() { Padding = new(8), Margin = new(0, 6, 0, 14) };
    internal TextBox EndEntry { get; } = new() { Padding = new(8), Margin = new(0, 6, 0, 14) };
    internal Button ApplyButton { get; } = new();
    internal SelectionDialog(AudioDocument document)
    {
        this.document = document;
        Title = Texts.Current["PreciseSelection"]; Width = 450; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new(24) };
        root.Children.Add(new TextBlock { Text = Texts.Current["TimeHint"], Foreground = Brushes.SlateGray, Margin = new(0, 0, 0, 16) });
        root.Children.Add(new TextBlock { Text = Texts.Current["StartTime"] }); root.Children.Add(StartEntry);
        root.Children.Add(new TextBlock { Text = Texts.Current["EndTime"] }); root.Children.Add(EndEntry);
        StartEntry.Text = Texts.Time(document.Selection.Start, document.Source.Rate); EndEntry.Text = Texts.Time(document.Selection.End, document.Source.Rate);
        var error = new TextBlock { Foreground = Brushes.Firebrick, Margin = new(0, 0, 0, 12) }; root.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = Texts.Current["Cancel"], IsCancel = true }; cancel.Click += (_, _) => Close(); actions.Children.Add(cancel);
        ApplyButton.Content = Texts.Current["SetSelection"]; ApplyButton.IsDefault = true;
        ApplyButton.Click += (_, _) => { if (TryRange(out var range)) { document.Select(range.Start, range.End); Close(); } else error.Text = Texts.Current["InvalidTime"]; };
        actions.Children.Add(ApplyButton); root.Children.Add(actions); Content = root;
    }
    internal bool TryRange(out FrameRange range)
    {
        range = default;
        if (!TrySeconds(StartEntry.Text, out double start) || !TrySeconds(EndEntry.Text, out double end) || start < 0 || end < start || end > document.Seconds + .0005) return false;
        range = new(Math.Clamp((long)Math.Round(start * document.Source.Rate), 0, document.Frames), Math.Clamp((long)Math.Round(end * document.Source.Rate), 0, document.Frames)); return true;
    }
    private static bool TrySeconds(string text, out double seconds)
    {
        text = text.Trim().Replace(',', '.');
        if (text.Contains(':') && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time)) { seconds = time.TotalSeconds; return true; }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && double.IsFinite(seconds);
    }
}
