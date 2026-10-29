using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace XDWaveEditor;

public partial class MainWindow : Window
{
    private readonly AudioEngine engine;
    private readonly MediaPlayer player = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(35) };
    private AudioDocument? document;
    private CancellationTokenSource? operation;
    private bool busy, playing, mediaReady, updatingScroll;
    private string? previewPath;
    private long playbackEnd, pendingStart, previousFrames;
    private string statusKey = "Ready", statusDetail = "";
    private Texts T => Texts.Current;
    internal AudioDocument? Document => document;
    public MainWindow(AudioEngine engine)
    {
        this.engine = engine; InitializeComponent();
        Wave.ViewChanged += RefreshScroll;
        T.PropertyChanged += LanguageChanged;
        timer.Tick += (_, _) =>
        {
            if (!playing || !mediaReady || document == null) return;
            document.Cursor = Math.Clamp((long)(player.Position.TotalSeconds * document.Source.Rate), 0, document.Frames);
            if (document.Cursor >= playbackEnd) { StopPlayback(); document.Cursor = playbackEnd; }
            PositionText.Text = Texts.Time(document.Cursor, document.Source.Rate); Wave.InvalidateVisual();
        };
        player.MediaOpened += (_, _) => { mediaReady = true; if (playing && document != null) { player.Position = TimeSpan.FromSeconds(pendingStart / (double)document.Source.Rate); player.Play(); timer.Start(); } };
        player.MediaEnded += (_, _) => { StopPlayback(); RefreshInfo(); };
        player.MediaFailed += (_, _) => { StopPlayback(); SetStatus("PlaybackFailed"); RefreshInfo(); };
        Closed += (_, _) => { T.PropertyChanged -= LanguageChanged; InvalidatePlayback(); document?.Source.Dispose(); };
        RefreshLanguage(); RefreshInfo();
    }
    internal void LoadDocument(AudioDocument loaded)
    {
        InvalidatePlayback();
        if (document != null) { document.Edited -= DocumentEdited; document.SelectionChanged -= SelectionChanged; document.Source.Dispose(); }
        document = loaded; document.Edited += DocumentEdited; document.SelectionChanged += SelectionChanged;
        Wave.Document = loaded; previousFrames = loaded.Frames; SetStatus("Ready"); RefreshInfo();
    }
    private void DocumentEdited()
    {
        InvalidatePlayback();
        if (document != null)
        {
            bool fitted = Wave.VisibleFrames >= previousFrames;
            previousFrames = document.Frames;
            if (fitted) Wave.Fit(); else Wave.Scroll(Math.Min(Wave.ViewStart, Math.Max(0, document.Frames - Wave.VisibleFrames)));
        }
        SetStatus("Ready"); Wave.InvalidateVisual(); RefreshInfo();
    }
    private void SelectionChanged()
    {
        StopPlayback();
        RefreshInfo(); Wave.InvalidateVisual();
    }
    private void RefreshLanguage()
    {
        GermanMenu.IsChecked = !T.English; EnglishMenu.IsChecked = T.English;
        RefreshInfo(); StatusText.Text = T[statusKey] + statusDetail;
    }
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => RefreshLanguage();
    private void RefreshInfo()
    {
        bool loaded = document != null, audio = document?.Frames > 0, selection = document?.Selection.Length > 0;
        OpenButton.IsEnabled = !busy; ExportButton.IsEnabled = ExportMenu.IsEnabled = !busy && audio;
        PlayButton.IsEnabled = StopButton.IsEnabled = !busy && audio;
        PlayButton.Content = T[playing ? "Pause" : "Play"];
        SelectButton.IsEnabled = !busy && audio;
        DeleteButton.IsEnabled = TrimButton.IsEnabled = CutButton.IsEnabled = CutMenu.IsEnabled = CopyButton.IsEnabled = CopyMenu.IsEnabled = !busy && selection;
        PasteButton.IsEnabled = PasteMenu.IsEnabled = !busy && document?.CanPaste == true;
        UndoButton.IsEnabled = UndoMenu.IsEnabled = !busy && document?.CanUndo == true;
        RedoButton.IsEnabled = RedoMenu.IsEnabled = !busy && document?.CanRedo == true;
        Wave.IsEditingEnabled = !busy;
        EmptyPanel.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
        FileNameText.Text = loaded ? Path.GetFileName(document!.Original) : T["Empty"];
        FileInfoText.Text = loaded ? $"{T["Duration"]}: {Texts.Time(document!.Frames, document.Source.Rate)}  ·  {document.Source.Rate:N0} Hz  ·  {document.Source.Channels} {T["Channels"]}  ·  {T[document.IsDirty ? "Changed" : "Unchanged"]}" : "";
        SelectionText.Text = loaded ? $"{T["Selection"]}: {Texts.Time(document!.Selection.Start, document.Source.Rate)} — {Texts.Time(document.Selection.End, document.Source.Rate)}  ({document.Selection.Length / (double)document.Source.Rate:0.000} s)" : "";
        PositionText.Text = loaded ? Texts.Time(document!.Cursor, document.Source.Rate) : "00:00:00.000";
        ClipboardText.Text = document?.CanPaste == true ? string.Format(T.Culture, T["Clipboard"], document.ClipboardFrames / (double)document.Source.Rate) : T["ClipboardEmpty"];
        RefreshScroll();
    }
    private void RefreshScroll()
    {
        updatingScroll = true;
        WaveScroll.Maximum = Math.Max(0, (document?.Frames ?? 0) - Wave.VisibleFrames);
        WaveScroll.ViewportSize = Wave.VisibleFrames; WaveScroll.LargeChange = Wave.VisibleFrames * .8;
        WaveScroll.SmallChange = Wave.VisibleFrames * .05; WaveScroll.Value = Wave.ViewStart;
        WaveScroll.IsEnabled = WaveScroll.Maximum > 0;
        updatingScroll = false;
    }
    private void Scroll_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!updatingScroll && Wave != null) Wave.Scroll(e.NewValue); }
    private void SetStatus(string key, string detail = "") { statusKey = key; statusDetail = detail; StatusText.Text = T[key] + detail; }
    private async Task<bool> RunBusy(string status, Func<CancellationToken, Task> action)
    {
        if (busy) return false;
        busy = true; operation = new(); SetStatus(status);
        BusyBar.Visibility = CancelButton.Visibility = Visibility.Visible; RefreshInfo();
        try { await action(operation.Token); return true; }
        catch (OperationCanceledException) { SetStatus("Cancelled"); return false; }
        catch (Exception error) { SetStatus("Failed"); MessageBox.Show(this, T.Error(error), T["Title"], MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        finally { operation.Dispose(); operation = null; busy = false; BusyBar.Visibility = CancelButton.Visibility = Visibility.Collapsed; RefreshInfo(); }
    }
    private bool MayDiscard() => document?.IsDirty != true || MessageBox.Show(this, T["Unsaved"], T["Title"], MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    private async Task OpenFile(string path)
    {
        if (busy || !MayDiscard()) return;
        StopPlayback();
        await RunBusy("Loading", async token => { var loaded = await engine.Open(path, token); LoadDocument(loaded); });
    }
    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var dialog = new OpenFileDialog { Filter = "Audio|*.wav;*.wave;*.mp3;*.flac;*.ogg;*.m4a;*.aac;*.opus;*.wma;*.aif;*.aiff", Multiselect = false };
        if (dialog.ShowDialog(this) == true) await OpenFile(dialog.FileName);
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (busy || document == null || document.Frames == 0) return;
        StopPlayback();
        var dialog = new SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(document.Original) + "-edited", DefaultExt = ".wav", Filter = "WAV (24 Bit)|*.wav|MP3|*.mp3|FLAC (24 Bit)|*.flac|OGG Vorbis|*.ogg|M4A AAC|*.m4a|Opus|*.opus", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        if (await RunBusy("Exporting", token => engine.Export(document, dialog.FileName, true, token))) SetStatus("Saved", dialog.FileName);
    }
    private void Copy_Click(object sender, RoutedEventArgs e) { if (!busy) { document?.CopySelection(); RefreshInfo(); } }
    private void Cut_Click(object sender, RoutedEventArgs e) { if (!busy) document?.CutSelection(); }
    private void Paste_Click(object sender, RoutedEventArgs e) { if (!busy) document?.Paste(); }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (!busy) document?.DeleteSelection(); }
    private void Trim_Click(object sender, RoutedEventArgs e) { if (!busy) document?.TrimSelection(); }
    private void Undo_Click(object sender, RoutedEventArgs e) { if (!busy) document?.Undo(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (!busy) document?.Redo(); }
    private void SelectAll_Click(object sender, RoutedEventArgs e) { if (!busy && document != null) document.Select(0, document.Frames); }
    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (playing) { player.Pause(); timer.Stop(); playing = false; RefreshInfo(); return; }
        await PlayAudio();
    }
    internal async Task PlayAudio()
    {
        if (busy || document == null || document.Frames == 0) return;
        if (previewPath == null)
        {
            string cache = AudioEngine.NewCachePath(".wav");
            if (!await RunBusy("Preparing", token => Task.Run(() => AudioEngine.WritePreview(document, cache, token), token))) { if (File.Exists(cache)) File.Delete(cache); return; }
            previewPath = cache;
        }
        pendingStart = document.Cursor;
        if (document.Selection.Length > 0)
        {
            if (pendingStart < document.Selection.Start || pendingStart >= document.Selection.End) pendingStart = document.Selection.Start;
            playbackEnd = document.Selection.End;
        }
        else { if (pendingStart >= document.Frames) pendingStart = 0; playbackEnd = document.Frames; }
        playing = true;
        if (!mediaReady) player.Open(new Uri(previewPath));
        else { player.Position = TimeSpan.FromSeconds(pendingStart / (double)document.Source.Rate); player.Play(); timer.Start(); }
        SetStatus("Ready"); RefreshInfo();
    }
    private void StopPlayback() { playing = false; timer.Stop(); player.Pause(); RefreshPlayButton(); }
    private void RefreshPlayButton() { if (PlayButton != null) PlayButton.Content = T[playing ? "Pause" : "Play"]; }
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback(); if (document != null) document.Cursor = document.Selection.Start;
        RefreshInfo(); Wave.InvalidateVisual();
    }
    private void InvalidatePlayback()
    {
        StopPlayback(); player.Close(); mediaReady = false;
        if (previewPath != null) { try { File.Delete(previewPath); } catch (IOException) { } previewPath = null; }
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Wave.Zoom(2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Wave.Zoom(.5);
    private void Fit_Click(object sender, RoutedEventArgs e) => Wave.Fit();
    private void Cancel_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void German_Click(object sender, RoutedEventArgs e) => SetLanguage(false);
    private void English_Click(object sender, RoutedEventArgs e) => SetLanguage(true);
    private void SetLanguage(bool english) { try { T.Set(english, true); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(this, T.Error(error), T["Title"]); } }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_DragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        if (paths.Length != 1) { MessageBox.Show(this, T["OneFile"], T["Title"]); return; }
        await OpenFile(paths[0]);
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox or ComboBox || busy) return;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key) { case Key.O: Open_Click(this, e); break; case Key.S: Export_Click(this, e); break; case Key.A: SelectAll_Click(this, e); break; case Key.X: Cut_Click(this, e); break; case Key.C: Copy_Click(this, e); break; case Key.V: Paste_Click(this, e); break; case Key.Z: Undo_Click(this, e); break; case Key.Y: Redo_Click(this, e); break; default: return; }
        }
        else if (Keyboard.Modifiers == ModifierKeys.None)
        {
            switch (e.Key) { case Key.Delete: Delete_Click(this, e); break; case Key.Space: Play_Click(this, e); break; case Key.Escape: Stop_Click(this, e); break; default: return; }
        }
        else return;
        e.Handled = true;
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; MessageBox.Show(this, T["Busy"], T["Title"]); return; }
        e.Cancel = !MayDiscard();
    }
}
