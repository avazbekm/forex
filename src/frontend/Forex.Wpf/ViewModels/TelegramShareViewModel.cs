using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Forex.Wpf.Pages.Common;

namespace Forex.Wpf.ViewModels;

public partial class TelegramShareViewModel : ViewModelBase
{
    [ObservableProperty]
    private string pdfFilePath = string.Empty;

    [ObservableProperty]
    private string messageCaption = string.Empty;

    public event Action? RequestClose;

    [RelayCommand]
    private void Share()
    {
        if (string.IsNullOrEmpty(PdfFilePath) || !File.Exists(PdfFilePath))
        {
            MessageBox.Show("Fayl topilmadi.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var paths = new System.Collections.Specialized.StringCollection { PdfFilePath };
            Clipboard.SetFileDropList(paths);

            var telegramPath = GetTelegramPath();
            if (!string.IsNullOrEmpty(telegramPath))
            {
                Process.Start(telegramPath);
            }
            else
            {
                Process.Start("explorer.exe", $"/select,\"{PdfFilePath}\"");
            }

            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(PdfFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Faylni ochishda xatolik: {ex.Message}");
        }
    }

    private string? GetTelegramPath()
    {
        // Oddiy qidiruv: Default o'rnatish joylari
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string progFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] possiblePaths = {
            Path.Combine(appData, "Telegram Desktop", "Telegram.exe"),
            Path.Combine(progFiles, "Telegram Desktop", "Telegram.exe"),
            Path.Combine(progFiles86, "Telegram Desktop", "Telegram.exe")
        };

        foreach (var p in possiblePaths)
        {
            if (File.Exists(p)) return p;
        }

        return null;
    }
}