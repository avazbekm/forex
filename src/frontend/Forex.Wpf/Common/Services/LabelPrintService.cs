namespace Forex.Wpf.Common.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

public sealed record LabelItem(string Title, string Size, string UnitLabel, int Pairs, string? Barcode);

public static class LabelPrintService
{
    private const double MmToDip = 96.0 / 25.4;
    private const int MaxCopies = 500;

    public static bool Print(IReadOnlyCollection<LabelItem> labels, string? printerName, int copies, double widthMm = 60, double heightMm = 40)
    {
        var valid = labels.Where(l => !string.IsNullOrWhiteSpace(l.Barcode)).ToList();
        if (valid.Count == 0)
        {
            MessageBox.Show("Chop etish uchun barkod yo'q.", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            MessageBox.Show("Avval Sozlamalar → Printer bo'limida yorliq printerini tanlang.", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        PrintQueue? queue;
        try
        {
            queue = new LocalPrintServer()
                .GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                .FirstOrDefault(q => q.FullName == printerName);
        }
        catch
        {
            MessageBox.Show("Printerlar ro'yxatini olib bo'lmadi. Windows Print Spooler xizmati ishlayotganini tekshiring.", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (queue is null)
        {
            MessageBox.Show("Tanlangan printer topilmadi. Sozlamalardan qayta tanlang.", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            new PrintDialog { PrintQueue = queue }
                .PrintDocument(BuildDocument(valid, Math.Clamp(copies, 1, MaxCopies), widthMm, heightMm).DocumentPaginator, "Yorliqlar");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chop etishda xatolik: {ex.Message}", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        return true;
    }

    public static void ShowPreview(IReadOnlyCollection<LabelItem> labels, string? printerName, double widthMm = 60, double heightMm = 40)
    {
        var valid = labels.Where(l => !string.IsNullOrWhiteSpace(l.Barcode)).ToList();
        if (valid.Count == 0)
        {
            MessageBox.Show("Chop etish uchun barkod yo'q.", "Yorliq", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var copiesBox = new TextBox
        {
            Width = 56,
            Text = "1",
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var printButton = new Button
        {
            Content = "Chop etish",
            Padding = new Thickness(18, 6, 18, 6),
            Margin = new Thickness(16, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 8, 10, 8)
        };
        bar.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(printerName) ? "Printer: (sozlanmagan)" : $"Printer: {printerName}", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(new TextBlock { Text = "Nusxa:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0) });
        bar.Children.Add(copiesBox);
        bar.Children.Add(printButton);

        var viewer = new DocumentViewer { Document = BuildDocument(valid, 1, widthMm, heightMm), Margin = new Thickness(8) };

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        layout.Children.Add(bar);
        layout.Children.Add(viewer);

        var window = new Window
        {
            Title = "Yorliq — oldindan ko'rish",
            Width = 720,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout,
            Owner = Application.Current.MainWindow
        };

        printButton.Click += (_, _) =>
        {
            var copies = int.TryParse(copiesBox.Text, out var c) && c > 0 ? c : 1;
            if (Print(valid, printerName, copies, widthMm, heightMm))
                window.Close();
        };

        window.ShowDialog();
    }

    private static FixedDocument BuildDocument(IReadOnlyCollection<LabelItem> labels, int copies, double widthMm, double heightMm)
    {
        var pageWidth = widthMm * MmToDip;
        var pageHeight = heightMm * MmToDip;

        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        foreach (var label in labels)
        {
            var barcode = BarcodeImageService.Render(label.Barcode!);
            for (var i = 0; i < copies; i++)
            {
                var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                page.Children.Add(BuildLabel(label, barcode, pageWidth, pageHeight));

                var content = new PageContent();
                ((IAddChild)content).AddChild(page);
                doc.Pages.Add(content);
            }
        }

        return doc;
    }

    private static UIElement BuildLabel(LabelItem label, ImageSource barcode, double pageWidth, double pageHeight)
    {
        var ink = new SolidColorBrush(Color.FromRgb(0x12, 0x18, 0x22));

        var panel = new StackPanel
        {
            Width = pageWidth - 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = label.Title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = ink,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 24
        });

        var info = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 2)
        };
        info.Children.Add(new TextBlock
        {
            Text = label.Size,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center
        });
        info.Children.Add(new TextBlock
        {
            Text = $"   {label.UnitLabel} · {label.Pairs} juft",
            FontSize = 11,
            Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(info);

        panel.Children.Add(new Image
        {
            Source = barcode,
            Width = pageWidth - 20,
            Height = 46,
            Stretch = Stretch.Fill
        });

        panel.Children.Add(new TextBlock
        {
            Text = label.Barcode,
            FontSize = 10,
            FontFamily = new FontFamily("Consolas"),
            Foreground = ink,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });

        return new Border
        {
            Width = pageWidth,
            Height = pageHeight,
            Child = panel
        };
    }
}
