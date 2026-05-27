using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.ViewModels;

public partial class ScheduleViewModel : ObservableObject
{
    private readonly ISchedulingService _schedulingService;
    private readonly INotificationService _notificationService;

    [ObservableProperty] public partial ObservableCollection<ScheduledExport> ScheduledExports { get; set; } = [];
    [ObservableProperty] public partial ScheduledExport SelectedSchedule { get; set; }
    [ObservableProperty] public partial bool IsEnabled { get; set; }
    [ObservableProperty] public partial DateTime StartDate { get; set; } = DateTime.Now;
    [ObservableProperty] public partial string StartTime { get; set; } = "09:00 AM";
    [ObservableProperty] public partial string RepeatType { get; set; } = "Does not repeat";
    [ObservableProperty] public partial string ScheduleName { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public ScheduleViewModel(ISchedulingService schedulingService, INotificationService notificationService)
    {
        _schedulingService = schedulingService;
        _notificationService = notificationService;

        _schedulingService.ExportTriggered += OnExportTriggered;
    }

    [RelayCommand]
    private void AddSchedule()
    {
        try
        {
            var frequency = RepeatType switch
            {
                "Daily" => ScheduleRepeatType.Daily,
                "Weekly" => ScheduleRepeatType.Weekly,
                "Monthly" => ScheduleRepeatType.Monthly,
                _ => ScheduleRepeatType.Once
            };

            var schedule = new ScheduledExport
            {
                Name = string.IsNullOrWhiteSpace(ScheduleName)
                    ? $"Export {DateTime.Now:yyyy-MM-dd HH:mm}"
                    : ScheduleName,
                Frequency = frequency,
                NextRunTime = StartDate.Date + DateTime.Parse(StartTime).TimeOfDay,
                IsEnabled = IsEnabled
            };

            _schedulingService.AddScheduledExport(schedule);
            ScheduledExports.Add(schedule);

            StatusMessage = $"Schedule '{schedule.Name}' added for {schedule.NextRunTime:yyyy-MM-dd HH:mm}";
            _notificationService.ShowSuccess(StatusMessage);

            ScheduleName = string.Empty;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to add schedule", ex);
        }
    }

    [RelayCommand]
    private void RemoveSchedule()
    {
        if (SelectedSchedule == null) return;

        try
        {
            _schedulingService.RemoveScheduledExport(SelectedSchedule.Id);
            ScheduledExports.Remove(SelectedSchedule);
            StatusMessage = $"Schedule '{SelectedSchedule.Name}' removed";
            _notificationService.ShowInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to remove schedule", ex);
        }
    }

    [RelayCommand]
    private void ToggleSchedule()
    {
        if (SelectedSchedule == null) return;

        SelectedSchedule.IsEnabled = !SelectedSchedule.IsEnabled;
        StatusMessage = SelectedSchedule.IsEnabled
            ? $"Schedule '{SelectedSchedule.Name}' enabled"
            : $"Schedule '{SelectedSchedule.Name}' disabled";
    }

    private void OnExportTriggered(object sender, ScheduledExportEventArgs e)
    {
        StatusMessage = $"Scheduled export '{e.Export.Name}' triggered at {DateTime.Now:HH:mm:ss}";
    }
}
