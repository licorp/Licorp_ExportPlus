using System;
using System.Collections.Generic;
using LicorpExportPlus.Models;
using RevitDB = Autodesk.Revit.DB;

namespace LicorpExportPlus.Services.Interfaces;

public interface IPDFExportService
{
    bool ExportSheetsToPDF(List<RevitDB.ViewSheet> sheets, string outputFolder,
        ExportSettings settings, Action<string, bool> progressCallback = null);

    bool ExportSheetsWithCustomNames(List<SheetItem> sheetItems, string outputFolder,
        ExportSettings settings, Action<int, int, string, bool> progressCallback = null);

    List<RevitDB.View> CollectPrintableViewsByType(PSPDFExportSettings settings);

    bool ExportViewsByType(string outputFolder, PSPDFExportSettings pdfSettings,
        Action<int, int, string, bool> progressCallback = null);
}

public interface IDWGExportService
{
    bool ExportToDWG(List<RevitDB.ViewSheet> sheets, PSDWGExportSettings settings,
        Func<RevitDB.ViewSheet, string> customFileNameResolver = null);
}

public interface IIFCExportService
{
    bool ExportToIFC(List<RevitDB.ViewSheet> sheets, IFCExportSettings settings,
        string outputPath, Action<string> logCallback = null);

    bool Export3DViewsToIFC(List<RevitDB.View3D> views, IFCExportSettings settings,
        string outputPath, Action<string> logCallback = null,
        Action<string, bool> progressCallback = null);

    List<RevitDB.View3D> Get3DViews();

    List<string> GetAvailableIFCSetups();

    IFCExportSettings LoadIFCSetupFromRevit(string setupName);
}

public interface INWCExportService
{
    bool ExportToNavisworks(List<ViewItem> selectedViews, NWCExportSettings settings,
        string outputFolder, string fileNamePrefix = "",
        Action<string, bool> progressCallback = null);

    bool ExportSheetsReference(List<SheetItem> selectedSheets, string outputFolder,
        string fileNamePrefix = "");
}

public interface IImageExportService
{
    void ExportToImages(List<RevitDB.ViewSheet> sheets, ImageExportSettings settings);
}

public interface IXMLExportService
{
    void ExportToXML(List<RevitDB.ViewSheet> sheets, string outputPath);
}

public interface IBatchExportService
{
    System.Threading.Tasks.Task<bool> ExportToPDF(List<RevitDB.ViewSheet> sheets,
        PSPDFExportSettings settings, IProgress<int> progress = null);

    System.Threading.Tasks.Task<bool> ExportToDWG(List<RevitDB.ViewSheet> sheets,
        PSDWGExportSettings settings, IProgress<int> progress = null);

    System.Threading.Tasks.Task<bool> ExportToIFC(List<RevitDB.ViewSheet> sheets,
        PSIFCExportSettings settings, IProgress<int> progress = null);
}

public interface IProfileService
{
    List<Profile> LoadAllProfiles();
    Profile LoadProfile(string profileName);
    void SaveProfile(Profile profile);
    void DeleteProfile(string profileName);
    Profile CreateDefaultProfile();
    void ExportProfile(Profile profile, string filePath);
    Profile ImportProfile(string filePath);
}

public interface ISchedulingService
{
    void AddScheduledExport(ScheduledExport export);
    void RemoveScheduledExport(string id);
    List<ScheduledExport> GetScheduledExports();
    event EventHandler<ScheduledExportEventArgs> ExportTriggered;
}

public interface IExportReportService
{
    string WriteCsvReport(IEnumerable<ExportQueueItem> items, string outputFolder);
    string WriteXlsxReport(IEnumerable<ExportQueueItem> items, string outputFolder);
}

public interface IDrawingTransmittalService
{
    void CreateTransmittal(List<RevitDB.ViewSheet> sheets, TransmittalSettings settings);
}

public interface IViewSheetSetService
{
    List<ViewSheetSetInfo> GetAllViewSheetSets();
    void SaveViewSheetSet(ViewSheetSetInfo setInfo);
    void DeleteViewSheetSet(string name);
}
