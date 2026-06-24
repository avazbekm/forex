namespace Forex.Wpf.Resources.Charts;

using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

public partial class BarChart : UserControl
{
    public BarChart()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IEnumerable), typeof(BarChart),
            new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Color), typeof(BarChart),
            new PropertyMetadata(Color.FromRgb(0x3B, 0x5B, 0xDB), (d, _) => ((BarChart)d).Render()));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(BarChart),
            new PropertyMetadata(0.0, (d, _) => ((BarChart)d).Render()));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(BarChart),
            new PropertyMetadata(0.0, (d, _) => ((BarChart)d).Render()));

    public IEnumerable? Points { get => (IEnumerable?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public double RowHeight { get => (double)GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (BarChart)d;
        if (e.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= chart.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += chart.OnCollectionChanged;
        chart.Render();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Render();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        canvas.Children.Clear();

        var data = Points?.Cast<ChartPoint>().ToList() ?? [];

        const double labelW = 150, valueW = 70, top = 10, bottom = 10;
        bool scroll = RowHeight > 0;

        if (scroll)
        {
            double needed = data.Count > 0 ? top + bottom + data.Count * RowHeight : 0;
            if (Math.Abs(Height - needed) > 0.5 || (double.IsNaN(Height) && needed > 0))
                Height = needed;
        }
        else
        {
            Height = double.NaN;
        }

        double w = canvas.ActualWidth, h = canvas.ActualHeight;
        if (w <= 0 || h <= 0 || data.Count == 0) return;

        double plotW = w - labelW - valueW;
        if (plotW <= 0) return;

        double maxValue = MaxValue > 0 ? MaxValue : data.Max(p => p.Value);
        if (maxValue <= 0) maxValue = 1;

        var accent = Accent;
        var trackBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF8));
        var nameBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x54, 0x67));

        double rowH = scroll ? RowHeight : (h - top - bottom) / data.Count;
        double barH = Math.Min(26, rowH * 0.56);

        for (int i = 0; i < data.Count; i++)
        {
            var p = data[i];
            var barColor = p.Color ?? accent;
            double cy = top + rowH * i + rowH / 2;
            double barLen = Math.Max(2, plotW * (p.Value / maxValue));

            // Name label
            var name = new TextBlock
            {
                Text = p.Label,
                FontSize = 12.5,
                Foreground = nameBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = labelW - 12
            };
            name.Measure(new Size(labelW - 12, rowH));
            Canvas.SetLeft(name, 0);
            Canvas.SetTop(name, cy - name.DesiredSize.Height / 2);
            canvas.Children.Add(name);

            // Track
            var track = new Rectangle
            {
                Width = plotW,
                Height = barH,
                RadiusX = barH / 2,
                RadiusY = barH / 2,
                Fill = trackBrush
            };
            Canvas.SetLeft(track, labelW);
            Canvas.SetTop(track, cy - barH / 2);
            canvas.Children.Add(track);

            // Bar
            var bar = new Rectangle
            {
                Width = barLen,
                Height = barH,
                RadiusX = barH / 2,
                RadiusY = barH / 2,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(0xFF, barColor.R, barColor.G, barColor.B),
                    Color.FromArgb(0xCC, barColor.R, barColor.G, barColor.B),
                    0),
                ToolTip = $"{p.Label}: {p.Value:N0}"
            };
            Canvas.SetLeft(bar, labelW);
            Canvas.SetTop(bar, cy - barH / 2);
            canvas.Children.Add(bar);

            // Value label
            var value = new TextBlock
            {
                Text = p.Value.ToString("N0"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(barColor)
            };
            value.Measure(new Size(valueW, rowH));
            Canvas.SetLeft(value, labelW + barLen + 8);
            Canvas.SetTop(value, cy - value.DesiredSize.Height / 2);
            canvas.Children.Add(value);
        }
    }
}
