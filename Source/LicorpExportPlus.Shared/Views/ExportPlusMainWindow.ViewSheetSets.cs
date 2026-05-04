using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using Licorp.Diagnostics;
using RevitView = Autodesk.Revit.DB.View;

namespace LicorpExportPlus.Views
{
    /// <summary>
    /// View/Sheet Set Management - Partial class for ExportPlusMainWindow
    /// </summary>
    public partial class ExportPlusMainWindow
    {
        #region View/Sheet Set Management
        
        /// <summary>
        /// Load all View/Sheet Sets into ItemsControl
        /// </summary>
        private void LoadViewSheetSets()
        {
            try
            {
                
                if (_viewSheetSetManager == null)
                {
                    return;
                }
                
                var sets = _viewSheetSetManager.GetAllViewSheetSets();
                
                // Create ObservableCollection and subscribe to PropertyChanged
                ViewSheetSets = new ObservableCollection<ViewSheetSetInfo>();
                
                foreach (var set in sets)
                {
                    // Subscribe to IsSelected changes
                    set.PropertyChanged += ViewSheetSet_PropertyChanged;
                    ViewSheetSets.Add(set);
                }
                
                // Bind to ItemsControl
                ViewSheetSetItems.ItemsSource = ViewSheetSets;
                
                // Select "All Sheets" by default if no filter checkbox
                if (sets.Count > 0 && FilterByVSCheckBox?.IsChecked != true)
                {
                    var allSheets = ViewSheetSets.FirstOrDefault(s => s.Name == "All Sheets");
                    if (allSheets != null)
                    {
                        allSheets.IsSelected = true;
                    }
                }
                
            }
            catch (Exception) {
            }
        }
        
