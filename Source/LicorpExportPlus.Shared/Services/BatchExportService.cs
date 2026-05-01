using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using RevitDB = Autodesk.Revit.DB;

namespace LicorpExportPlus.Services
{
    public class BatchExportService
    {
        private readonly RevitDB.Document _document;

        public BatchExportService(RevitDB.Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public async Task<bool> ExportToPDF(List<RevitDB.ViewSheet> sheets, PSPDFExportSettings settings, IProgress<int> progress = null)
        {
            try
            {
                using var exportScope = RevitExecutionScope.Create();
                var pdfManager = new PDFExportService(_document);

                var exportSettings = new ExportSettings
                {
                    OutputFolder = settings.OutputFolder,
                    CreateSeparateFolders = settings.CreateSubfolders
                };

                int completed = 0;

                bool result = pdfManager.ExportSheetsToPDF(sheets, settings.OutputFolder, exportSettings,
                    (sheetName, success) =>
                    {
                        completed++;
                        progress?.Report((completed * 100) / sheets.Count);
                    });

                progress?.Report(100);
                await Task.Yield();

                return result;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"PDF Export Error: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> ExportToDWG(List<RevitDB.ViewSheet> sheets, PSDWGExportSettings settings, IProgress<int> progress = null)
        {
            try
            {
                using var exportScope = RevitExecutionScope.Create();
                var dwgOptions = new RevitDB.DWGExportOptions();
                ConfigureDwgOptions(dwgOptions, settings);

                for (int i = 0; i < sheets.Count; i++)
                {
                    var sheet = sheets[i];

                    var fileName = FileNameGenerator.GenerateFileName(sheet, _document, settings.FileNamingPattern, "dwg");
                    var outputPath = settings.CreateSubfolders
                        ? FileNameGenerator.GenerateSubfolderPath(sheet, _document, settings)
                        : settings.OutputFolder;

                    try
                    {
                        var singleViewIds = new List<RevitDB.ElementId> { sheet.Id };
                        _document.Export(outputPath, fileName.Replace(".dwg", ""), singleViewIds, dwgOptions);

                        progress?.Report(((i + 1) * 100) / sheets.Count);
                        await Task.Yield();
                    }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"DWG Export Error for sheet {sheet.SheetNumber}: {ex.Message}");
            }
                }

                return true;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"DWG Export Error: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> ExportToIFC(List<RevitDB.ViewSheet> sheets, PSIFCExportSettings settings, IProgress<int> progress = null)
        {
            try
            {
                using var exportScope = RevitExecutionScope.Create();
                var ifcOptions = new RevitDB.IFCExportOptions();
                ifcOptions.FileVersion = GetIFCVersion(settings.IFCVersion);
                ifcOptions.ExportBaseQuantities = settings.ExportBaseQuantities;

                var fileName = "ExportedModel.ifc";
                var filePath = Path.Combine(settings.OutputFolder, fileName);

                _document.Export(settings.OutputFolder, fileName, ifcOptions);

                progress?.Report(100);

                return true;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"IFC Export Error: {ex.Message}", ex);
                return false;
            }
        }

        private RevitDB.ColorDepthType ConvertColorDepth(PSColorDepth colorDepth)
        {
            switch (colorDepth)
            {
                case PSColorDepth.BlackLine:
                    return RevitDB.ColorDepthType.BlackLine;
                case PSColorDepth.GrayScale:
                    return RevitDB.ColorDepthType.GrayScale;
                case PSColorDepth.Color:
                    return RevitDB.ColorDepthType.Color;
                default:
                    return RevitDB.ColorDepthType.Color;
            }
        }



        private RevitDB.ACADVersion GetDWGVersion(string version)
        {
            switch (version)
            {
                case "2018": return RevitDB.ACADVersion.R2018;
                case "2013": return RevitDB.ACADVersion.R2013;
                case "2010": return RevitDB.ACADVersion.R2010;
                case "2007": return RevitDB.ACADVersion.R2007;
                default: return RevitDB.ACADVersion.R2018;
            }
        }

        private void ConfigureDwgOptions(RevitDB.DWGExportOptions options, PSDWGExportSettings settings)
        {
            options.FileVersion = GetDWGVersion(settings.DWGVersion);
            options.SharedCoords = settings.UseSharedCoordinates;
            options.ExportOfSolids = RevitDB.SolidGeometry.Polymesh;

            ReflectionHelper.TrySetProperty(options, "ExportingAreas", settings.ExportViewsOnSheets);
            ReflectionHelper.TrySetProperty(options, "MergedViews", settings.CompactDwgFiles);
            ReflectionHelper.TrySetProperty(options, "ExportRoomsAndAreas", false);
            ReflectionHelper.TrySetProperty(options, "PropOverrides", false);
            ReflectionHelper.TrySetProperty(options, "HideReferencePlane", settings.HideReferencePlane);
            ReflectionHelper.TrySetProperty(options, "HideScopeBox", settings.HideScopeBox);
            ReflectionHelper.TrySetProperty(options, "HideUnreferenceViewTags", settings.HideUnreferenceViewTags);
            ReflectionHelper.TrySetProperty(options, "PreserveCoincidentLines", settings.PreserveCoincidentLines);
        }

        private RevitDB.IFCVersion GetIFCVersion(PSIFCVersion version)
        {
            switch (version)
            {
                case PSIFCVersion.IFC2x3: return RevitDB.IFCVersion.IFC2x3CV2;
                case PSIFCVersion.IFC4: return RevitDB.IFCVersion.IFC4RV;
                default: return RevitDB.IFCVersion.IFC2x3CV2;
            }
        }
    }
}
