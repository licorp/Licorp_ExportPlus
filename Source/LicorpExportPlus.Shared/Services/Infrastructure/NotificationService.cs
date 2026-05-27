using System;
using Licorp.Diagnostics;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.Services.Infrastructure;

public class NotificationService : INotificationService
{
    public void ShowSuccess(string message)
    {
        LicorpTrace.Info($"[SUCCESS] {message}");
        ShowToast(message, NotificationType.Success);
    }

    public void ShowWarning(string message)
    {
        LicorpTrace.Warn($"[WARNING] {message}");
        ShowToast(message, NotificationType.Warning);
    }

    public void ShowError(string message)
    {
        LicorpTrace.Error($"[ERROR] {message}");
        ShowToast(message, NotificationType.Error);
    }

    public void ShowInfo(string message)
    {
        LicorpTrace.Info($"[INFO] {message}");
        ShowToast(message, NotificationType.Info);
    }

    public void ShowError(string message, Exception ex)
    {
        LicorpTrace.Error($"[ERROR] {message}: {ex.Message}", ex);
        ShowToast($"{message}: {ex.Message}", NotificationType.Error);
    }

    private void ShowToast(string message, NotificationType type)
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.StatusMessage = message;
                }
            });
        }
        catch
        {
        }
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
