using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INotificationService _notificationService;

    [ObservableProperty] public partial SheetsViewModel Sheets { get; set; }
    [ObservableProperty] public partial FormatsViewModel Formats { get; set; }
    [ObservableProperty] public partial ProfileViewModel Profiles { get; set; }
    [ObservableProperty] public partial ExportViewModel Export { get; set; }
    [ObservableProperty] public partial ScheduleViewModel Schedule { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready";
    [ObservableProperty] public partial int SelectedTab { get; set; }
    [ObservableProperty] public partial bool IsExporting { get; set; }
    [ObservableProperty] public partial bool IsSheetsTabSelected { get; set; } = true;
    [ObservableProperty] public partial bool IsViewsTabSelected { get; set; }
    [ObservableProperty] public partial bool IsFormatsTabSelected { get; set; }
    [ObservableProperty] public partial string ActiveSheetViewMode { get; set; } = "Sheets";

    public MainViewModel(
        SheetsViewModel sheetsViewModel,
        FormatsViewModel formatsViewModel,
        ProfileViewModel profileViewModel,
        ExportViewModel exportViewModel,
        ScheduleViewModel scheduleViewModel,
        INotificationService notificationService)
    {
        _notificationService = notificationService;

        Sheets = sheetsViewModel;
        Formats = formatsViewModel;
        Profiles = profileViewModel;
        Export = exportViewModel;
        Schedule = scheduleViewModel;

        Sheets.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Sheets.SelectedCount))
            {
                OnPropertyChanged(nameof(SelectedSheetsSummary));
            }
        };
    }

    public string SelectedSheetsSummary =>
        $"{Sheets?.SelectedCount ?? 0} of {Sheets?.TotalCount ?? 0} sheets selected";

    [RelayCommand]
    private async Task ExportAsync()
    {
        var selectedSheets = Sheets?.GetSelectedSheets();
        if (selectedSheets == null || selectedSheets.Count == 0)
        {
            _notificationService.ShowWarning("No sheets selected for export");
            return;
        }

        var selectedFormats = Formats?.GetSelectedFormats();
        if (selectedFormats == null || selectedFormats.Count == 0)
        {
            _notificationService.ShowWarning("No export formats selected");
            return;
        }

        IsExporting = true;
        StatusMessage = "Exporting...";

        try
        {
            var settings = Formats.GetExportSettings();
            settings.OutputFolder = Export.OutputFolder;

            Export.PrepareExport(selectedSheets, selectedFormats, settings);
            Export.ExecuteCommand.Execute(null);

            _notificationService.ShowSuccess($"Export completed: {selectedSheets.Count} sheets");
            StatusMessage = "Export completed";
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Export failed", ex);
            StatusMessage = "Export failed";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void SwitchToSheetsMode()
    {
        ActiveSheetViewMode = "Sheets";
        IsSheetsTabSelected = true;
        IsViewsTabSelected = false;
    }

    [RelayCommand]
    private void SwitchToViewsMode()
    {
        ActiveSheetViewMode = "Views";
        IsSheetsTabSelected = false;
        IsViewsTabSelected = true;
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            StatusMessage = "Loading sheets...";
            await Sheets.LoadSheetsCommand.ExecuteAsync(null);

            StatusMessage = "Loading profiles...";
            Profiles.LoadProfilesCommand.Execute(null);

            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to load data", ex);
            StatusMessage = "Error loading data";
        }
    }
}
