using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using RevitDB = Autodesk.Revit.DB;

namespace LicorpExportPlus.Services
{
    public class DWGExportService
    {
        private readonly RevitDB.Document _document;

        public DWGExportService(RevitDB.Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        private RevitDB.DWGExportOptions GetDWGExportOptions(string setupName, PSDWGExportSettings settings)
        {
            try
            {
                RevitDB.ExportDWGSettings dwgSettings = RevitDB.ExportDWGSettings.FindByName(_document, setupName);

                if (dwgSettings != null)
                {
                    LicorpTrace.Info($"Using existing DWG setup: {setupName}");
                    RevitDB.DWGExportOptions options = dwgSettings.GetDWGExportOptions();

                    OverrideOptionsFromUI(options, settings);

                    return options;
                }
                else
                {
                    LicorpTrace.Warn($"Setup '{setupName}' not found, creating default options");
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error loading DWG setup: {ex.Message}");
            }

            return CreateDefaultDWGOptions(settings);
        }

        private void OverrideOptionsFromUI(RevitDB.DWGExportOptions options, PSDWGExportSettings settings)
        {
            try
            {
                LicorpTrace.Dbg($"ExportViewsOnSheets: {settings.ExportViewsOnSheets}");

                if (!settings.ExportViewsOnSheets)
                {
                    LicorpTrace.Info("Disabling all XREF export options");

                    TrySetProperty(options, "ExportingAreas", false);
                    TrySetProperty(options, "MergedViews", settings.CompactDwgFiles);
                    TrySetProperty(options, "ExportOfSolids", RevitDB.SolidGeometry.Polymesh);
                    TrySetProperty(options, "TargetUnit", RevitDB.ExportUnit.Default);

                    var acaPrefType = typeof(RevitDB.DWGExportOptions).Assembly
                        .GetTypes()
                        .FirstOrDefault(t => t.Name == "ACAObjectPreference");
                    if (acaPrefType != null)
                    {
                        var geometryValue = Enum.Parse(acaPrefType, "Geometry");
                        TrySetProperty(options, "ACAPreference", geometryValue);
                        LicorpTrace.Dbg("Set ACAPreference = Geometry");
                    }

                LicorpTrace.Info(settings.CompactDwgFiles
                    ? "DWG compact mode enabled - sheet views will be merged into self-contained DWG files"
                    : "DWG compact mode disabled - Revit may create companion XREF files");
                }
                else
                {
                    LicorpTrace.Info("Enabling ExportingAreas for XREF export");
                    TrySetProperty(options, "ExportingAreas", true);
                }

                options.FileVersion = GetDWGVersion(settings.DWGVersion);
                options.SharedCoords = settings.UseSharedCoordinates;

                TrySetProperty(options, "HideScopeBox", settings.HideScopeBox);
                TrySetProperty(options, "HideReferencePlane", settings.HideReferencePlane);
                TrySetProperty(options, "HideUnreferenceViewTags", settings.HideUnreferenceViewTags);
                TrySetProperty(options, "PreserveCoincidentLines", settings.PreserveCoincidentLines);

                LicorpTrace.Dbg($"FileVersion: {options.FileVersion}, SharedCoords: {options.SharedCoords}");
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error overriding DWG options: {ex.Message}");
            }
        }

        private void TrySetProperty(RevitDB.DWGExportOptions options, string propertyName, object value)
        {
            if (ReflectionHelper.TrySetProperty(options, propertyName, value))
            {
                LicorpTrace.Dbg($"Set {propertyName} = {value}");
            }
            else
            {
                LicorpTrace.Dbg($"Property {propertyName} not found or read-only");
            }
        }

        private RevitDB.DWGExportOptions CreateDefaultDWGOptions(PSDWGExportSettings settings)
        {
            var options = new RevitDB.DWGExportOptions();

            LicorpTrace.Dbg("Creating default DWG options");

            options.FileVersion = GetDWGVersion(settings.DWGVersion);
            options.SharedCoords = settings.UseSharedCoordinates;

            if (!settings.ExportViewsOnSheets)
            {
                LicorpTrace.Info("Disabling XREF export");
                TrySetProperty(options, "ExportingAreas", false);
                TrySetProperty(options, "MergedViews", settings.CompactDwgFiles);
                TrySetProperty(options, "ExportOfSolids", RevitDB.SolidGeometry.Polymesh);

                var acaPrefType = typeof(RevitDB.DWGExportOptions).Assembly
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == "ACAObjectPreference");
                if (acaPrefType != null)
                {
                    var geometryValue = Enum.Parse(acaPrefType, "Geometry");
                    TrySetProperty(options, "ACAPreference", geometryValue);
                }
            }
            else
            {
                TrySetProperty(options, "ExportingAreas", true);
            }

            TrySetProperty(options, "Colors", GetEnumValue("ExportColorMode", "TrueColorPerView"));

            return options;
        }

        private object GetEnumValue(string enumTypeName, string valueName)
        {
            return ReflectionHelper.TryGetEnumValueByShortName(typeof(RevitDB.DWGExportOptions).Assembly, enumTypeName, valueName);
        }

        public bool ExportToDWG(List<RevitDB.ViewSheet> sheets, PSDWGExportSettings settings)
        {
            try
            {
                LicorpTrace.Section("DWG Export - Sheet-Only");
                LicorpTrace.Info($"Sheets count: {sheets.Count}");
                LicorpTrace.Info($"Output folder: {settings.OutputFolder}");

                settings.ExportViewsOnSheets = false;

                RevitDB.DWGExportOptions dwgOptions = CreateSheetOnlyDWGOptions(settings);

                int successCount = 0;
                int failCount = 0;

                foreach (var sheet in sheets)
                {
                    try
                    {
                        LicorpTrace.Info($"Processing: {sheet.SheetNumber} - {sheet.Name}");

                        string fileName = GenerateDiRootsFileName(sheet);
                        LicorpTrace.Dbg($"Generated filename: {fileName}");

                        var outputPath = settings.CreateSubfolders
                            ? FileNameGenerator.GenerateSubfolderPath(sheet, _document, settings)
                            : settings.OutputFolder;

                        if (!Directory.Exists(outputPath))
                        {
                            Directory.CreateDirectory(outputPath);
                            LicorpTrace.Dbg($"Created directory: {outputPath}");
                        }

                        ICollection<RevitDB.ElementId> sheetOnly = new List<RevitDB.ElementId> { sheet.Id };

                        bool success = _document.Export(outputPath, fileName, sheetOnly, dwgOptions);

                        if (success)
                        {
                            successCount++;
                            LicorpTrace.Add($"DWG created: {outputPath}\\{fileName}.dwg");

                            string fullPath = Path.Combine(outputPath, fileName + ".dwg");
                            if (File.Exists(fullPath))
                            {
                                FileInfo fi = new FileInfo(fullPath);
                                LicorpTrace.Dbg($"File verified - Size: {fi.Length / 1024} KB");

                                bool hasXRefs = DWGCleanupService.HasXRefReferences(fullPath);

                                if (hasXRefs)
                                {
                                    LicorpTrace.Warn($"XREF files detected - attempting cleanup...");

                                    bool bindSuccess = AutoCADBindService.BindXRefsInDWG(fullPath, deleteXRefFiles: true);

                                    if (bindSuccess)
                                    {
                                        LicorpTrace.Add("AutoCAD BIND SUCCESS - XREFs merged into single file!");
                                    }
                                    else
                                    {
                                        LicorpTrace.Warn("AutoCAD BIND failed - multiple XREF files remain, manual cleanup needed");
                                    }
                                }
                                else
                                {
                                    LicorpTrace.Add("Clean single file export - no XREF files created");
                                }

                                var finalFi = new FileInfo(fullPath);
                                if (finalFi.Exists)
                                {
                                    LicorpTrace.Info($"Final: {Path.GetFileName(fullPath)} - {finalFi.Length / 1024} KB");
                                }
                            }
                            else
                            {
                                LicorpTrace.Warn("Export returned success but file not found!");
                            }
                        }
                        else
                        {
                            failCount++;
                            LicorpTrace.Error($"Document.Export() returned FALSE for {sheet.SheetNumber}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        LicorpTrace.Error($"DWG export exception for {sheet.SheetNumber}", ex);
                    }
                }

                LicorpTrace.Section("DWG Export Completed");
                LicorpTrace.Info($"Success: {successCount}, Failed: {failCount}");

                return successCount > 0;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("DWG export manager error", ex);
                return false;
            }
        }

        private string GenerateDiRootsFileName(RevitDB.ViewSheet sheet)
        {
            string fileName = $"{sheet.SheetNumber}-{sheet.Name}";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '-');
            }

            return fileName;
        }

        private RevitDB.DWGExportOptions CreateSheetOnlyDWGOptions(PSDWGExportSettings settings)
        {
            var options = new RevitDB.DWGExportOptions();
            LicorpTrace.Dbg("Creating sheet-only DWG options");

            TrySetProperty(options, "ExportingAreas", false);
            TrySetProperty(options, "MergedViews", settings.CompactDwgFiles);
            LicorpTrace.Info(settings.CompactDwgFiles
                ? "DWG compact mode enabled: MergedViews = true"
                : "DWG compact mode disabled: MergedViews = false");

            options.SharedCoords = false;
            LicorpTrace.Dbg("SharedCoords = FALSE (prevent view splitting)");

            TrySetProperty(options, "ExportRoomsAndAreas", false);
            TrySetProperty(options, "PropOverrides", false);

            options.ExportOfSolids = RevitDB.SolidGeometry.Polymesh;

            var acaPrefType = typeof(RevitDB.DWGExportOptions).Assembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == "ACAObjectPreference");
            if (acaPrefType != null)
            {
                var geometryValue = Enum.Parse(acaPrefType, "Geometry");
                TrySetProperty(options, "ACAPreference", geometryValue);
            }

            try
            {
                var targetUnit = Enum.Parse(typeof(RevitDB.ExportUnit), "Millimeter");
                TrySetProperty(options, "TargetUnit", targetUnit);
            }
            catch
            {
                TrySetProperty(options, "TargetUnit", RevitDB.ExportUnit.Default);
            }

            TrySetProperty(options, "Colors", GetEnumValue("ExportColorMode", "IndexColors"));
            TrySetProperty(options, "LineScaling", GetEnumValue("LineScaling", "ViewScale"));

            TrySetProperty(options, "HideReferencePlane", true);
            TrySetProperty(options, "HideScopeBox", true);
            TrySetProperty(options, "HideUnreferenceViewTags", true);

            options.FileVersion = GetDWGVersion(settings.DWGVersion);
            LicorpTrace.Info($"DWG options configured - FileVersion: {options.FileVersion}");

            return options;
        }

