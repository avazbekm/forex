namespace Forex.Wpf.Resources.UserControls;

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

public partial class FloatingImageComboBox : UserControl
{
    public FloatingImageComboBox() => InitializeComponent();

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FloatingImageComboBox));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(FloatingImageComboBox));
    public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(FloatingImageComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(FloatingImageComboBox), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public static readonly DependencyProperty ImageMemberPathProperty = DependencyProperty.Register(nameof(ImageMemberPath), typeof(string), typeof(FloatingImageComboBox), new PropertyMetadata("ImagePath"));
    public string ImageMemberPath { get => (string)GetValue(ImageMemberPathProperty); set => SetValue(ImageMemberPathProperty, value); }

    public static readonly DependencyProperty PrimaryTextMemberPathProperty = DependencyProperty.Register(nameof(PrimaryTextMemberPath), typeof(string), typeof(FloatingImageComboBox), new PropertyMetadata("Code"));
    public string PrimaryTextMemberPath { get => (string)GetValue(PrimaryTextMemberPathProperty); set => SetValue(PrimaryTextMemberPathProperty, value); }

    public static readonly DependencyProperty SecondaryTextMemberPathProperty = DependencyProperty.Register(nameof(SecondaryTextMemberPathProperty), typeof(string), typeof(FloatingImageComboBox), new PropertyMetadata("Name"));
    public string SecondaryTextMemberPath { get => (string)GetValue(SecondaryTextMemberPathProperty); set => SetValue(SecondaryTextMemberPathProperty, value); }

    public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(nameof(ImageWidth), typeof(double), typeof(FloatingImageComboBox), new PropertyMetadata(24.0));
    public double ImageWidth { get => (double)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }

    public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(nameof(ImageHeight), typeof(double), typeof(FloatingImageComboBox), new PropertyMetadata(24.0));
    public double ImageHeight { get => (double)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }

    public static readonly DependencyProperty ImageCornerRadiusProperty = DependencyProperty.Register(nameof(ImageCornerRadius), typeof(CornerRadius), typeof(FloatingImageComboBox), new PropertyMetadata(new CornerRadius(0)));
    public CornerRadius ImageCornerRadius { get => (CornerRadius)GetValue(ImageCornerRadiusProperty); set => SetValue(ImageCornerRadiusProperty, value); }

    public static readonly DependencyProperty ShowImageProperty = DependencyProperty.Register(nameof(ShowImage), typeof(bool), typeof(FloatingImageComboBox), new PropertyMetadata(true));
    public bool ShowImage { get => (bool)GetValue(ShowImageProperty); set => SetValue(ShowImageProperty, value); }

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(FloatingImageComboBox), new PropertyMetadata(false));
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    public static readonly DependencyProperty IsSearchEnabledProperty = DependencyProperty.Register(nameof(IsSearchEnabled), typeof(bool), typeof(FloatingImageComboBox), new PropertyMetadata(true));
    public bool IsSearchEnabled { get => (bool)GetValue(IsSearchEnabledProperty); set => SetValue(IsSearchEnabledProperty, value); }

    public static readonly DependencyProperty IsTextSearchEnabledProperty = DependencyProperty.Register(nameof(IsTextSearchEnabled), typeof(bool), typeof(FloatingImageComboBox), new PropertyMetadata(true));
    public bool IsTextSearchEnabled { get => (bool)GetValue(IsTextSearchEnabledProperty); set => SetValue(IsTextSearchEnabledProperty, value); }


    public static readonly DependencyProperty ItemHoverScaleProperty = DependencyProperty.Register(nameof(ItemHoverScale), typeof(double), typeof(FloatingImageComboBox), new PropertyMetadata(1.0));
    public double ItemHoverScale { get => (double)GetValue(ItemHoverScaleProperty); set => SetValue(ItemHoverScaleProperty, value); }

    private void ComboBox_GotFocus(object sender, RoutedEventArgs e) => combo.IsDropDownOpen = true;

    private void ComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (combo.Template?.FindName("PART_Popup", combo) is Popup popup)
        {
            popup.AllowsTransparency = true;
            popup.Opened += (s, args) =>
            {
                if (popup.Child is FrameworkElement child)
                    SetClipToBoundsRecursive(child, false);
            };
        }
    }

    private void SetClipToBoundsRecursive(DependencyObject element, bool value)
    {
        if (element is FrameworkElement fe)
            fe.ClipToBounds = value;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            SetClipToBoundsRecursive(VisualTreeHelper.GetChild(element, i), value);
    }

    private void Item_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBoxItem item) return;

        item.RenderTransform = new ScaleTransform(1, 1);

        item.MouseEnter += (s, args) =>
        {
            if (ItemHoverScale <= 1.0) return;
            var transform = (ScaleTransform)item.RenderTransform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(ItemHoverScale, TimeSpan.FromMilliseconds(200)));
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(ItemHoverScale, TimeSpan.FromMilliseconds(200)));
        };

        item.MouseLeave += (s, args) =>
        {
            var transform = (ScaleTransform)item.RenderTransform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        };
    }
}
