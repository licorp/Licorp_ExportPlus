using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Licorp.Diagnostics;
using LicorpExportPlus.Services.Infrastructure;
using LicorpExportPlus.ViewModels;
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
            try
            {
                RevitDocumentProvider.SetDocument(document);

                var viewModel = Host.GetService<MainViewModel>();
                if (viewModel != null)
                {
                    LicorpTrace.Info("[MainWindowFactory] Creating window with ViewModel");
                    var window = new ExportPlusMainWindow(viewModel);
                    return window;
                }

                LicorpTrace.Warn("[MainWindowFactory] ViewModel not available, using legacy constructor");
                return new ExportPlusMainWindow(document, uiApplication);
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"[MainWindowFactory] Failed to create window with ViewModel: {ex.Message}", ex);
                return new ExportPlusMainWindow(document, uiApplication);
            }
        }
    }
}