        /// <summary>
        /// Handle ViewSheetSet IsSelected property changes
        /// </summary>
        private void ViewSheetSet_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewSheetSetInfo.IsSelected))
            {
                Logger.Info($"ViewSheetSet selection changed");
                OnPropertyChanged(nameof(SelectedSetsDisplay));
                
                // Auto-apply filter if checkbox is checked
                Logger.Info($"FilterByVSCheckBox?.IsChecked = {FilterByVSCheckBox?.IsChecked}");
                
                if (FilterByVSCheckBox?.IsChecked == true)
                {
                    Logger.Info("Calling ApplyMultiSetFilter from PropertyChanged...");
                    ApplyMultiSetFilter();
                }
                else
                {
                    Logger.Warning("Filter checkbox NOT checked - filter not applied");
                }
            }
        }
        
        /// <summary>
        /// Apply multi-select filter combining all selected sets
        /// </summary>
        private void ApplyMultiSetFilter()
        {
            if (ViewSheetSets == null)
            {
                Logger.Error("ApplyMultiSetFilter: ViewSheetSets is null");
                return;
            }
            
            var selectedSets = ViewSheetSets.Where(s => s.IsSelected).ToList();
            Logger.Info($"===== APPLYING MULTI-SET FILTER with {selectedSets.Count} selected sets =====");
            
            try
            {
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                
                if (isSheetMode)
                {
                    // Combine sheets from all selected sets
                    var combinedSheetIds = new HashSet<ElementId>();
                    
                    if (selectedSets.Count == 0 || selectedSets.Any(s => s.Name.StartsWith("All Sheets")))
                    {
                        // Show all sheets if nothing selected or "All Sheets" selected
                        Logger.Info("Showing all sheets (no filter or All Sheets selected)");
                        var allSheets = _viewSheetSetManager.GetSheetsFromSet("All Sheets");
                        foreach (var sheet in allSheets)
                            combinedSheetIds.Add(sheet.Id);
                        Logger.Info($"Got {allSheets.Count} sheets from 'All Sheets'");
                    }
                    else
                    {
                        // Combine from selected sets
                        Logger.Info($"Combining sheets from {selectedSets.Count} selected sets");
                        foreach (var set in selectedSets)
                        {
                            Logger.Info($"Getting sheets from set: '{set.Name}'");
                            var sheets = _viewSheetSetManager.GetSheetsFromSet(set.Name);
                            Logger.Info($"  -> Found {sheets.Count} sheets in '{set.Name}'");
                            foreach (var sheet in sheets)
                                combinedSheetIds.Add(sheet.Id);
                        }
                    }
                    
                    Logger.Info($"Combined {combinedSheetIds.Count} unique sheets from selected sets");
                    
                    // Get all sheets and filter
                    var allProjectSheets = new FilteredElementCollector(_document)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(s => !s.IsTemplate)
                        .ToList();
                    
                    Logger.Info($"Total project sheets (non-template): {allProjectSheets.Count}");
                    Logger.Info($"Sheets collection before clear: {Sheets?.Count ?? 0}");
                    
                    if (Sheets == null)
                    {
                        Logger.Error("ERROR: Sheets collection is NULL!");
                        MessageBox.Show("Sheets collection is null. Please reload the form.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    Sheets.Clear();
                    Logger.Info("Sheets collection cleared");
                    
                    int addedCount = 0;
                    foreach (var sheet in allProjectSheets.Where(s => combinedSheetIds.Contains(s.Id)))
                    {
                        var sheetItem = CreateSheetItem(sheet);
                        Sheets.Add(sheetItem);
                        addedCount++;
                    }
                    
                    Logger.Info($"Added {addedCount} sheets to collection");
                    Logger.Info($"===== FINAL: Sheets.Count = {Sheets.Count} =====");
                    
                    // Show confirmation message
                    MessageBox.Show(
                        $"Filtered: {Sheets.Count} sheets displayed\nFrom {selectedSets.Count} selected set(s)",
                        "Filter Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Combine views from all selected sets
                    var combinedViewIds = new HashSet<ElementId>();
                    
                    if (selectedSets.Count == 0 || selectedSets.Any(s => s.Name.StartsWith("All Views")))
                    {
                        // Show all views if nothing selected or "All Views" selected
                        var allViews = _viewSheetSetManager.GetViewsFromSet("All Views");
                        foreach (var view in allViews)
                            combinedViewIds.Add(view.Id);
                    }
                    else
                    {
                        // Combine from selected sets
                        foreach (var set in selectedSets)
                        {
                            var views = _viewSheetSetManager.GetViewsFromSet(set.Name);
                            foreach (var view in views)
                                combinedViewIds.Add(view.Id);
                        }
                    }
                    
                    
                    // Get all views and filter
                    var allProjectViews = new FilteredElementCollector(_document)
                        .OfClass(typeof(RevitView))
                        .Cast<RevitView>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .ToList();
                    
                    Views.Clear();
                    
                    foreach (var view in allProjectViews.Where(v => combinedViewIds.Contains(v.Id)))
                    {
                        var viewItem = CreateViewItem(view);
                        Views.Add(viewItem);
                    }
                    
                    
                    // Show confirmation message
                    MessageBox.Show(
                        $"Filtered: {Views.Count} views displayed\nFrom {selectedSets.Count} selected set(s)",
                        "Filter Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to apply filter:\n\n{ex.Message}",
                    "Filter Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Apply View/Sheet Set filter to current list
        /// </summary>
        private void ApplyViewSheetSetFilter(ViewSheetSetInfo setInfo)
        {
            if (setInfo == null)
            {
                return;
            }
            
            
            try
            {
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                
                if (isSheetMode)
                {
                    // Filter sheets
                    var filteredSheets = _viewSheetSetManager.GetSheetsFromSet(setInfo.Name);
                    var filteredIds = new HashSet<ElementId>(filteredSheets.Select(s => s.Id));
                    
                    
                    // Get all sheets and filter
                    var allSheets = new FilteredElementCollector(_document)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(s => !s.IsTemplate)
                        .ToList();
                    
                    Sheets.Clear();
                    
                    foreach (var sheet in allSheets.Where(s => filteredIds.Contains(s.Id)))
                    {
                        var sheetItem = CreateSheetItem(sheet);
                        Sheets.Add(sheetItem);
                    }
                    
                }
                else
                {
                    // Filter views
                    var filteredViews = _viewSheetSetManager.GetViewsFromSet(setInfo.Name);
                    var filteredIds = new HashSet<ElementId>(filteredViews.Select(v => v.Id));
                    
                    
                    // Get all views and filter
                    var allViews = new FilteredElementCollector(_document)
                        .OfClass(typeof(RevitView))
                        .Cast<RevitView>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .ToList();
                    
                    Views.Clear();
                    
                    foreach (var view in allViews.Where(v => filteredIds.Contains(v.Id)))
                    {
                        var viewItem = CreateViewItem(view);
                        Views.Add(viewItem);
                    }
                    
                }
                
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to apply filter:\n\n{ex.Message}",
                    "Filter Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Helper method to create SheetItem from ViewSheet
        /// </summary>
        private SheetItem CreateSheetItem(ViewSheet sheet)
        {
            return new SheetItem
            {
                Id = sheet.Id,
                SheetNumber = sheet.SheetNumber ?? "",
                SheetName = sheet.Name ?? "",
                IsSelected = false,
                Revision = GetParameterValue(sheet, "Current Revision"),
                Size = sheet.get_Parameter(BuiltInParameter.SHEET_HEIGHT)?.AsValueString() ?? ""
            };
        }
        
        /// <summary>
        /// Helper method to create ViewItem from View
        /// </summary>
        private ViewItem CreateViewItem(RevitView view)
        {
            return new ViewItem
            {
                RevitViewId = view.Id,
                ViewId = view.Id.GetIdValue().ToString(),
                ViewName = view.Name ?? "",
                ViewType = view.ViewType.ToString(),
                Scale = view.Scale.ToString(),
                IsSelected = false
            };
        }
        
        /// <summary>
        /// Helper method to get parameter value
        /// </summary>
        private string GetParameterValue(Element element, string paramName)
        {
            try
            {
                var param = element.LookupParameter(paramName);
                if (param != null && param.HasValue)
                {
                    return param.AsString() ?? "";
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Could not read parameter '{paramName}': {ex.Message}");
            }
            return "";
        }
        
        #endregion View/Sheet Set Management
    }
}
