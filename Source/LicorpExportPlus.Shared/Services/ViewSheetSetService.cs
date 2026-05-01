using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;
using LicorpExportPlus.Utils;
using Nice3point.Revit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LicorpExportPlus.Services
{
    /// <summary>
    /// Manager for Revit View/Sheet Sets
    /// </summary>
    public class ViewSheetSetService
    {
        private readonly Document _doc;

        public ViewSheetSetService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Get all View/Sheet Sets from project
        /// Reads saved ViewSheetSets from the document
        /// </summary>
        public List<ViewSheetSetInfo> GetAllViewSheetSets()
        {
            var sets = new List<ViewSheetSetInfo>();

            try
            {
                // Add "All Sheets" built-in option
                var allSheetsSet = new ViewSheetSetInfo("All Sheets")
                {
                    IsBuiltIn = true
                };

                var allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .Select(s => s.Id)
                .ToList();

                allSheetsSet.SheetIds.AddRange(allSheets);
                sets.Add(allSheetsSet);

                // Add "All Views" built-in option
                var allViewsSet = new ViewSheetSetInfo("All Views")
                {
                    IsBuiltIn = true
                };

                var allViews = new FilteredElementCollector(_doc)
.OfClass(typeof(Autodesk.Revit.DB.View))
.Cast<Autodesk.Revit.DB.View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .Select(v => v.Id)
                .ToList();

                allViewsSet.ViewIds.AddRange(allViews);
                sets.Add(allViewsSet);

                // Get saved ViewSheetSets from document (created via Print dialog)
                var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet));

                LicorpTrace.Info($"Found {collector.GetElementCount()} ViewSheetSets in document");

                foreach (ViewSheetSet vss in collector)
                {
                    if (vss == null || string.IsNullOrEmpty(vss.Name))
                        continue;

                    LicorpTrace.Info($"Processing set: {vss.Name}");

                    var setInfo = new ViewSheetSetInfo(vss.Name)
                    {
                        IsBuiltIn = false
                    };

                    // Get views and sheets in this set using Views property
                    if (vss.Views != null && !vss.Views.IsEmpty)
                    {
                        LicorpTrace.Info($"ViewSet has {vss.Views.Size} items");

foreach (Autodesk.Revit.DB.View view in vss.Views)
{
if (view == null)
continue;

if (view is ViewSheet sheet)
{
setInfo.SheetIds.Add(sheet.Id);
                        LicorpTrace.Info($"- Sheet: {sheet.SheetNumber} - {sheet.Name}");
}
else if (view.CanBePrinted && !view.IsTemplate)
{
setInfo.ViewIds.Add(view.Id);
                        LicorpTrace.Info($"- View: {view.Name}");
}
}
                    }

                    LicorpTrace.Info($"Total: {setInfo.SheetIds.Count} sheets, {setInfo.ViewIds.Count} views");

                    // Add set even if empty (to show in dropdown)
                    sets.Add(setInfo);
                }

                LicorpTrace.Info($"Total sets loaded: {sets.Count}");
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"ERROR: {ex.Message}\n{ex.StackTrace}");
            }

            return sets;
        }

        /// <summary>
        /// Create new ViewSheetSet from selected sheets/views
        /// </summary>
        public ViewSheetSet CreateViewSheetSet(string name, List<ElementId> selectedIds)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Set name cannot be empty", nameof(name));

            if (selectedIds == null || selectedIds.Count == 0)
                throw new ArgumentException("Must select at least one sheet or view", nameof(selectedIds));

            using (var trans = new Transaction(_doc, "Create ViewSheetSet"))
            {
                trans.Start();

                try
                {
                    // Check if name already exists
                    var existing = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        throw new InvalidOperationException($"ViewSheetSet '{name}' already exists");
                    }

                    // Create ViewSet and add selected items
                    var printManager = _doc.PrintManager;
                    printManager.PrintRange = PrintRange.Select;

                    var viewSet = new ViewSet();

foreach (var id in selectedIds)
{
var view = id.ToElement<Autodesk.Revit.DB.View>(_doc);
if (view != null && view.CanBePrinted)
{
viewSet.Insert(view);
}
}

                    if (viewSet.IsEmpty)
                    {
                        throw new InvalidOperationException("No printable views selected");
                    }

                    // Create the ViewSheetSet
                    var viewSheetSetting = printManager.ViewSheetSetting;
                    viewSheetSetting.CurrentViewSheetSet.Views = viewSet;
                    viewSheetSetting.SaveAs(name);

                    trans.Commit();

                    // Return the newly created set
                    return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .FirstOrDefault(s => s.Name == name);
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    throw new InvalidOperationException($"Failed to create ViewSheetSet: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Get sheets from ViewSheetSet by name
        /// </summary>
        public List<ViewSheet> GetSheetsFromSet(string setName)
        {
            if (string.IsNullOrWhiteSpace(setName))
                return new List<ViewSheet>();

            try
            {
                // Handle built-in "All Sheets"
                if (setName.Equals("All Sheets", StringComparison.OrdinalIgnoreCase) ||
                setName.StartsWith("All Sheets ("))
                {
                    return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();
                }

                // Find the ViewSheetSet
                var vss = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .FirstOrDefault(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase) ||
                setName.StartsWith(s.Name + " ("));

                if (vss == null)
                    return new List<ViewSheet>();

                var sheets = new List<ViewSheet>();
foreach (Autodesk.Revit.DB.View view in vss.Views)
{
if (view is ViewSheet sheet && !sheet.IsTemplate)
sheets.Add(sheet);
}

                return sheets;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error getting sheets from set: {ex.Message}");
                return new List<ViewSheet>();
            }
        }

        /// <summary>
        /// Get views from ViewSheetSet by name
        /// </summary>
        public List<Autodesk.Revit.DB.View> GetViewsFromSet(string setName)
        {
            if (string.IsNullOrWhiteSpace(setName))
                return new List<Autodesk.Revit.DB.View>();

            try
            {
                // Handle built-in "All Views"
                if (setName.Equals("All Views", StringComparison.OrdinalIgnoreCase) ||
                    setName.StartsWith("All Views ("))
                {
                    return new FilteredElementCollector(_doc)
                        .OfClass(typeof(Autodesk.Revit.DB.View))
                        .Cast<Autodesk.Revit.DB.View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .ToList();
                }

                // Find the ViewSheetSet
                var vss = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .FirstOrDefault(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase) ||
                        setName.StartsWith(s.Name + " ("));

                if (vss == null)
                    return new List<Autodesk.Revit.DB.View>();

                var views = new List<Autodesk.Revit.DB.View>();
                foreach (Autodesk.Revit.DB.View view in vss.Views)
                {
                    if (view != null &&
                        !view.IsTemplate &&
                        view.CanBePrinted &&
                        !(view is ViewSheet))
                    {
                        views.Add(view);
                    }
                }

                return views;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error getting views from set: {ex.Message}");
                return new List<Autodesk.Revit.DB.View>();
            }
        }

        /// <summary>
        /// Get ViewSheetSet by name
        /// </summary>
        public ViewSheetSet GetViewSheetSetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewSheetSet))
            .Cast<ViewSheetSet>()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Add sheets/views to an existing ViewSheetSet
        /// </summary>
        public bool AddToExistingSet(string setName, List<ElementId> elementIds)
        {
            if (string.IsNullOrWhiteSpace(setName) || elementIds == null || !elementIds.Any())
                return false;

            try
            {
                var vss = GetViewSheetSetByName(setName);
                if (vss == null)
                {
                    LicorpTrace.Warn($"ViewSheetSet '{setName}' not found");
                    return false;
                }

                using (var trans = new Transaction(_doc, "Add to Existing ViewSheetSet"))
                {
                    trans.Start();

                    // Get current ViewSet
                    var currentViewSet = vss.Views;
                    var newViewSet = new ViewSet();

                    // Copy existing views to new ViewSet
foreach (Autodesk.Revit.DB.View existingView in currentViewSet)
{
newViewSet.Insert(existingView);
}

                    // Add new views/sheets
                    int addedCount = 0;
foreach (var id in elementIds)
{
var view = id.ToElement<Autodesk.Revit.DB.View>(_doc);
if (view != null && view.CanBePrinted)
{
// Check if not already in set
bool alreadyExists = false;
foreach (Autodesk.Revit.DB.View v in newViewSet)
{
if (v.Id == view.Id)
{
alreadyExists = true;
break;
}
}

                            if (!alreadyExists)
                            {
                                newViewSet.Insert(view);
                                addedCount++;
                            }
                        }
                    }

                    if (addedCount == 0)
                    {
                        trans.RollBack();
                        LicorpTrace.Info($"No new items to add to set '{setName}'");
                        return true; // Not an error, just nothing new to add
                    }

                    // Delete old set and create new one with updated content
                    // This is necessary because Revit doesn't allow direct modification of ViewSheetSet
                    _doc.Delete(vss.Id);

                    // Create new ViewSheetSet with same name
                    var printManager = _doc.PrintManager;
                    printManager.PrintRange = PrintRange.Select;
                    var viewSheetSetting = printManager.ViewSheetSetting;
                    viewSheetSetting.CurrentViewSheetSet.Views = newViewSet;
                    viewSheetSetting.SaveAs(setName);

                    trans.Commit();

                    LicorpTrace.Info($"Added {addedCount} items to set '{setName}'");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error adding to existing set: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Delete ViewSheetSet by name
        /// </summary>
        public bool DeleteViewSheetSet(string setName)
        {
            if (string.IsNullOrWhiteSpace(setName))
            {
                LicorpTrace.Warn($"DeleteViewSheetSet: setName is empty");
                return false;
            }

            try
            {
                LicorpTrace.Info($"Searching for ViewSheetSet to delete: '{setName}'");

                var vss = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .FirstOrDefault(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase));

if (vss == null)
{
                    LicorpTrace.Warn($"ViewSheetSet '{setName}' not found for deletion");
return false;
}

long vssIdValue;
                vssIdValue = vss.Id.GetIdValue();
                LicorpTrace.Info($"Found ViewSheetSet '{vss.Name}' (Id: {vssIdValue}), deleting...");

                using (var trans = new Transaction(_doc, "Delete ViewSheetSet"))
                {
                    trans.Start();

                    var deletedIds = _doc.Delete(vss.Id);
                    LicorpTrace.Info($"Document.Delete() returned {deletedIds.Count} deleted element IDs");

                    trans.Commit();
                    LicorpTrace.Info($"Transaction committed successfully");

                    // Verify deletion
                    var stillExists = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .Any(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase));

                    if (stillExists)
                    {
                        LicorpTrace.Error($"WARNING: ViewSheetSet '{setName}' still exists after deletion!");
                        return false;
                    }

                    LicorpTrace.Info($"ViewSheetSet '{setName}' successfully deleted and verified");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error deleting ViewSheetSet '{setName}': {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Check if ViewSheetSet name already exists
        /// </summary>
        public bool SetNameExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewSheetSet))
            .Cast<ViewSheetSet>()
            .Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
