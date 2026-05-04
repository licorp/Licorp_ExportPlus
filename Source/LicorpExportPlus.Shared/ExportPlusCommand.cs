using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Licorp.Diagnostics;
using LicorpExportPlus.Views;

namespace LicorpExportPlus
{
    [Transaction(TransactionMode.Manual)]
    public class ExportPlusCommand : IExternalCommand
    {
        private static ExportPlusMainWindow _window;
        private const string NoActiveDocumentMessage = "Không tìm thấy tài liệu Revit đang mở.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;

                if (uiDoc == null)
                {
                    message = NoActiveDocumentMessage;
                    LicorpTrace.Warn($"ExportPlusCommand blocked: {NoActiveDocumentMessage}");
                    return Result.Cancelled;
                }

                var doc = uiDoc.Document;
                
                if (_window == null || !_window.IsLoaded)
                {
                    _window = new ExportPlusMainWindow(doc, uiApp);
                    _window.Closed += (s, e) => _window = null;
                    _window.Show();
                }
                else
                {
                    _window.Activate();
                }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                LicorpTrace.Error("ExportPlusCommand failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
