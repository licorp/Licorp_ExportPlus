using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Licorp.Diagnostics;
using LicorpExportPlus.Services;

namespace LicorpExportPlus
{
    [Transaction(TransactionMode.Manual)]
    public class ExportPlusCommand : IExternalCommand
    {
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

                IMainWindowLauncher windowLauncher;
                if (ExportPlusApplication.Container == null)
                {
                    LicorpTrace.Warn("ExportPlusCommand detected null Container. Using Addin Manager fallback launcher.");
                    windowLauncher = new MainWindowLauncher(new MainWindowFactory());
                }
                else
                {
                    windowLauncher = ExportPlusApplication.Container.Resolve<IMainWindowLauncher>();
                }

                if (windowLauncher == null)
                {
                    message = "Không thể khởi tạo launcher cho cửa sổ Export+.";
                    LicorpTrace.Error("ExportPlusCommand failed: IMainWindowLauncher is not registered.");
                    return Result.Failed;
                }

                windowLauncher.ShowOrActivate(doc, uiApp);

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
