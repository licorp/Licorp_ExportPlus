using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using LicorpExportPlus.Services.Infrastructure;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.ViewModels;

public partial class ExportViewModel : ObservableObject
{
    private readonly INotificationService _notificationService;

    public ExportViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [ObservableProperty] public partial string OutputFolder { get; set; } = string.Empty;
    [ObservableProperty] public partial string FileNameTemplate { get; set; } = "{SheetNumber}_{SheetName}";
    [ObservableProperty] public partial bool CreateSubfolders { get; set; } = true;

    [ObservableProperty] public partial bool IsExporting { get; set; }
    [ObservableProperty] public partial double ExportProgress { get; set; }
    [ObservableProperty] public partial string CurrentSheetName { get; set; } = string.Empty;
    [ObservableProperty] public partial int CompletedCount { get; set; }
    [ObservableProperty] public partial int FailedCount { get; set; }
    [ObservableProperty] public partial int TotalCount { get; set; }
    [ObservableProperty] public partial string ExportStatus { get; set; } = "Ready";

    [ObservableProperty] public partial ObservableCollection<ExportQueueItem> ExportQueue { get; set; } = [];
    [ObservableProperty] public partial string ReportType { get; set; } = "Don't Save Report";

    private List<SheetItem> _pendingSheets;
    private List<string> _pendingFormats;
    private ExportSettings _pendingSettings;

    public void PrepareExport(List<SheetItem> sheets, List<string> formats, ExportSettings settings)
    {
        _pendingSheets = sheets;
        _pendingFormats = formats;
        _pendingSettings = settings;
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        var sheets = _pendingSheets;
        var formats = _pendingFormats;
        var settings = _pendingSettings;

        if (sheets == null || sheets.Count == 0)
        {
            _notificationService.ShowWarning("No sheets selected");
            return;
        }

        if (formats == null || formats.Count == 0)
        {
            _notificationService.ShowWarning("No formats selected");
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            _notificationService.ShowWarning("Output folder not specified");
            return;
        }

        IsExporting = true;
        TotalCount = sheets.Count * formats.Count;
        CompletedCount = 0;
        FailedCount = 0;
        ExportQueue.Clear();

        try
        {
            Directory.CreateDirectory(OutputFolder);

            foreach (var format in formats)
            {
                ExportStatus = $"Exporting {format}...";
                await ExportByFormatAsync(sheets, format, settings);
            }

            ExportStatus = $"Completed: {CompletedCount} success, {FailedCount} failed";
            _notificationService.ShowSuccess(ExportStatus);
        }
        catch (Exception ex)
        {
            ExportStatus = $"Export failed: {ex.Message}";
            _notificationService.ShowError("Export failed", ex);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task ExportByFormatAsync(List<SheetItem> sheets, string format, ExportSettings settings)
    {
        await Task.Run(() =>
        {
            var doc = RevitDocumentProvider.GetDocument();
            if (doc == null)
            {
                _notificationService.ShowError("No Revit document available");
                return;
            }

            switch (format)
            {
                case "PDF":
                    ExportPdf(doc, sheets, settings);
                    break;
                case "DWG":
                    ExportDwg(doc, sheets, settings);
                    break;
                case "IFC":
                    ExportIfc(doc, sheets, settings);
                    break;
                default:
                    _notificationService.ShowWarning($"Format {format} not yet implemented");
                    break;
            }
        });
    }

    private void ExportPdf(Autodesk.Revit.DB.Document doc, List<SheetItem> sheets, ExportSettings settings)
    {
        try
        {
            var pdfService = new PDFExportService(doc);
            pdfService.ExportSheetsWithCustomNames(sheets, OutputFolder, settings,
                (current, total, name, success) =>
                {
                    CurrentSheetName = name;
                    ExportProgress = (double)current / total * 100;

                    if (success) CompletedCount++;
                    else FailedCount++;

                    ExportQueue.Add(new ExportQueueItem
                    {
                        ViewSheetNumber = name,
                        Format = "PDF",
                        Status = success ? "Completed" : "Failed",
                        Progress = success ? 100 : 0,
                        OutputPath = OutputFolder,
                        CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("PDF export failed", ex);
        }
    }

    private void ExportDwg(Autodesk.Revit.DB.Document doc, List<SheetItem> sheets, ExportSettings settings)
    {
        try
        {
            var dwgService = new DWGExportService(doc);
            var viewSheets = sheets
                .Where(s => s.RevitSheet != null)
                .Select(s => s.RevitSheet)
                .ToList();

            var dwgSettings = new PSDWGExportSettings
            {
                OutputFolder = OutputFolder,
                DWGVersion = settings.DWGVersion,
                UseSharedCoordinates = settings.UseSharedCoordinates,
                CompactDwgFiles = settings.CompactDwgFiles
            };

            dwgService.ExportToDWG(viewSheets, dwgSettings);
            CompletedCount += sheets.Count;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("DWG export failed", ex);
        }
    }

    private void ExportIfc(Autodesk.Revit.DB.Document doc, List<SheetItem> sheets, ExportSettings settings)
    {
        try
        {
            var ifcService = new IFCExportService(doc);
            var viewSheets = sheets
                .Where(s => s.RevitSheet != null)
                .Select(s => s.RevitSheet)
                .ToList();

            var ifcSettings = new IFCExportSettings();
            ifcService.ExportToIFC(viewSheets, ifcSettings, OutputFolder);
            CompletedCount += sheets.Count;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("IFC export failed", ex);
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select output folder for exports"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputFolder = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (!string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", OutputFolder);
        }
    }
}