        private RevitDB.ACADVersion GetDWGVersion(string version)
        {
            switch (version?.ToLower())
            {
                case "2018": return RevitDB.ACADVersion.R2018;
                case "2013": return RevitDB.ACADVersion.R2013;
                case "2010": return RevitDB.ACADVersion.R2010;
                case "2007": return RevitDB.ACADVersion.R2007;
                default: return RevitDB.ACADVersion.R2018;
            }
        }

        private List<RevitDB.ElementId> UnloadAllLinkedModels()
        {
            var unloadedLinks = new List<RevitDB.ElementId>();

            try
            {
                var linkTypes = new RevitDB.FilteredElementCollector(_document)
                    .OfClass(typeof(RevitDB.RevitLinkType))
                    .Cast<RevitDB.RevitLinkType>()
                    .Where(lt => lt.GetLinkedFileStatus() == RevitDB.LinkedFileStatus.Loaded)
                    .ToList();

                LicorpTrace.Info($"Found {linkTypes.Count} loaded link types");

                if (linkTypes.Count == 0)
                {
                    LicorpTrace.Dbg("No linked models to unload");
                    return unloadedLinks;
                }

                using (RevitDB.Transaction trans = new RevitDB.Transaction(_document, "Unload Links for DWG Export"))
                {
                    trans.Start();

                    foreach (var linkType in linkTypes)
                    {
                        try
                        {
                            var linkName = linkType.Name;
                            LicorpTrace.Info($"Unloading: {linkName}");

                            linkType.Unload(null);
                            unloadedLinks.Add(linkType.Id);

                            LicorpTrace.Add($"Unloaded: {linkName}");
                        }
                        catch (Exception ex)
                        {
                            LicorpTrace.Warn($"Failed to unload {linkType.Name}: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                LicorpTrace.Info($"Successfully unloaded {unloadedLinks.Count} linked models");
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error unloading links: {ex.Message}");
            }

            return unloadedLinks;
        }

        private int ReloadLinkedModels(List<RevitDB.ElementId> linkIds)
        {
            int reloadedCount = 0;

            try
            {
                if (linkIds == null || linkIds.Count == 0)
                {
                    return 0;
                }

                using (RevitDB.Transaction trans = new RevitDB.Transaction(_document, "Reload Links after DWG Export"))
                {
                    trans.Start();

                    foreach (var linkId in linkIds)
                    {
                        try
                        {
                            var linkType = _document.GetElement(linkId) as RevitDB.RevitLinkType;
                            if (linkType != null)
                            {
                                LicorpTrace.Info($"Reloading: {linkType.Name}");

                                linkType.Reload();
                                reloadedCount++;

                                LicorpTrace.Add($"Reloaded: {linkType.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LicorpTrace.Warn($"Failed to reload link {linkId}: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                LicorpTrace.Info($"Successfully reloaded {reloadedCount} linked models");
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error reloading links: {ex.Message}");
            }

            return reloadedCount;
        }
    }
}
