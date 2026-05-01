using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using LicorpExportPlus.Utils;

namespace LicorpExportPlus.Services
{
    public class IFCExportService
    {
        private Document _document;

        public IFCExportService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool ExportToIFC(List<ViewSheet> sheets, IFCExportSettings settings, string outputPath, Action<string> logCallback = null)
        {
            try
            {
                logCallback?.Invoke($"Starting IFC export with {sheets.Count} sheets");

                var ifcOptions = CreateIFCExportOptions(settings, logCallback);

                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                    logCallback?.Invoke($"Created output directory: {outputPath}");
                }

                using (Transaction trans = new Transaction(_document, "Export IFC"))
                {
                    trans.Start();

                    try
                    {
                        foreach (var sheet in sheets)
                        {
                            string fileName = SanitizeFileName(sheet.SheetNumber + "_" + sheet.Name);

                            logCallback?.Invoke($"Exporting sheet: {sheet.SheetNumber} - {sheet.Name}");

                            string fullPath = Path.Combine(outputPath, fileName + ".ifc");

                            using (Transaction t = new Transaction(_document, "IFC Export"))
                            {
                                t.Start();

                                bool success = ExportSingleSheet(sheet, fullPath, ifcOptions, logCallback);

                                if (success)
                                {
                                    logCallback?.Invoke($"✓ Exported: {fileName}.ifc");
                                }
                                else
                                {
                                    logCallback?.Invoke($"✗ Failed to export: {fileName}");
                                }

                                t.RollBack();
                            }
                        }

                        trans.RollBack();
                        logCallback?.Invoke($"IFC export completed: {sheets.Count} sheets processed");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        logCallback?.Invoke($"ERROR during export: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"IFC Export failed: {ex.Message}");
                LicorpTrace.Error($"IFC Export Error: {ex}");
                return false;
            }
        }

        private bool ExportSingleSheet(ViewSheet sheet, string filePath, IFCExportOptions options, Action<string> logCallback)
        {
            try
            {
                var viewIds = new List<ElementId> { sheet.Id };

                _document.Export(Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(filePath),
                    options);

                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Error exporting sheet: {ex.Message}");
                return false;
            }
        }

        private IFCExportOptions CreateIFCExportOptions(IFCExportSettings settings, Action<string> logCallback = null)
        {
            var options = new IFCExportOptions();

            try
            {
                logCallback?.Invoke("=== IFC Export Configuration (APS Enhanced) ===");

                options.FileVersion = ConvertIFCVersion(settings.IFCVersion);
                logCallback?.Invoke($"✓ IFC Version: {settings.IFCVersion}");

                options.SpaceBoundaryLevel = ConvertSpaceBoundaries(settings.SpaceBoundaries);
                logCallback?.Invoke($"✓ Space Boundaries: {settings.SpaceBoundaries} (Level {options.SpaceBoundaryLevel})");

                options.ExportBaseQuantities = settings.ExportBaseQuantities;
                logCallback?.Invoke($"✓ Export Base Quantities: {settings.ExportBaseQuantities}");

                if (settings.ExportUserDefinedPsets && !string.IsNullOrEmpty(settings.ExportUserDefinedPsetsFileName))
                {
                    string psetsFile = settings.ExportUserDefinedPsetsFileName;

                    if (!Path.IsPathRooted(psetsFile))
                    {
                        psetsFile = Path.Combine(Path.GetDirectoryName(_document.PathName) ?? "", psetsFile);
                    }

                    if (File.Exists(psetsFile))
                    {
                        try
                        {
                            options.AddOption("ExportUserDefinedPsets", "true");
                            options.AddOption("ExportUserDefinedPsetsFileName", psetsFile);

                            logCallback?.Invoke($"✓ User Defined Property Sets: ENABLED");
                            logCallback?.Invoke($" └─ File: {Path.GetFileName(psetsFile)}");
                        }
                        catch (Exception ex)
                        {
                            logCallback?.Invoke($"⚠ Warning: Could not set property sets file: {ex.Message}");
                        }
                    }
                    else
                    {
                        logCallback?.Invoke($"⚠ Warning: Property sets file not found: {psetsFile}");
                    }
                }

                if (settings.ExportParameterMapping && !string.IsNullOrEmpty(settings.ExportParameterMappingFileName))
                {
                    string mappingFile = settings.ExportParameterMappingFileName;

                    if (!Path.IsPathRooted(mappingFile))
                    {
                        mappingFile = Path.Combine(Path.GetDirectoryName(_document.PathName) ?? "", mappingFile);
                    }

                    if (File.Exists(mappingFile))
                    {
                        try
                        {
                            options.AddOption("ExportUserDefinedParameterMapping", "true");
                            options.AddOption("ExportUserDefinedParameterMappingFileName", mappingFile);

                            logCallback?.Invoke($"✓ Parameter Mapping: ENABLED");
                            logCallback?.Invoke($" └─ File: {Path.GetFileName(mappingFile)}");
                        }
                        catch (Exception ex)
                        {
                            logCallback?.Invoke($"⚠ Warning: Could not set parameter mapping: {ex.Message}");
                        }
                    }
                    else
                    {
                        logCallback?.Invoke($"⚠ Warning: Parameter mapping file not found: {mappingFile}");
                    }
                }

                if (settings.VisibleElementsOfCurrentView)
                {
                    try
                    {
                        options.AddOption("VisibleElementsOfCurrentView", "true");
                        logCallback?.Invoke($"✓ Visible Elements Only: ENABLED");
                    }
                    catch { }
                }

                if (settings.UseActiveViewGeometry)
                {
                    try
                    {
                        options.AddOption("UseActiveViewGeometry", "true");
                        logCallback?.Invoke($"✓ Use Active View Geometry: ENABLED");
                    }
                    catch { }
                }

                try
                {
                    options.WallAndColumnSplitting = settings.SplitWallsByLevel;
                    logCallback?.Invoke($"✓ Split Walls/Columns by Level: {settings.SplitWallsByLevel}");
                }
                catch { }

                try
                {
                    options.AddOption("ExportLinkedFiles", settings.ExportLinkedFiles.ToString());
                    logCallback?.Invoke($"✓ Export Linked Files: {settings.ExportLinkedFiles}");
                }
                catch { }

                try
                {
                    options.AddOption("StoreIFCGUID", settings.StoreIFCGUID.ToString());
                    if (settings.StoreIFCGUID)
                    {
                        logCallback?.Invoke($"✓ Store IFC GUID: ENABLED (GUIDs will be saved to Revit model)");
                    }
                }
                catch { }

                logCallback?.Invoke("=== IFC Export Options Applied Successfully ===");
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"⚠ Warning: Some IFC options not set: {ex.Message}");
            }

            return options;
        }

        private Autodesk.Revit.DB.IFCVersion ConvertIFCVersion(string version)
        {
            switch (version)
            {
                case "IFC 2x3 Coordination View 2.0":
                case "IFC 2x3 Coordination View":
                    return Autodesk.Revit.DB.IFCVersion.IFC2x3CV2;

                case "IFC 4 Reference View":
                    return Autodesk.Revit.DB.IFCVersion.IFC4RV;

                case "IFC 4 Design Transfer View":
                    return Autodesk.Revit.DB.IFCVersion.IFC4DTV;

                case "IFC 2x2":
                    return Autodesk.Revit.DB.IFCVersion.IFC2x2;

                case "IFC 4":
                    return Autodesk.Revit.DB.IFCVersion.IFC4;

                default:
                    return Autodesk.Revit.DB.IFCVersion.IFC2x3CV2;
            }
        }

        private int ConvertSpaceBoundaries(string spaceBoundaries)
        {
            switch (spaceBoundaries)
            {
                case "None":
                    return 0;
                case "1st Level":
                    return 1;
                case "2nd Level":
                    return 2;
                default:
                    return 0;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            fileName = fileName.Replace(':', '-');
            fileName = fileName.Replace('/', '-');
            fileName = fileName.Replace('\\', '-');

            return fileName;
        }

        public List<View3D> Get3DViews()
        {
            var views3D = new FilteredElementCollector(_document)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => !v.IsTemplate)
                .ToList();

            return views3D;
        }

        public bool Export3DViewsToIFC(List<View3D> views, IFCExportSettings settings, string outputPath, Action<string> logCallback = null, Action<string, bool> progressCallback = null)
        {
            try
            {
                logCallback?.Invoke($"Starting IFC export with {views.Count} 3D views");

                var ifcOptions = CreateIFCExportOptions(settings, logCallback);

                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                int successCount = 0;
                int failCount = 0;

                using (Transaction trans = new Transaction(_document, "Export IFC"))
                {
                    trans.Start();

                    try
                    {
foreach (var view in views)
{
string fileName = SanitizeFileName(view.Name);
string fullPath = Path.Combine(outputPath, fileName + ".ifc");

long viewIdValue = view.Id.GetIdValue();
logCallback?.Invoke($"Exporting 3D view: {view.Name} (ID: {viewIdValue})");

try
{
var viewSpecificOptions = CreateIFCExportOptions(settings, null);

viewSpecificOptions.FilterViewId = view.Id;
logCallback?.Invoke($" Set FilterViewId: {viewIdValue}");

_document.Export(Path.GetDirectoryName(fullPath),
                                    Path.GetFileNameWithoutExtension(fullPath),
                                    viewSpecificOptions);

                                if (File.Exists(fullPath))
                                {
                                    var fileInfo = new FileInfo(fullPath);
                                    logCallback?.Invoke($"✓ Exported: {fileName}.ifc ({fileInfo.Length / 1024} KB)");
                                    successCount++;

                                    progressCallback?.Invoke(view.Name, true);
                                }
                                else
                                {
                                    logCallback?.Invoke($"✗ Export failed: File not created for {view.Name}");
                                    failCount++;

                                    progressCallback?.Invoke(view.Name, false);
                                }
                            }
                            catch (Exception ex)
                            {
                                logCallback?.Invoke($"✗ Failed to export {view.Name}: {ex.Message}");
                                logCallback?.Invoke($" Exception Type: {ex.GetType().Name}");
                                if (ex.InnerException != null)
                                {
                                    logCallback?.Invoke($" Inner Exception: {ex.InnerException.Message}");
                                }
                                failCount++;

                                progressCallback?.Invoke(view.Name, false);
                            }
                        }

                        trans.Commit();
                        logCallback?.Invoke($"Transaction committed successfully");
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        logCallback?.Invoke($"Transaction rolled back due to error: {ex.Message}");
                        throw;
                    }
                }

                logCallback?.Invoke($"IFC export completed: {successCount} succeeded, {failCount} failed");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"IFC Export failed: {ex.Message}");
                logCallback?.Invoke($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        public static List<string> GetAvailableIFCSetups(Document document)
        {
            var setupNames = new List<string>();

            LicorpTrace.Info($"GetAvailableIFCSetups() CALLED - Using ExtensibleStorage");

            try
            {
                setupNames.Add("<In-Session Setup>");
                LicorpTrace.Info("Added: <In-Session Setup>");

                LicorpTrace.Info("========== READING FROM EXTENSIBLE STORAGE ==========");

                try
                {
                    Guid jsonSchemaId = new Guid("C2A3E6FE-CE51-4F35-8FF1-20C34567B687");
                    Guid oldSchemaId = new Guid("DCB88B13-594F-44F6-8F5D-AE9477305AC3");

                    Schema jsonSchema = Schema.Lookup(jsonSchemaId);
                    Schema oldSchema = Schema.Lookup(oldSchemaId);

                    LicorpTrace.Info($"JSON Schema found: {jsonSchema != null}");
                    LicorpTrace.Info($"Old Schema found: {oldSchema != null}");

                    int customCount = 0;

                    if (jsonSchema != null)
                    {
                        LicorpTrace.Info("Using JSON Schema...");

                        FilteredElementCollector collector = new FilteredElementCollector(document);
                        var dataStorages = collector.OfClass(typeof(DataStorage)).Cast<DataStorage>();

                        foreach (DataStorage storage in dataStorages)
                        {
                            Entity entity = storage.GetEntity(jsonSchema);
                            if (entity != null && entity.IsValid())
                            {
                                try
                                {
                                    string configData = entity.Get<string>("MapField");
                                    if (!string.IsNullOrEmpty(configData))
                                    {
                                        var configDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(configData);
                                        if (configDict != null && configDict.ContainsKey("Name"))
                                        {
                                            string configName = configDict["Name"].ToString();
                                            if (!setupNames.Contains(configName))
                                            {
                                                setupNames.Add(configName);
                                                customCount++;
                                                LicorpTrace.Info($" [{customCount}] {configName} (from JSON)");
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LicorpTrace.Error($" Error parsing JSON config: {ex.Message}");
                                }
                            }
                        }
                    }

                    if (oldSchema != null && customCount == 0)
                    {
                        LicorpTrace.Info("Using Old MapField Schema...");

                        FilteredElementCollector collector = new FilteredElementCollector(document);
                        var dataStorages = collector.OfClass(typeof(DataStorage)).Cast<DataStorage>();

                        foreach (DataStorage storage in dataStorages)
                        {
                            Entity entity = storage.GetEntity(oldSchema);
                            if (entity != null && entity.IsValid())
                            {
                                try
                                {
                                    var configMap = entity.Get<IDictionary<string, string>>("MapField");
                                    if (configMap != null && configMap.ContainsKey("Name"))
                                    {
                                        string configName = configMap["Name"];
                                        if (!setupNames.Contains(configName))
                                        {
                                            setupNames.Add(configName);
                                            customCount++;
                                            LicorpTrace.Info($" [{customCount}] {configName} (from MapField)");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LicorpTrace.Error($" Error reading MapField config: {ex.Message}");
                                }
                            }
                        }
                    }

                    LicorpTrace.Info($"Found {customCount} custom configurations in document");
                }
                catch (Exception ex)
                {
                    LicorpTrace.Error($"ERROR reading ExtensibleStorage: {ex.Message}");
                    LicorpTrace.Error($" Type: {ex.GetType().Name}");
                    LicorpTrace.Error($" Stack: {ex.StackTrace}");
                }

                LicorpTrace.Info("========== ADDING BUILT-IN CONFIGURATIONS ==========");

                List<string> builtInSetups = new List<string>
                {
                    "IFC 2x3 Coordination View 2.0",
                    "IFC 2x3 Coordination View",
                    "IFC 2x3 GSA Concept Design BIM 2010",
                    "IFC 2x3 Basic FM Handover View",
                    "IFC 2x2 Coordination View",
                    "IFC 2x2 Singapore BCA e-Plan Check",
                    "IFC 2x3 COBie 2.4 Design Deliverable View",
                    "IFC4 Reference View",
                    "IFC4 Design Transfer View"
                };

                foreach (string builtIn in builtInSetups)
                {
                    if (!setupNames.Contains(builtIn))
                    {
                        setupNames.Add(builtIn);
                    }
                }

                LicorpTrace.Info($"Added {builtInSetups.Count} built-in setups");
                LicorpTrace.Info($"FINAL TOTAL: {setupNames.Count} setups");

                for (int i = 0; i < setupNames.Count; i++)
                {
                    LicorpTrace.Info($" [{i+1}] {setupNames[i]}");
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"OUTER ERROR: {ex.Message}");
                LicorpTrace.Error($" Stack: {ex.StackTrace}");

                if (setupNames.Count == 0)
                {
                    setupNames.Add("<In-Session Setup>");
                }
            }

            return setupNames;
        }

        public static IFCExportSettings LoadIFCSetupFromRevit(Document document, string setupName)
        {
            var settings = new IFCExportSettings();

            try
            {
                if (setupName == "<In-Session Setup>")
                {
                    return settings;
                }

                try
                {
                    var ifcExportConfigType = Type.GetType("BIM.IFC.Export.UI.IFCExportConfigurationsMap, RevitIFCUI");

                    if (ifcExportConfigType != null)
                    {
                        var getMethod = ifcExportConfigType.GetMethod("GetStoredConfigurations",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                        if (getMethod != null)
                        {
                            var configs = getMethod.Invoke(null, new object[] { document }) as IDictionary<string, object>;

                            if (configs != null && configs.ContainsKey(setupName))
                            {
                                var config = configs[setupName];

                                var configType = config.GetType();

                                var ifcVersionProp = configType.GetProperty("IFCVersion");
                                if (ifcVersionProp != null)
                                {
                                    var versionValue = ifcVersionProp.GetValue(config);
                                    settings.IFCVersion = versionValue?.ToString() ?? "IFC 2x3 Coordination View 2.0";
                                }

                                var spaceBoundariesProp = configType.GetProperty("SpaceBoundaries");
                                if (spaceBoundariesProp != null)
                                {
                                    var spaceBoundValue = spaceBoundariesProp.GetValue(config);
                                    settings.SpaceBoundaries = spaceBoundValue?.ToString() ?? "None";
                                }

                                var exportBaseQtyProp = configType.GetProperty("ExportBaseQuantities");
                                if (exportBaseQtyProp != null)
                                {
                                    var baseQtyValue = exportBaseQtyProp.GetValue(config);
                                    settings.ExportBaseQuantities = baseQtyValue is bool ? (bool)baseQtyValue : false;
                                }

                                return settings;
                            }
                        }
                    }
                }
                catch
                {
                }

                settings = CreateDefaultSetupSettings(setupName);
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error loading IFC setup '{setupName}': {ex.Message}");
            }

            return settings;
        }

        private static IFCExportSettings CreateDefaultSetupSettings(string setupName)
        {
            var settings = new IFCExportSettings();

            var cleanName = setupName.Replace(" Setup>", "").Replace("Setup>", "");

            if (cleanName.Contains("IFC 2x3 Coordination View 2.0") || setupName.Contains("IFC 2x3 Coordination View 2.0"))
            {
                settings.IFCVersion = "IFC 2x3 Coordination View 2.0";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = false;
                settings.SplitWallsByLevel = true;
            }
            else if (cleanName.Contains("IFC 2x3 GSA") || setupName.Contains("IFC 2x3 GSA"))
            {
                settings.IFCVersion = "IFC 2x3 GSA Concept Design BIM 2010";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = true;
                settings.ExportBoundingBox = true;
            }
            else if (cleanName.Contains("IFC 2x3 Basic FM") || setupName.Contains("IFC 2x3 Basic FM"))
            {
                settings.IFCVersion = "IFC 2x3 Basic FM Handover View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = true;
                settings.SpaceBoundaries = "1st Level";
                settings.ExportRoomsIn3DViews = true;
            }
            else if (cleanName.Contains("IFC 2x3 COBie") || setupName.Contains("IFC 2x3 COBie"))
            {
                settings.IFCVersion = "IFC 2x3 COBie 2.4 Design Deliverable View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = true;
                settings.SpaceBoundaries = "2nd Level";
                settings.ExportRoomsIn3DViews = true;
            }
            else if (cleanName.Contains("IFC4 Reference View") || setupName.Contains("IFC4 Reference View"))
            {
                settings.IFCVersion = "IFC4 Reference View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = false;
            }
            else if (cleanName.Contains("IFC4 Design Transfer") || setupName.Contains("IFC4 Design Transfer"))
            {
                settings.IFCVersion = "IFC4 Design Transfer View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = true;
            }
            else if (cleanName.Contains("IFC 2x3 Coordination View") || setupName.Contains("IFC 2x3 Coordination View"))
            {
                settings.IFCVersion = "IFC 2x3 Coordination View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = false;
            }
            else if (cleanName.Contains("IFC 2x2 Coordination View") || setupName.Contains("IFC 2x2 Coordination View"))
            {
                settings.IFCVersion = "IFC 2x2 Coordination View";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = false;
            }
            else if (cleanName.Contains("IFC 2x2 Singapore") || setupName.Contains("IFC 2x2 Singapore"))
            {
                settings.IFCVersion = "IFC 2x2 Singapore BCA e-Plan Check";
                settings.FileType = "IFC";
                settings.ExportBaseQuantities = true;
            }
            else if (cleanName.Contains("Typical") || setupName.Contains("Typical"))
            {
                settings.IFCVersion = "IFC 2x3 Coordination View 2.0";
                settings.FileType = "IFC";
                settings.DetailLevel = "Medium";
            }
            else
            {
                settings.IFCVersion = "IFC 2x3 Coordination View 2.0";
                settings.FileType = "IFC";
            }

            return settings;
        }
    }
}
