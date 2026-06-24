namespace Forex.Wpf.Resources.UserControls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public partial class StatCard : UserControl
{
    public StatCard() => InitializeComponent();

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty, OnCaptionChanged));

    public static readonly DependencyProperty CaptionVisibilityProperty =
        DependencyProperty.Register(nameof(CaptionVisibility), typeof(Visibility), typeof(StatCard),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(nameof(CardBackground), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(new BrushConverter().ConvertFromString("#EEF2FF") as Brush));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(new BrushConverter().ConvertFromString("#3949AB") as Brush));

    public static readonly DependencyProperty TitleBrushProperty =
        DependencyProperty.Register(nameof(TitleBrush), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(new BrushConverter().ConvertFromString("#6470B0") as Brush));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public Visibility CaptionVisibility { get => (Visibility)GetValue(CaptionVisibilityProperty); set => SetValue(CaptionVisibilityProperty, value); }
    public Brush CardBackground { get => (Brush)GetValue(CardBackgroundProperty); set => SetValue(CardBackgroundProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush TitleBrush { get => (Brush)GetValue(TitleBrushProperty); set => SetValue(TitleBrushProperty, value); }

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((StatCard)d).CaptionVisibility = string.IsNullOrWhiteSpace(e.NewValue as string)
            ? Visibility.Collapsed : Visibility.Visible;
}
