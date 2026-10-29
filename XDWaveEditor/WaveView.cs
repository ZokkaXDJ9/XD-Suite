using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace XDWaveEditor;

public sealed class WaveView : FrameworkElement
{
    private AudioDocument? document;
    private long anchor;
    private double viewStart, visibleFrames = 1;
    public AudioDocument? Document { get => document; set { document = value; Fit(); } }
    public bool IsEditingEnabled { get; set; } = true;
    public double ViewStart => viewStart;
    public double VisibleFrames => visibleFrames;
    public event Action? ViewChanged;
    public WaveView() { Focusable = true; Cursor = Cursors.IBeam; ClipToBounds = true; }
    public void Fit() { viewStart = 0; visibleFrames = Math.Max(1, document?.Frames ?? 1); InvalidateVisual(); ViewChanged?.Invoke(); }
    public void Scroll(double value) { viewStart = Math.Clamp(value, 0, Math.Max(0, (document?.Frames ?? 0) - visibleFrames)); InvalidateVisual(); ViewChanged?.Invoke(); }
    public void Zoom(double factor, double at = .5)
    {
        if (document == null || document.Frames == 0) return;
        double center = viewStart + visibleFrames * at;
        visibleFrames = Math.Clamp(visibleFrames / factor, Math.Min(document.Frames, Math.Max(32, document.Source.Rate * .005)), Math.Max(1, document.Frames));
        Scroll(center - visibleFrames * at);
    }
    public long FrameAt(double x) => (long)Math.Clamp(viewStart + Math.Clamp(x / Math.Max(1, ActualWidth), 0, 1) * visibleFrames, 0, document?.Frames ?? 0);
    private double X(long frame) => (frame - viewStart) / visibleFrames * ActualWidth;
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEditingEnabled || document == null) return;
        Focus(); anchor = FrameAt(e.GetPosition(this).X); document.Select(anchor, anchor); CaptureMouse(); InvalidateVisual(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsMouseCaptured || document == null) return;
        double x = e.GetPosition(this).X;
        if (x < 0) Scroll(viewStart - visibleFrames * .025); else if (x > ActualWidth) Scroll(viewStart + visibleFrames * .025);
        document.Select(anchor, FrameAt(x)); InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) { ReleaseMouseCapture(); e.Handled = true; } }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { Zoom(e.Delta > 0 ? 1.5 : 1 / 1.5, Math.Clamp(e.GetPosition(this).X / Math.Max(1, ActualWidth), 0, 1)); e.Handled = true; }
    protected override void OnRender(DrawingContext draw)
    {
        base.OnRender(draw);
        double width = ActualWidth, height = ActualHeight;
        if (width <= 1 || height <= 1) return;
        draw.DrawRectangle(Brush("#172333"), null, new(0, 0, width, height));
        if (document == null || document.Frames == 0) return;
        const double top = 30, bottom = 12;
        double band = (height - top - bottom) / document.Source.Channels;
        for (int i = 0; i <= 8; i++)
        {
            double x = width * i / 8;
            draw.DrawLine(new(Brush("#28384A"), 1), new(x, top), new(x, height));
            string label = TimeSpan.FromSeconds((viewStart + visibleFrames * i / 8) / document.Source.Rate).ToString(visibleFrames / document.Source.Rate < 10 ? @"mm\:ss\.fff" : @"hh\:mm\:ss", CultureInfo.InvariantCulture);
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brush("#A4B2C3"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            draw.DrawText(text, new(Math.Clamp(x - text.Width / 2, 4, Math.Max(4, width - text.Width - 4)), 7));
        }
        if (document.Selection.Length > 0) Shade(document.Selection, "#384C6B", .8);
        for (int channel = 0; channel < document.Source.Channels; channel++)
        {
            double center = top + band * (channel + .5), amplitude = Math.Max(1, band / 2 - 20);
            draw.DrawLine(new(Brush("#425066"), 1), new(0, center), new(width, center));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                for (int pixel = 0; pixel < (int)width; pixel++)
                {
                    long start = FrameAt(pixel), end = Math.Max(start + 1, FrameAt(pixel + 1));
                    end = Math.Min(document.Frames, end);
                    if (start >= end) continue;
                    var peak = document.GetPeak(start, end, channel);
                    context.BeginFigure(new(pixel + .5, center - peak.Max * amplitude), false, false);
                    context.LineTo(new(pixel + .5, center - peak.Min * amplitude + .5), true, false);
                }
            }
            geometry.Freeze(); draw.DrawGeometry(null, new(Brush(channel == 0 ? "#6AE1C1" : "#76BFF5"), 1), geometry);
            var label = new FormattedText(document.Source.Channels == 1 ? "MONO" : channel == 0 ? "L" : "R", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 11, Brush("#A4B2C3"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            draw.DrawText(label, new(10, top + band * channel + 8));
        }
        double cursor = X(document.Cursor);
        if (cursor >= 0 && cursor <= width) draw.DrawLine(new(Brush("#F5D47B"), 1.4), new(cursor, top), new(cursor, height));

        void Shade(FrameRange range, string color, double opacity)
        {
            double left = Math.Clamp(X(range.Start), 0, width), right = Math.Clamp(X(range.End), 0, width);
            if (right <= left) return;
            var brush = Brush(color); brush.Opacity = opacity; draw.DrawRectangle(brush, null, new(left, top, right - left, height - top));
        }
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
