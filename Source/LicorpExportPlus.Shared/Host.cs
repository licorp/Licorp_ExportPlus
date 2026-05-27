using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Licorp.Diagnostics;
using LicorpExportPlus.Services;
using LicorpExportPlus.Services.Infrastructure;
using LicorpExportPlus.Services.Interfaces;
using LicorpExportPlus.ViewModels;

namespace LicorpExportPlus;

public static class Host
{
    private static readonly Dictionary<Type, object> _services = new();
    private static bool _isStarted;

    public static Task StartAsync()
    {
        if (_isStarted) return Task.CompletedTask;

        try
        {
            RegisterServices();
            RegisterViewModels();
            _isStarted = true;
            LicorpTrace.Info("[Host] DI Container initialized successfully");
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[Host] Failed to initialize DI Container: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    public static Task StopAsync()
    {
        if (!_isStarted) return Task.CompletedTask;

        try
        {
            _services.Clear();
            _isStarted = false;
            LicorpTrace.Info("[Host] DI Container disposed");
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[Host] Failed to dispose DI Container: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    public static T GetService<T>() where T : class
    {
        if (!_isStarted) return null;

        var type = typeof(T);
        if (_services.TryGetValue(type, out var existing))
            return (T)existing;

        return null;
    }

    public static void RegisterSingleton<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }

    private static void RegisterServices()
    {
        _services[typeof(INotificationService)] = new NotificationService();
        _services[typeof(IProfileService)] = new ProfileServiceAdapter();
        _services[typeof(IExportReportService)] = new ExportReportServiceAdapter();
        _services[typeof(IDrawingTransmittalService)] = new DrawingTransmittalServiceAdapter();
        _services[typeof(ISchedulingService)] = new SchedulingAssistantAdapter();
    }

    private static void RegisterViewModels()
    {
        _services[typeof(SheetsViewModel)] = new SheetsViewModel();
        _services[typeof(FormatsViewModel)] = new FormatsViewModel();

        _services[typeof(ProfileViewModel)] = new ProfileViewModel(
            GetService<IProfileService>(),
            GetService<INotificationService>());

        _services[typeof(ExportViewModel)] = new ExportViewModel(
            GetService<INotificationService>());

        _services[typeof(ScheduleViewModel)] = new ScheduleViewModel(
            GetService<ISchedulingService>(),
            GetService<INotificationService>());

        _services[typeof(MainViewModel)] = new MainViewModel(
            GetService<SheetsViewModel>(),
            GetService<FormatsViewModel>(),
            GetService<ProfileViewModel>(),
            GetService<ExportViewModel>(),
            GetService<ScheduleViewModel>(),
            GetService<INotificationService>());
    }
}

internal static class RevitDocumentProvider
{
    private static Autodesk.Revit.DB.Document _document;

    public static void SetDocument(Autodesk.Revit.DB.Document document)
    {
        _document = document;
    }

    public static Autodesk.Revit.DB.Document GetDocument()
    {
        return _document;
    }
}
