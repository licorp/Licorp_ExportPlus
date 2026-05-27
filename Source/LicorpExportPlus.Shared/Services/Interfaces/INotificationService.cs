using System;

namespace LicorpExportPlus.Services.Interfaces;

public interface INotificationService
{
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
    void ShowInfo(string message);
    void ShowError(string message, Exception ex);
}
