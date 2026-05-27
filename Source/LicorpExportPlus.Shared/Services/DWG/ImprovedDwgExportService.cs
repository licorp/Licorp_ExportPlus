using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services.DWG;

/// <summary>
/// Improved DWG Export Service - Learned from Licorp_Combi CAD.
/// Features:
/// - SmartScale auto-detect viewport scale
/// - XREF cleanup after export
/// - Cancellation support
/// - Progress reporting
/// - Multiple export modes
/// </summary>
public class ImprovedDwgExportService
{
    private readonly Document _document;

    public ImprovedDwgExportService(Document document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public List<string> GetAvailableExportSetups()
    {
        var setups = new List<string>();
        try
        {
            var collector = new FilteredElementCollector(_document)
                .OfClass(typeof(ExportDWGSettings));

            foreach (ExportDWGSettings setting in collector)
            {
                if (!string.IsNullOrEmpty(setting.Name))
                    setups.Add(setting.Name);
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[DwgExport] Error getting setups: {ex.Message}");
        }

        if (setups.Count == 0)
            setups.Add("(Default)");

        return setups;
    }

    public DWGExportOptions BuildExportOptions(ExportSettings settings)
    {
        DWGExportOptions options = null;

        if (!string.IsNullOrEmpty(settings.DWGExportSetupName)
            && settings.DWGExportSetupName != "(Default)")
        {
            try
            {
                var setup = new FilteredElementCollector(_document)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .FirstOrDefault(s => s.Name == settings.DWGExportSetupName);

                if (setup != null)
                    options = setup.GetDWGExportOptions();
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"[DwgExport] Error loading setup: {ex.Message}");
            }
        }

        if (options == null)
            options = new DWGExportOptions();

        ConfigureCleanExportOptions(options, settings);
        return options;
    }

    private void ConfigureCleanExportOptions(DWGExportOptions options, ExportSettings settings)
    {
        // Self-contained DWG (no XREF)
        TrySetProperty(options, "ExportingAreas", false);
        TrySetProperty(options, "MergedViews", true);
        options.SharedCoords = false;
        TrySetProperty(options, "ExportRoomsAndAreas", false);
        TrySetProperty(options, "PropOverrides", false);
        options.ExportOfSolids = SolidGeometry.Polymesh;

        // ACA preference
        var acaPrefType = typeof(DWGExportOptions).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.Name == "ACAObjectPreference");
        if (acaPrefType != null)
            TrySetProperty(options, "ACAPreference", Enum.Parse(acaPrefType, "Geometry"));

        // Units
        try
        {
            TrySetProperty(options, "TargetUnit", Enum.Parse(typeof(ExportUnit), "Millimeter"));
        }
        catch
        {
            TrySetProperty(options, "TargetUnit", ExportUnit.Default);
        }

        // Colors and line scaling
        TrySetProperty(options, "Colors", GetEnumValue("ExportColorMode", "IndexColors"));
        TrySetProperty(options, "LineScaling", GetEnumValue("LineScaling", "ViewScale"));

        // Hide options
        TrySetProperty(options, "HideReferencePlane", true);
        TrySetProperty(options, "HideScopeBox", true);
        TrySetProperty(options, "HideUnreferenceViewTags", true);
        TrySetProperty(options, "PreserveCoincidentLines", false);

        options.FileVersion = GetAcadVersion(settings.DWGVersion);
    }

    public DwgExportResult ExportSheetsIndividually(
        List<SheetItem> sheets, ExportSettings settings, DWGExportOptions options,
        bool enableSmartScale = false,
        IProgress<DwgExportProgressInfo> progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DwgExportResult();
        var totalTimer = Stopwatch.StartNew();
        SmartScaleService smartScaleService = null;

        if (enableSmartScale)
        {
            try
            {
                smartScaleService = new SmartScaleService(_document);
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"[DwgExport] SmartScale init failed: {ex.Message}");
            }
        }

