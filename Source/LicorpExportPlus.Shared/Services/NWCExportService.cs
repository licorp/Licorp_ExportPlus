using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;

namespace LicorpExportPlus.Services
{
    public class NWCExportService
    {
        private readonly Document _document;

        public NWCExportService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool ExportToNavisworks(List<ViewItem> selectedViews, NWCExportSettings settings, string outputFolder, string fileNamePrefix = "", Action<string, bool> progressCallback = null)
        {
            try
            {
                if (selectedViews == null || !selectedViews.Any())
                {
                    LicorpTrace.Warn("No views selected for Navisworks export");
                    return false;
                }

                if (string.IsNullOrEmpty(outputFolder))
                {
                    LicorpTrace.Warn("Invalid output folder");
                    return false;
                }

                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                var threeDViews = selectedViews.Where(Is3DViewItem).ToList();

                if (!threeDViews.Any())
                {
                    LicorpTrace.Warn("No 3D views selected for Navisworks export. NWC export is limited to 3D views.");
                    selectedViews.ForEach(view => progressCallback?.Invoke(view.ViewName, false));
                    return false;
                }

                int exportedCount = 0;

                foreach (var viewItem in threeDViews)
                {
                    try
                    {
                        var view = _document.GetElement(viewItem.RevitViewId) as View3D;
                        if (view != null)
                        {
                            string fileName = !string.IsNullOrEmpty(viewItem.CustomFileName)
                                ? viewItem.CustomFileName
                                : $"{fileNamePrefix}{view.Name}";

                            fileName = CleanFileName(fileName);
                            string fullPath = Path.Combine(outputFolder, $"{fileName}.nwc");

                            var options = CreateNavisworksExportOptions(settings);
                            options.ExportScope = NavisworksExportScope.View;
                            options.ViewId = view.Id;

                            _document.Export(outputFolder, fileName, options);
                            exportedCount++;

                            progressCallback?.Invoke(viewItem.ViewName, true);
                        }
                    }
                    catch (Exception ex)
                    {
                    LicorpTrace.Error($"Error exporting view {viewItem.ViewName}: {ex.Message}");
                    LicorpTrace.Error($"Exception type: {ex.GetType().Name}");
                    LicorpTrace.Error($"Stack trace: {ex.StackTrace}");
                        progressCallback?.Invoke(viewItem.ViewName, false);
                    }
                }

                return exportedCount > 0;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Navisworks export error: {ex.Message}");
                LicorpTrace.Error($"Exception type: {ex.GetType().Name}");
                LicorpTrace.Error($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LicorpTrace.Error($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        private bool ExportModelToNavisworks(NWCExportSettings settings, string outputFolder, string fileNamePrefix)
        {
            try
            {
                string fileName = !string.IsNullOrEmpty(fileNamePrefix)
                    ? $"{fileNamePrefix}_Model"
                    : $"{_document.Title}_Model";

                fileName = CleanFileName(fileName);

                var options = CreateNavisworksExportOptions(settings);
                options.ExportScope = NavisworksExportScope.Model;

                _document.Export(outputFolder, fileName, options);
                return true;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Model export to Navisworks error: {ex.Message}");
                return false;
            }
        }

        public static bool Is3DViewItem(ViewItem viewItem)
        {
            if (viewItem?.ViewType == null)
            {
                return false;
            }

            return viewItem.ViewType.Contains("ThreeD") || viewItem.ViewType.Contains("3D");
        }

        private NavisworksExportOptions CreateNavisworksExportOptions(NWCExportSettings settings)
        {
            var options = new NavisworksExportOptions();

            try
            {
                options.ExportRoomGeometry = settings.ExportRoomGeometry;
                options.DivideFileIntoLevels = settings.DivideFileIntoLevels;
                options.ExportRoomAsAttribute = settings.ConvertRoomAsAttribute;
                options.ExportLinks = settings.ConvertLinkedFiles;
                options.ExportUrls = settings.ConvertURLs;
                options.FindMissingMaterials = settings.TryAndFindMissingMaterials;
                options.ConvertElementProperties = settings.ConvertElementProperties;

                switch (settings.ConvertElementParameters)
                {
                    case "None":
                        options.Parameters = NavisworksParameters.None;
                        break;
                    case "Elements":
                        options.Parameters = NavisworksParameters.Elements;
                        break;
                    case "All":
                    default:
                        options.Parameters = NavisworksParameters.All;
                        break;
                }

                switch (settings.Coordinates)
                {
                    case "Shared":
                        options.Coordinates = NavisworksCoordinates.Shared;
                        break;
                    case "Project Internal":
                    case "Internal":
                    default:
                        options.Coordinates = NavisworksCoordinates.Internal;
                        break;
                }

                try
                {
                    var type = options.GetType();

                    var convertLightsProperty = type.GetProperty("ConvertLights");
                    if (convertLightsProperty != null && convertLightsProperty.CanWrite)
                        convertLightsProperty.SetValue(options, settings.ConvertLights);

                    var convertLinkedCADProperty = type.GetProperty("ConvertLinkedCADFormats");
                    if (convertLinkedCADProperty != null && convertLinkedCADProperty.CanWrite)
                        convertLinkedCADProperty.SetValue(options, settings.ConvertLinkedCADFormats);

                    var facetingFactorProperty = type.GetProperty("FacetingFactor");
                    if (facetingFactorProperty != null && facetingFactorProperty.CanWrite)
                        facetingFactorProperty.SetValue(options, settings.FacetingFactor);

                    var convertElementIdsProperty = type.GetProperty("ConvertElementId");
                    if (convertElementIdsProperty != null && convertElementIdsProperty.CanWrite)
                        convertElementIdsProperty.SetValue(options, settings.ConvertElementIds);
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error creating Navisworks export options: {ex.Message}");
            }

            return options;
        }

        public bool ExportSheetsReference(List<SheetItem> selectedSheets, string outputFolder, string fileNamePrefix = "")
        {
            try
            {
                string fileName = !string.IsNullOrEmpty(fileNamePrefix)
                    ? $"{fileNamePrefix}_Sheets_Reference"
                    : "Sheets_Reference";

                fileName = CleanFileName(fileName);
                string fullPath = Path.Combine(outputFolder, $"{fileName}.txt");

                var content = new List<string>
                {
                    "Revit Sheets Reference for Navisworks",
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Document: {_document.Title}",
                    "",
                    "Selected Sheets:"
                };

                foreach (var sheet in selectedSheets)
                {
                    content.Add($"- {sheet.SheetNumber}: {sheet.SheetName}");
                }

                File.WriteAllLines(fullPath, content);
                return true;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Sheets reference export error: {ex.Message}");
                return false;
            }
        }

        public bool ExportSheetsReference(List<SheetItem> sheets, string outputFolder)
        {
            try
            {
                if (sheets?.Any() != true)
                    return false;

                string fileName = "Sheets_Reference";
                string filePath = Path.Combine(outputFolder, $"{fileName}.nwc");

                var collector = new FilteredElementCollector(_document);
                var view3D = collector.OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);

                if (view3D == null)
                {
                    return false;
                }

                var exportOptions = new NavisworksExportOptions();
                exportOptions.ExportScope = NavisworksExportScope.View;
                exportOptions.ViewId = view3D.Id;

                try
                {
                    _document.Export(outputFolder, fileName, exportOptions);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "Untitled";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            fileName = fileName.Replace(' ', '_')
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_');

            return fileName;
        }
    }
}
