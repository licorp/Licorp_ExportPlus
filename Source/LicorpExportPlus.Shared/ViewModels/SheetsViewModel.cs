using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Infrastructure;
using LicorpExportPlus.Utils;

namespace LicorpExportPlus.ViewModels;

public partial class SheetsViewModel : ObservableObject
{
    [ObservableProperty] public partial ObservableCollection<SheetItem> Sheets { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<ViewSheetSetInfo> ViewSheetSets { get; set; } = [];
    [ObservableProperty] public partial SheetItem SelectedSheet { get; set; }
    [ObservableProperty] public partial bool IsSelectAllChecked { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string FilterType { get; set; } = "All Sheets";
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string LoadingStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial double LoadingProgress { get; set; }

    public int SelectedCount => Sheets?.Count(s => s.IsSelected) ?? 0;
    public int TotalCount => Sheets?.Count ?? 0;

    private List<SheetItem> _allSheets = new();

    partial void OnSearchTextChanged(string value)
    {
        FilterSheets();
    }

    partial void OnIsSelectAllCheckedChanged(bool value)
    {
        if (Sheets == null) return;
        foreach (var sheet in Sheets)
            sheet.IsSelected = value;
        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    private async Task LoadSheetsAsync()
    {
        IsLoading = true;
        LoadingStatus = "Loading sheets...";

        try
        {
            var sheets = await ExportPlusApplication.RevitTask.Run(uiapp =>
            {
                var doc = uiapp.ActiveUIDocument.Document;
                RevitDocumentProvider.SetDocument(doc);
                return LoadSheetsFromDocument(doc);
            }, System.Threading.CancellationToken.None);

            _allSheets = sheets;
            Sheets = new ObservableCollection<SheetItem>(sheets);
            LoadingStatus = $"Loaded {Sheets.Count} sheets";
            LoadingProgress = 100;

            foreach (var sheet in Sheets)
            {
                sheet.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SheetItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCount));
                    }
                };
            }

            LicorpTrace.Info($"[SheetsViewModel] Loaded {Sheets.Count} sheets");
        }
        catch (Exception ex)
        {
            LoadingStatus = $"Error: {ex.Message}";
            LicorpTrace.Error($"[SheetsViewModel] Failed to load sheets: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (Sheets == null) return;
        foreach (var sheet in Sheets)
            sheet.IsSelected = true;
        IsSelectAllChecked = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (Sheets == null) return;
        foreach (var sheet in Sheets)
            sheet.IsSelected = false;
        IsSelectAllChecked = false;
    }

    [RelayCommand]
    private void SelectByDiscipline(string discipline)
    {
        if (Sheets == null) return;
        foreach (var sheet in Sheets)
            sheet.IsSelected = sheet.SheetNumber?.StartsWith(discipline, StringComparison.OrdinalIgnoreCase) == true;
    }

    [RelayCommand]
    private void InvertSelection()
    {
        if (Sheets == null) return;
        foreach (var sheet in Sheets)
            sheet.IsSelected = !sheet.IsSelected;
    }

    public List<SheetItem> GetSelectedSheets()
        => Sheets?.Where(s => s.IsSelected).ToList() ?? new List<SheetItem>();

    private void FilterSheets()
    {
        if (_allSheets == null || _allSheets.Count == 0) return;

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allSheets
            : _allSheets.Where(s =>
                (s.SheetNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (s.SheetName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (s.Revision?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

        Sheets = new ObservableCollection<SheetItem>(filtered);
    }

    private List<SheetItem> LoadSheetsFromDocument(Autodesk.Revit.DB.Document doc)
    {
        var sheets = new List<SheetItem>();
        try
        {
            SheetSizeDetector.PreloadTitleBlockSizes(doc);

            var collector = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.ViewSheet));

            foreach (Autodesk.Revit.DB.ViewSheet sheet in collector)
            {
                var item = new SheetItem
                {
                    Id = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    Revision = sheet.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                    Size = SheetSizeDetector.GetSheetSize(sheet),
                    RevitSheet = sheet
                };

                sheets.Add(item);
            }

            sheets.Sort((a, b) =>
            {
                var cmp = string.Compare(a.SheetNumber, b.SheetNumber, StringComparison.OrdinalIgnoreCase);
                return cmp != 0 ? cmp : string.Compare(a.SheetName, b.SheetName, StringComparison.OrdinalIgnoreCase);
            });
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[SheetsViewModel] Error loading sheets: {ex.Message}", ex);
        }

        return sheets;
    }
}
