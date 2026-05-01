using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public class PDFExportService
    {
        private readonly Document _document;

        public PDFExportService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool ExportSheetsWithCustomNames(List<SheetItem> sheetItems, string outputFolder, ExportSettings settings, Action<int, int, string, bool> progressCallback = null)
        {
            if (sheetItems == null || sheetItems.Count == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(outputFolder);

                try
                {
                    using (var trans = new Transaction(_document, "Apply PDF View Options"))
                    {
                        trans.Start();

                        foreach (var sheetItem in sheetItems)
                        {
                            var sheet = _document.GetElement(sheetItem.Id) as ViewSheet;
                            if (sheet != null)
                            {
                                PDFOptionsApplier.ApplyViewOptionsToSheetNoTransaction(_document, sheet, settings);
                            }
                        }

                        trans.Commit();
                    }
                }
                catch (Exception viewEx)
                {
                    LicorpTrace.Warn($"ApplyViewOptions failed: {viewEx.Message}");
                }

#if REVIT2024_OR_GREATER
                var pdfOptions = CreatePDFExportOptions(settings);

                if (settings.CombineFiles && sheetItems.Count > 1)
                {
                    var viewSheets = sheetItems
                        .Select(item => _document.GetElement(item.Id) as ViewSheet)
                        .Where(sheet => sheet != null)
                        .ToList();

                    if (viewSheets.Count == 0)
                    {
                        return false;
                    }

                    Action<string, bool> combineCallback = null;
                    if (progressCallback != null)
                    {
                        combineCallback = (sheetNum, isCompleted) =>
                            progressCallback(viewSheets.Count, viewSheets.Count, sheetNum, isCompleted);
                    }

                    return ExportCombinedPDF(viewSheets, outputFolder, pdfOptions, settings, combineCallback);
                }

                int successCount = 0;
                int failCount = 0;
                int skippedCount = 0;
                int total = sheetItems.Count;

                for (int idx = 0; idx < sheetItems.Count; idx++)
                {
                    var sheetItem = sheetItems[idx];

                    try
                    {
                        var sheet = _document.GetElement(sheetItem.Id) as ViewSheet;
                        if (sheet == null)
                        {
                            failCount++;
                            continue;
                        }

                        if (ShouldSkipSheet(sheet, settings))
                        {
                            skippedCount++;
                            progressCallback?.Invoke(idx + 1, total, $"Skipped: {sheet.SheetNumber}", true);
                            continue;
                        }

                        int currentIndex = idx + 1;
                        progressCallback?.Invoke(currentIndex, total, sheet.SheetNumber, false);

                        string customFileName = FileNameHelper.SanitizeFileName(GetCustomOrDefaultFileName(sheetItem, sheet, settings));

                        var filesBeforeInfo = Directory.GetFiles(outputFolder, "*.pdf")
                            .Select(path => new FileInfo(path))
                            .ToDictionary(info => info.FullName, info => info.LastWriteTime);

                        DateTime exportStartTime = DateTime.Now;
                        pdfOptions.FileName = $"_TEMP_{Guid.NewGuid():N}";

                        _document.Export(outputFolder, new List<ElementId> { sheet.Id }, pdfOptions);

                        System.Threading.Thread.Sleep(500);

                        var filesAfter = Directory.GetFiles(outputFolder, "*.pdf");
                        string exportedFile = filesAfter.FirstOrDefault(file =>
                        {
                            var fileInfo = new FileInfo(file);
                            return !filesBeforeInfo.ContainsKey(fileInfo.FullName) || fileInfo.LastWriteTime > exportStartTime;
                        });

                        if (exportedFile == null)
                        {
                            var fileDetails = string.Join(", ", filesAfter.Select(f =>
                            {
                                var fi = new FileInfo(f);
                                return $"{Path.GetFileName(f)} (modified: {fi.LastWriteTime:HH:mm:ss.fff})";
                            }));

                            LicorpTrace.Warn($"PDF exported file not found. Files in folder: {fileDetails}");
                            failCount++;
                            continue;
                        }

                        string targetFile = Path.Combine(outputFolder, customFileName + ".pdf");

                        if (File.Exists(targetFile) && !string.Equals(exportedFile, targetFile, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(targetFile);
                        }

                        if (!string.Equals(exportedFile, targetFile, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(exportedFile, targetFile);
                        }

                        try
                        {
                            var pdfFile = new FileInfo(targetFile);
                            long fileSizeKB = pdfFile.Length / 1024;

                            if (fileSizeKB < 50)
                            {
                                LicorpTrace.Warn($"PDF file suspiciously small: {fileSizeKB} KB - {targetFile}");
                            }
                            else if (fileSizeKB > 5000)
                            {
                                LicorpTrace.Info($"PDF file size: {fileSizeKB} KB - {targetFile}");
                            }
                            else
                            {
                                LicorpTrace.Dbg($"PDF file size: {fileSizeKB} KB - {targetFile}");
                            }
                        }
                        catch (Exception qcEx)
                        {
                            LicorpTrace.Warn($"PDF quality check failed: {qcEx.Message}");
                        }

                        progressCallback?.Invoke(currentIndex, total, sheet.SheetNumber, true);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Error($"PDF export failed for sheet index {idx}", ex);
                        failCount++;
                    }
                }

                string summary = skippedCount > 0
                    ? $"PDF Export completed - Success: {successCount}, Failed: {failCount}, Skipped: {skippedCount} empty sheet(s)"
                    : $"PDF Export completed - Success: {successCount}, Failed: {failCount}";
                LicorpTrace.Info(summary);
                return successCount > 0;
#else
                var printService = new PDFPrintService(_document);
                return printService.ExportSheetsWithPrintManager(sheetItems, outputFolder, settings, progressCallback);
#endif
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("PDF export failed", ex);
                return false;
            }
        }

        private string GetCustomOrDefaultFileName(SheetItem sheetItem, ViewSheet sheet, ExportSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(sheetItem.CustomFileName))
            {
                return SanitizeFileName(sheetItem.CustomFileName);
            }

            return GenerateFileName(sheet, settings);
        }

        public bool ExportSheetsToPDF(List<ViewSheet> sheets, string outputFolder, ExportSettings settings, Action<string, bool> progressCallback = null)
        {
#if REVIT2024_OR_GREATER
            try
            {
                if (sheets == null || sheets.Count == 0)
                {
                    return false;
                }

                Directory.CreateDirectory(outputFolder);
                var pdfOptions = CreatePDFExportOptions(settings);

                if (settings.CombineFiles && sheets.Count > 1)
                {
                    return ExportCombinedPDF(sheets, outputFolder, pdfOptions, settings, progressCallback);
                }

                int successCount = 0;
                foreach (var sheet in sheets)
                {
                    bool success = false;

                    try
                    {
                        success = ExportSingleSheetToPDF(sheet, outputFolder, pdfOptions, settings);
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Error($"PDF export failed for {sheet.SheetNumber}", ex);
                    }

                    progressCallback?.Invoke($"{sheet.SheetNumber} - {sheet.Name}", success);
                    if (success) successCount++;
                }

                return successCount > 0;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("Sheet PDF export failed", ex);
                return false;
            }
#else
            try
            {
                if (sheets == null || sheets.Count == 0)
                {
                    return false;
                }

                var sheetItems = sheets.Select(sheet => new SheetItem
                {
                    Id = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    CustomFileName = GenerateFileName(sheet, settings)
                }).ToList();

                var printService = new PDFPrintService(_document);
                return printService.ExportSheetsWithPrintManager(sheetItems, outputFolder, settings,
                    (current, total, sheetNumber, completed) =>
                    {
                        if (completed)
                        {
                            progressCallback?.Invoke(sheetNumber, true);
                        }
                    });
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("Legacy sheet PDF export failed", ex);
                progressCallback?.Invoke("Legacy PDF export failed", false);
                return false;
            }
#endif
        }

#if REVIT2024_OR_GREATER
        private bool ExportCombinedPDF(List<ViewSheet> sheets, string outputFolder, PDFExportOptions pdfOptions, ExportSettings settings, Action<string, bool> progressCallback = null)
        {
            try
            {
                string combinedFileName = null;

                if (settings.CombineFileNameParameters != null && settings.CombineFileNameParameters.Count > 0 && sheets.Count > 0)
                {
                    combinedFileName = GenerateFileNameFromParameters(sheets[0], settings.CombineFileNameParameters);
                }

                if (string.IsNullOrEmpty(combinedFileName) && !string.IsNullOrEmpty(settings.CombineCustomFileName))
                {
                    combinedFileName = settings.CombineCustomFileName;
                }

                if (string.IsNullOrEmpty(combinedFileName))
                {
                    combinedFileName = _document.Title;
                }

                if (string.IsNullOrEmpty(combinedFileName))
                {
                    combinedFileName = sheets.Count > 0
                        ? $"{sheets[0].SheetNumber}_to_{sheets[sheets.Count - 1].SheetNumber}_Combined"
                        : "Combined_Sheets";
                }

                combinedFileName = FileNameHelper.SanitizeFileName(combinedFileName);

                var allSheetIds = sheets.Select(sheet => sheet.Id).ToList();
                pdfOptions.FileName = combinedFileName;
                _document.Export(outputFolder, allSheetIds, pdfOptions);

                string expectedFilePath = Path.Combine(outputFolder, combinedFileName + ".pdf");
                bool success = File.Exists(expectedFilePath);

                foreach (var sheet in sheets)
                {
                    progressCallback?.Invoke(sheet.SheetNumber, success);
                }

                return success;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("Combined PDF export failed", ex);

                if (progressCallback != null)
                {
                    foreach (var sheet in sheets)
                    {
                        progressCallback($"{sheet.SheetNumber} - {sheet.Name}", false);
                    }
                }

                return false;
            }
        }

        private bool ExportSingleSheetToPDF(ViewSheet sheet, string outputFolder, PDFExportOptions options, ExportSettings settings)
        {
            try
            {
                string fileName = GenerateFileName(sheet, settings);
                options.FileName = fileName;
                _document.Export(outputFolder, new List<ElementId> { sheet.Id }, options);
                return true;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Single sheet PDF export failed: {sheet.SheetNumber}", ex);
                return false;
            }
        }

        private PDFExportOptions CreatePDFExportOptions(ExportSettings settings)
        {
            var options = new PDFExportOptions
            {
                PaperFormat = ExportPaperFormat.Default,
                PaperOrientation = PageOrientationType.Auto
            };

            PDFOptionsApplier.ApplyNativePdfExportOptions(options, settings);
            return options;
        }
#endif

        private string GenerateFileName(ViewSheet sheet, ExportSettings settings)
        {
            try
            {
                ProjectInfo projectInfo = _document.ProjectInformation;
                string projectNumber = GetParameterValue(projectInfo, BuiltInParameter.PROJECT_NUMBER);
                string sheetNumber = sheet.SheetNumber ?? "Unknown";
                string sheetName = sheet.Name ?? "Untitled";
                string revision = GetSheetRevision(sheet);

                string fileName = "";

                if (!string.IsNullOrEmpty(projectNumber))
                {
                    fileName += SanitizeFileName(projectNumber) + "_";
                }

                fileName += SanitizeFileName(sheetNumber);

                if (!string.IsNullOrEmpty(sheetName))
                {
                    fileName += "_" + SanitizeFileName(sheetName);
                }

                if (!string.IsNullOrEmpty(revision))
                {
                    fileName += "_Rev" + SanitizeFileName(revision);
                }

                return fileName.Length > 200 ? fileName.Substring(0, 200) : fileName;
            }
            catch
            {
                return SanitizeFileName($"{sheet.SheetNumber}_{sheet.Name}");
            }
        }

        private string GetParameterValue(Element element, BuiltInParameter paramName)
        {
            try
            {
                return element.get_Parameter(paramName)?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetSheetRevision(ViewSheet sheet)
        {
            try
            {
                Parameter revParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                if (revParam != null && !string.IsNullOrEmpty(revParam.AsString()))
                {
                    return revParam.AsString();
                }

                revParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_DATE);
                return revParam?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "Unknown";
            }

            try
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                fileName = fileName.Replace(' ', '_').Replace('.', '_').Replace(',', '_').Replace(';', '_').Replace(':', '_');

                while (fileName.Contains("__"))
                {
                    fileName = fileName.Replace("__", "_");
                }

                fileName = fileName.Trim('_');
                return string.IsNullOrEmpty(fileName) ? "Unknown" : fileName;
            }
            catch
            {
                return "Unknown";
            }
        }

        public List<View> CollectPrintableViewsByType(PSPDFExportSettings settings)
        {
            try
            {
                var filteredViews = new FilteredElementCollector(_document)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(vw =>
                        !vw.IsTemplate &&
                        vw.CanBePrinted &&
                        ((settings.ExportDrawingSheets && vw.ViewType == ViewType.DrawingSheet) ||
                         (settings.Export3DViews && vw.ViewType == ViewType.ThreeD) ||
                         (settings.ExportDetailViews && vw.ViewType == ViewType.Detail) ||
                         (settings.ExportElevationViews && vw.ViewType == ViewType.Elevation) ||
                         (settings.ExportFloorPlanViews && vw.ViewType == ViewType.FloorPlan) ||
                         (settings.ExportSectionViews && vw.ViewType == ViewType.Section) ||
                         (settings.ExportRenderingViews && vw.ViewType == ViewType.Rendering)))
                    .ToList();

                if (settings.MaxViewsToExport > 0 && filteredViews.Count > settings.MaxViewsToExport)
                {
                    filteredViews = filteredViews.Take(settings.MaxViewsToExport).ToList();
                }

                return filteredViews;
            }
            catch
            {
                return new List<View>();
            }
        }

        public bool ExportViewsByType(string outputFolder, PSPDFExportSettings pdfSettings, Action<int, int, string, bool> progressCallback = null)
        {
#if REVIT2024_OR_GREATER
            try
            {
                List<View> views = CollectPrintableViewsByType(pdfSettings);
                if (views.Count == 0)
                {
                    return false;
                }

                Directory.CreateDirectory(outputFolder);

                var exportSettings = new ExportSettings
                {
                    Colors = PSColors.Color,
                    RasterQuality = PSRasterQuality.High,
                    HideCropBoundaries = pdfSettings.HideCropBoundaries,
                    HideScopeBoxes = pdfSettings.HideScopeBoxes,
                    HideUnreferencedViewTags = pdfSettings.HideUnreferencedViewTags,
                    CombineFiles = pdfSettings.CombineMultipleSheets
                };

                var pdfOptions = CreatePDFExportOptions(exportSettings);
                int successCount = 0;

                for (int i = 0; i < views.Count; i++)
                {
                    View view = views[i];
                    bool success = false;

                    try
                    {
                        pdfOptions.FileName = SanitizeFileName($"{view.ViewType}_{view.Name}");
                        _document.Export(outputFolder, new List<ElementId> { view.Id }, pdfOptions);
                        success = true;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Error($"PDF export failed for view {view.Name}", ex);
                    }

                    progressCallback?.Invoke(i + 1, views.Count, view.Name, success);
                }

                return successCount > 0;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("View PDF export failed", ex);
                return false;
            }
#else
            progressCallback?.Invoke(0, 0, "PDF export requires Revit 2024+", false);
            return false;
#endif
        }

        private string GenerateFileNameFromParameters(ViewSheet sheet, List<SelectedParameterInfo> parameterConfig)
        {
            if (parameterConfig == null || parameterConfig.Count == 0)
            {
                return null;
            }

            var parts = parameterConfig.Select(paramInfo =>
                {
                    string value = GetParameterValue(sheet, paramInfo.ParameterName);
                    if (string.IsNullOrEmpty(value))
                    {
                        value = paramInfo.ParameterName;
                    }

                    var part = $"{paramInfo.Prefix}{value}{paramInfo.Suffix}";
                    return string.IsNullOrEmpty(part) ? null : new { Part = part, Separator = paramInfo.Separator ?? "" };
                })
                .Where(part => part != null)
                .ToList();

            if (parts.Count == 0)
            {
                return null;
            }

            var fileName = "";
            for (int i = 0; i < parts.Count; i++)
            {
                fileName += parts[i].Part;
                if (i < parts.Count - 1)
                {
                    fileName += parts[i].Separator;
                }
            }

            return SanitizeFileName(fileName);
        }

        private string GetParameterValue(ViewSheet sheet, string parameterName)
        {
            try
            {
                Parameter param = sheet.LookupParameter(parameterName);
                if (param != null && param.HasValue)
                {
                    return param.AsValueString() ?? param.AsString() ?? "";
                }

                switch (parameterName)
                {
                    case "Sheet Number": return sheet.SheetNumber;
                    case "Sheet Name": return sheet.Name;
                    case "Current Revision":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "";
                    case "Current Revision Date":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_DATE)?.AsString() ?? "";
                    case "Current Revision Description":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_DESCRIPTION)?.AsString() ?? "";
                    case "Approved By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_APPROVED_BY)?.AsString() ?? "";
                    case "Checked By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CHECKED_BY)?.AsString() ?? "";
                    case "Designed By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_DESIGNED_BY)?.AsString() ?? "";
                    case "Drawn By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY)?.AsString() ?? "";
                    case "Sheet Issue Date":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE)?.AsString() ?? "";
                }

                if (parameterName.StartsWith("Project") || parameterName == "Client Name" || parameterName == "Author")
                {
                    Element projectInfo = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_ProjectInformation)
                        .FirstOrDefault();

                    if (projectInfo != null)
                    {
                        param = projectInfo.LookupParameter(parameterName);
                        if (param != null && param.HasValue)
                        {
                            return param.AsValueString() ?? param.AsString() ?? "";
                        }
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private static bool IsSheetEmpty(ViewSheet sheet)
        {
            try
            {
                return !new FilteredElementCollector(sheet.Document, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Any();
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldSkipSheet(ViewSheet sheet, ExportSettings settings)
        {
            if (settings != null && settings.SkipEmptySheets && IsSheetEmpty(sheet))
            {
                LicorpTrace.Info($"Skipping empty sheet: {sheet.SheetNumber} - {sheet.Name}");
                return true;
            }

            return false;
        }
    }
}
