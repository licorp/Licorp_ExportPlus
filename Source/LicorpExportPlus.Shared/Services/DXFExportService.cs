using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;

namespace LicorpExportPlus.Services
{
    public class DXFExportService
    {
        private readonly Document _document;

        public DXFExportService(Document document)
        {
            _document = document;
        }

        public bool ExportViewsToDXF(string outputFolder, PSDXFExportSettings settings, Action<int, int, string, bool> progressCallback = null)
        {
            try
            {
                LogTrace("===== DA4R-DxfExporter Pattern: Starting DXF Export =====");
                LogTrace($"Output folder: {outputFolder}");

                Directory.CreateDirectory(outputFolder);

                var viewIds = CollectViewsForExport(settings);

                if (viewIds == null || viewIds.Count == 0)
                {
                    LogTrace("⚠ No views found for export");
                    return false;
                }

                LogTrace($"✓ Collected {viewIds.Count} views for export");

                var exportOptions = new DXFExportOptions();

                string filePrefix = settings.UseDocumentTitle ? _document.Title :
                    (!string.IsNullOrEmpty(settings.CustomFilePrefix) ? settings.CustomFilePrefix : "Export");

                filePrefix = SanitizeFileName(filePrefix);

                LogTrace($"File prefix: {filePrefix}");
                LogTrace("Starting export...");

                bool exportSuccess = false;
                try
                {
                    _document.Export(outputFolder, filePrefix, viewIds, exportOptions);
                    exportSuccess = true;
                }
                catch (Exception ex)
                {
                    LogTrace($"DXF export call failed: {ex.Message}");
                    exportSuccess = false;
                }

                LogTrace($"✅ DXF Export completed successfully");
                LogTrace($"Output: {outputFolder}\\{filePrefix}*.dxf");

                progressCallback?.Invoke(viewIds.Count, viewIds.Count, "All views", exportSuccess);

                return exportSuccess;
            }
            catch (Autodesk.Revit.Exceptions.InvalidPathArgumentException ex)
            {
                LogTrace($"❌ Invalid path: {ex.Message}");
                return false;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                LogTrace($"❌ Invalid argument: {ex.Message}");
                return false;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                LogTrace($"❌ Invalid operation: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogTrace($"❌ Export failed: {ex.Message}");
                LogTrace($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private List<ElementId> CollectViewsForExport(PSDXFExportSettings settings)
        {
            LogTrace("Collecting views for export...");

            var viewElemIds = new List<ElementId>();

            try
            {
                if (settings.ExportAllViews)
                {
                    LogTrace("Mode: Export All Views (with filters)");

                    if (settings.Export3DViews)
                    {
                        using (var collector = new FilteredElementCollector(_document))
                        {
                            var view3dIds = collector
                                .WhereElementIsNotElementType()
                                .OfClass(typeof(View3D))
                                .Cast<Autodesk.Revit.DB.View>()
                                .Where(v => !v.IsTemplate || !settings.ExcludeTemplateViews)
                                .Select(v => v.Id);

                            viewElemIds.AddRange(view3dIds);
                            LogTrace($" + 3D Views: {view3dIds.Count()}");
                        }
                    }

                    if (settings.ExportPlanViews)
                    {
                        using (var collector = new FilteredElementCollector(_document))
                        {
                            var planIds = collector
                                .WhereElementIsNotElementType()
                                .OfClass(typeof(ViewPlan))
                                .Cast<Autodesk.Revit.DB.View>()
                                .Where(v => !v.IsTemplate || !settings.ExcludeTemplateViews)
                                .Select(v => v.Id);

                            viewElemIds.AddRange(planIds);
                            LogTrace($" + Plan Views: {planIds.Count()}");
                        }
                    }

                    if (settings.ExportSectionViews)
                    {
                        using (var collector = new FilteredElementCollector(_document))
                        {
                            var sectionIds = collector
                                .WhereElementIsNotElementType()
                                .OfClass(typeof(ViewSection))
                                .Cast<Autodesk.Revit.DB.View>()
                                .Where(v => !v.IsTemplate || !settings.ExcludeTemplateViews)
                                .Select(v => v.Id);

                            viewElemIds.AddRange(sectionIds);
                            LogTrace($" + Section Views: {sectionIds.Count()}");
                        }
                    }

                    if (settings.ExportSheetViews)
                    {
                        using (var collector = new FilteredElementCollector(_document))
                        {
                            var sheetIds = collector
                                .WhereElementIsNotElementType()
                                .OfClass(typeof(ViewSheet))
                                .Cast<Autodesk.Revit.DB.View>()
                                .Where(v => !v.IsTemplate || !settings.ExcludeTemplateViews)
                                .Select(v => v.Id);

                            viewElemIds.AddRange(sheetIds);
                            LogTrace($" + Sheet Views: {sheetIds.Count()}");
                        }
                    }
                }
                else
                {
                    LogTrace("Mode: Export Selected Views");
                    LogTrace("⚠ No views selected for export");
                }

                LogTrace($"Total views collected: {viewElemIds.Count}");
                return viewElemIds;
            }
            catch (Exception ex)
            {
                LogTrace($"Error collecting views: {ex.Message}");
                return new List<ElementId>();
            }
        }

        public bool ExportSpecificViews(List<ElementId> viewIds, string outputFolder, string filePrefix, Action<int, int, string, bool> progressCallback = null)
        {
            try
            {
                if (viewIds == null || viewIds.Count == 0)
                {
                    LogTrace("No views provided for export");
                    return false;
                }

                LogTrace($"===== Exporting {viewIds.Count} specific views to DXF =====");

                Directory.CreateDirectory(outputFolder);

                var exportOptions = new DXFExportOptions();

                filePrefix = SanitizeFileName(filePrefix);

                LogTrace($"Output: {outputFolder}\\{filePrefix}*.dxf");

                _document.Export(outputFolder, filePrefix, viewIds, exportOptions);

                LogTrace($"✅ Export completed");
                progressCallback?.Invoke(viewIds.Count, viewIds.Count, "Selected views", true);

                return true;
            }
            catch (Exception ex)
            {
                LogTrace($"❌ Export failed: {ex.Message}");
                return false;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            return FileNameHelper.SanitizeFileName(fileName);
        }

        private void LogTrace(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string fullMessage = $"[DXF Export] {timestamp} - {message}";
                LicorpTrace.Dbg(fullMessage);
            }
            catch
            {
                // Logging must never break export.
            }
        }
    }
}