        try
        {
            EnsureOutputFolder(settings.OutputFolder);

            for (int i = 0; i < sheets.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LicorpTrace.Info("[DwgExport] Export cancelled by user");
                    break;
                }

                var sheetItem = sheets[i];
                var viewSheet = _document.GetElement(sheetItem.Id) as ViewSheet;
                if (viewSheet == null)
                {
                    result.SkippedSheets.Add(sheetItem.SheetNumber);
                    continue;
                }

                try
                {
                    progress?.Report(new DwgExportProgressInfo
                    {
                        Phase = "Exporting",
                        CurrentItem = $"{sheetItem.SheetNumber} - {sheetItem.SheetName}",
                        Current = i + 1,
                        Total = sheets.Count
                    });

                    var sheetTimer = Stopwatch.StartNew();

                    // Apply SmartScale before export
                    if (smartScaleService != null)
                    {
                        using (var trans = new Transaction(_document, "Apply Smart Scale"))
                        {
                            try
                            {
                                trans.Start();
                                smartScaleService.ApplySmartScale(viewSheet, trans);
                                trans.Commit();
                            }
                            catch
                            {
                                if (trans.HasStarted())
                                    trans.RollBack();
                                throw;
                            }
                        }
                    }

                    var filePath = ExportSingleSheet(viewSheet, sheetItem, settings, options);
                    sheetTimer.Stop();

                    // Restore original scale
                    if (smartScaleService != null)
                    {
                        using (var trans = new Transaction(_document, "Restore Scale"))
                        {
                            trans.Start();
                            smartScaleService.RestoreOriginalScale(viewSheet, trans);
                            trans.Commit();
                        }
                    }

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        result.ExportedFiles.Add(filePath);
                        result.ExportedSheets.Add(sheetItem);
                        LicorpTrace.Info($"[DwgExport] {sheetItem.SheetNumber} exported in {sheetTimer.ElapsedMilliseconds}ms");
                    }
                    else
                    {
                        result.FailedSheets.Add(sheetItem.SheetNumber);
                    }
                }
                catch (Exception ex)
                {
                    // Restore scale on error
                    if (smartScaleService != null)
                    {
                        try
                        {
                            using (var trans = new Transaction(_document, "Restore Scale"))
                            {
                                trans.Start();
                                smartScaleService.RestoreOriginalScale(viewSheet, trans);
                                trans.Commit();
                            }
                        }
                        catch (Exception innerEx)
                        {
                            LicorpTrace.Warn($"[DwgExport] Failed to restore scale: {innerEx.Message}");
                        }
                    }
                    result.FailedSheets.Add(sheetItem.SheetNumber);
                    LicorpTrace.Error($"[DwgExport] Failed: {sheetItem.SheetNumber}: {ex.Message}", ex);
                }
            }
        }
        finally
        {
            smartScaleService?.ClearState();
        }

        totalTimer.Stop();
        LicorpTrace.Info($"[DwgExport] Total: {totalTimer.ElapsedMilliseconds}ms for {result.ExportedFiles.Count} sheets");

        if (result.FailedSheets.Count > 0)
            LicorpTrace.Warn($"[DwgExport] Failed sheets: {string.Join(", ", result.FailedSheets)}");

        return result;
    }

    private string ExportSingleSheet(ViewSheet viewSheet, SheetItem sheetItem, ExportSettings settings, DWGExportOptions options)
    {
        if (viewSheet == null) throw new ArgumentNullException(nameof(viewSheet));
        if (sheetItem == null) throw new ArgumentNullException(nameof(sheetItem));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
            throw new InvalidOperationException("OutputFolder is empty.");

        string fileName;
        try
        {
            fileName = FileNameHelper.SanitizeFileName(
                !string.IsNullOrWhiteSpace(sheetItem.CustomFileName)
                    ? sheetItem.CustomFileName
                    : $"{sheetItem.SheetNumber} - {sheetItem.SheetName}");
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[DwgExport] File-name generation failed for '{sheetItem.SheetNumber}': {ex}");
            throw;
        }

        var fullPath = Path.Combine(settings.OutputFolder, fileName + ".dwg");
        DeleteExportOutputIfExists(fullPath);

        try
        {
            ICollection<ElementId> sheetOnly = new List<ElementId> { viewSheet.Id };
            LicorpTrace.Info($"[DwgExport] Exporting {viewSheet.SheetNumber} to {fullPath}");

            bool success = _document.Export(settings.OutputFolder, fileName, sheetOnly, options);

            if (success && File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                LicorpTrace.Info($"[DwgExport] OK: {fileName}.dwg ({fi.Length / 1024} KB)");

                if (fi.Length < 1024)
                    LicorpTrace.Warn($"[DwgExport] WARNING: very small file ({fi.Length} bytes)");

                // Cleanup XREF files
                if (DWGCleanupService.HasXRefReferences(fullPath))
                {
                    int deleted = DWGCleanupService.CleanupXRefFiles(fullPath);
                    LicorpTrace.Info($"[DwgExport] Cleaned up {deleted} XREF companion files");
                }

                return fullPath;
            }

            if (success && !File.Exists(fullPath))
            {
                LicorpTrace.Warn($"[DwgExport] WARNING: success but file not found at {fullPath}");
                var possibleFiles = Directory.GetFiles(settings.OutputFolder, fileName + "*.dwg");
                if (possibleFiles.Length > 0)
                    return possibleFiles[0];
            }

            LicorpTrace.Warn($"[DwgExport] FAILED: {sheetItem.SheetNumber} - export returned {success}");
            return null;
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[DwgExport] Exception exporting {sheetItem.SheetNumber}: {ex}");
            return null;
        }
    }

    private static void TrySetProperty(DWGExportOptions options, string propertyName, object value)
    {
        try
        {
            var property = typeof(DWGExportOptions).GetProperty(propertyName);
            if (property != null && property.CanWrite)
                property.SetValue(options, value);
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[DwgExport] Failed to set {propertyName}: {ex.Message}");
        }
    }

    private static object GetEnumValue(string enumTypeName, string valueName)
    {
        try
        {
            var enumType = typeof(DWGExportOptions).Assembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == enumTypeName && t.IsEnum);

            if (enumType != null)
                return Enum.Parse(enumType, valueName);
        }
        catch { }

        return null;
    }

    private static ACADVersion GetAcadVersion(string version)
    {
        switch (version?.ToLower())
        {
            case "2018": return ACADVersion.R2018;
            case "2013": return ACADVersion.R2013;
            case "2010": return ACADVersion.R2010;
            case "2007": return ACADVersion.R2007;
            default: return ACADVersion.R2018;
        }
    }

    private void EnsureOutputFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"[DwgExport] Failed to create folder: {ex.Message}");
            }
        }
    }

    private static void DeleteExportOutputIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch (Exception ex)
        {
            var message = $"Cannot overwrite existing file: {path}";
            LicorpTrace.Error($"[DwgExport] {message}. {ex.Message}");
            throw new IOException(message, ex);
        }
    }
}

public class DwgExportResult
{
    public List<string> ExportedFiles { get; set; } = new();
    public List<SheetItem> ExportedSheets { get; set; } = new();
    public List<string> FailedSheets { get; set; } = new();
    public List<string> SkippedSheets { get; set; } = new();

    public bool HasWarnings => FailedSheets.Count > 0 || SkippedSheets.Count > 0;
    public int TotalProcessed => ExportedFiles.Count + FailedSheets.Count + SkippedSheets.Count;
    public string Summary => HasWarnings
        ? $"Exported {ExportedFiles.Count}, Failed {FailedSheets.Count}, Skipped {SkippedSheets.Count}"
        : $"Exported {ExportedFiles.Count} file(s) successfully";
}

public class DwgExportProgressInfo
{
    public string Phase { get; set; }
    public string CurrentItem { get; set; }
    public int Current { get; set; }
    public int Total { get; set; }
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}
