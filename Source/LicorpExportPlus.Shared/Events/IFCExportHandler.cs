using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LicorpExportPlus.Events
{
    public class IFCExportHandler : IExternalEventHandler
    {
        public Document Document { get; set; }
        public List<View3D> Views3D { get; set; }
        public Models.IFCExportSettings Settings { get; set; }
        public string OutputFolder { get; set; }
        public Action<string> LogCallback { get; set; }
        public Action<string, bool> ProgressCallback { get; set; }
        public Action<bool> CompletionCallback { get; set; }
        public bool ExportResult { get; private set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (Document == null || Views3D == null || Views3D.Count == 0)
                {
                    LogCallback?.Invoke("❌ IFC Export: Invalid parameters");
                    ExportResult = false;
                    return;
                }

                LogCallback?.Invoke($"[IFC ExternalEvent] Starting export with {Views3D.Count} views");

                var ifcManager = new Services.IFCExportService(Document);
                ExportResult = ifcManager.Export3DViewsToIFC(Views3D, Settings, OutputFolder, LogCallback, ProgressCallback);

                LogCallback?.Invoke($"[IFC ExternalEvent] Export completed: {(ExportResult ? "SUCCESS" : "FAILED")}");

                CompletionCallback?.Invoke(ExportResult);
            }
            catch (Exception ex)
            {
                LogCallback?.Invoke($"❌ IFC ExternalEvent Exception: {ex.Message}");
                LogCallback?.Invoke($" Stack: {ex.StackTrace}");
                ExportResult = false;

                CompletionCallback?.Invoke(false);
            }
        }

        public string GetName()
        {
            return "IFC Export Handler";
        }
    }
}
