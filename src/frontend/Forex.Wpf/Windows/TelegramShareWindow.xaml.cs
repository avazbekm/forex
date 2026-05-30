using Forex.Wpf.ViewModels;
using System.Windows;

namespace Forex.Wpf.Windows;

public partial class TelegramShareWindow : Window
{
    public TelegramShareWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TelegramShareViewModel oldVm)
            oldVm.RequestClose -= Close;

        if (e.NewValue is TelegramShareViewModel newVm)
            newVm.RequestClose += Close;
    }
}