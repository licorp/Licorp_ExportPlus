using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LicorpExportPlus.Views;

namespace LicorpExportPlus.Services
{
    public interface IMainWindowFactory
    {
        ExportPlusMainWindow Create(Document document, UIApplication uiApplication);
    }

    public class MainWindowFactory : IMainWindowFactory
    {
        public ExportPlusMainWindow Create(Document document, UIApplication uiApplication)
        {
            return new ExportPlusMainWindow(document, uiApplication);
        }
    }
}
