namespace Forex.Wpf.Resources.UserControls;

using System.Windows;
using System.Windows.Controls;

public partial class Pager : UserControl
{
    public Pager()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateState();
    }

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(Pager),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public static readonly DependencyProperty TotalPagesProperty =
        DependencyProperty.Register(nameof(TotalPages), typeof(int), typeof(Pager),
            new FrameworkPropertyMetadata(1, OnStateChanged));

    public int CurrentPage { get => (int)GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalPages { get => (int)GetValue(TotalPagesProperty); set => SetValue(TotalPagesProperty, value); }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Pager)d).UpdateState();

    private void UpdateState()
    {
        if (lblPage is null) return;
        lblPage.Text = $"{CurrentPage} / {TotalPages}";
        btnFirst.IsEnabled = btnPrev.IsEnabled = CurrentPage > 1;
        btnNext.IsEnabled = btnLast.IsEnabled = CurrentPage < TotalPages;
    }

    private void First_Click(object sender, RoutedEventArgs e) => CurrentPage = 1;
    private void Prev_Click(object sender, RoutedEventArgs e) { if (CurrentPage > 1) CurrentPage--; }
    private void Next_Click(object sender, RoutedEventArgs e) { if (CurrentPage < TotalPages) CurrentPage++; }
    private void Last_Click(object sender, RoutedEventArgs e) => CurrentPage = TotalPages;
}
