using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using Licorp.Diagnostics;

namespace LicorpExportPlus.Events
{
    public class PDFExportEventHandler : IExternalEventHandler
    {
        public Document Document { get; set; }
        public List<SheetItem> SheetItems { get; set; }
        public string OutputFolder { get; set; }
        public ExportSettings Settings { get; set; }
        public Action<int, int, string, bool> ProgressCallback { get; set; }

        public bool ExportResult { get; private set; }
        public string ErrorMessage { get; private set; }

        public void Execute(UIApplication app)
        {
            try
            {

                if (Document == null)
                {
                    ErrorMessage = "Document is null";
                    ExportResult = false;
                    return;
                }

                if (SheetItems == null || SheetItems.Count == 0)
                {
                    ErrorMessage = "No sheets to export";
                    ExportResult = false;
                    return;
                }

                var pdfManager = new PDFExportService(Document);

                ExportResult = pdfManager.ExportSheetsWithCustomNames(
                    SheetItems,
                    OutputFolder,
                    Settings,
                    ProgressCallback
                );


                if (!ExportResult)
                {
                    ErrorMessage = "Export failed - check debug log for details";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Exception in PDF export: {ex.Message}";
                ExportResult = false;
            }
            finally
            {
            }
        }

        public string GetName()
        {
            return "ExportPlus PDF Export Event";
        }

        private void WriteDebugLog(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string fullMessage = $"[Export +] {timestamp} - {message}";
                System.Diagnostics.Debug.WriteLine(fullMessage);
            }
            catch { }
        }
    }
}
